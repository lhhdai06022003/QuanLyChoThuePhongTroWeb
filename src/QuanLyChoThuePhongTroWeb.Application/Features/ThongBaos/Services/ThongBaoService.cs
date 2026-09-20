using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.Services
{
    public class ThongBaoService : IThongBaoService
    {
        private readonly IThongBaoStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IThongBaoNotifier _notifier;

        public ThongBaoService(
            IThongBaoStore store,
            IUnitOfWork unitOfWork,
            IThongBaoNotifier notifier)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _notifier = notifier;
        }

        private static ThongBaoRes MapToRes(ThongBao t)
        {
            return new ThongBaoRes
            {
                Id = t.Id,
                NguoiDungId = t.NguoiDungId,
                TieuDe = t.TieuDe,
                NoiDung = t.NoiDung,
                LinhVuc = t.LinhVuc,
                LinkDieuHuong = t.LinkDieuHuong,
                IsRead = t.IsRead,
                CreatedAt = t.CreatedAt
            };
        }

        public async Task<List<ThongBaoRes>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            var list = await _store.LayDanhSachTheoNguoiDungAsync(nguoiDungId, cancellationToken);
            return list.Select(MapToRes).ToList();
        }

        public async Task<ServiceResult> GuiChoNguoiDungAsync(int nguoiDungId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
        {
            var thongBao = new ThongBao
            {
                NguoiDungId = nguoiDungId,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                LinhVuc = linhVuc,
                LinkDieuHuong = linkDieuHuong,
                CreatedAt = DateTime.UtcNow
            };

            _store.Add(thongBao);
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken) > 0;

            if (saved)
            {
                await _notifier.SendToUserAsync(nguoiDungId, new
                {
                    id = thongBao.Id,
                    tieuDe = thongBao.TieuDe,
                    noiDung = thongBao.NoiDung,
                    linhVuc = thongBao.LinhVuc,
                    linkDieuHuong = thongBao.LinkDieuHuong,
                    createdAt = thongBao.CreatedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                }, cancellationToken);
                return ServiceResult.Ok("Đã gửi thông báo thành công!");
            }

            return ServiceResult.Fail("Không thể lưu thông báo vào cơ sở dữ liệu.");
        }

        public async Task<ServiceResult> GuiChoKhachThueAsync(int nguoiThueId, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
        {
            var user = await _store.GetActiveNguoiDungByNguoiThueIdAsync(nguoiThueId, cancellationToken);
                
            if (user == null) 
                return ServiceResult.Fail("Không tìm thấy người dùng khách thuê hoặc tài khoản đã bị khóa.");

            return await GuiChoNguoiDungAsync(user.NguoiDungId, tieuDe, noiDung, linhVuc, linkDieuHuong, cancellationToken);
        }

        public async Task<ServiceResult> GuiChoQuyenAsync(AppRole role, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
        {
            var domainRole = (Role)(int)role;
            var targetUsers = await _store.GetActiveUserIdsByRoleAsync(domainRole, cancellationToken);

            if (!targetUsers.Any()) 
                return ServiceResult.Fail($"Không có người dùng nào thuộc quyền {role}.");

            var thongBaos = targetUsers.Select(userId => new ThongBao
            {
                NguoiDungId = userId,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                LinhVuc = linhVuc,
                LinkDieuHuong = linkDieuHuong,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _store.AddRange(thongBaos);
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken) > 0;

            if (saved)
            {
                await _notifier.SendToRoleGroupAsync(role.ToString(), new
                {
                    tieuDe = tieuDe,
                    noiDung = noiDung,
                    linhVuc = linhVuc,
                    linkDieuHuong = linkDieuHuong,
                    createdAt = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm")
                }, cancellationToken);
                
                return ServiceResult.Ok("Đã gửi thông báo cho nhóm quyền thành công!");
            }

            return ServiceResult.Fail("Lỗi khi lưu thông báo nhóm vào hệ thống.");
        }

        public async Task<ServiceResult> DanhDauDaDocAsync(int thongBaoId, CancellationToken cancellationToken = default)
        {
            var tb = await _store.GetByIdAsync(thongBaoId, cancellationToken);
            if (tb == null) return ServiceResult.Fail("Không tìm thấy thông báo.");
            if (tb.IsRead) return ServiceResult.Ok("Thông báo đã được đọc.");

            tb.IsRead = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            return ServiceResult.Ok("Đã đánh dấu đọc.");
        }

        public async Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _store.LaySoLuongChuaDocAsync(nguoiDungId, cancellationToken);
        }

        public async Task<ServiceResult> DanhDauTatCaDaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            var unreadThongBaos = await _store.GetUnreadByUserAsync(nguoiDungId, cancellationToken);

            if (!unreadThongBaos.Any()) 
                return ServiceResult.Ok("Không có thông báo chưa đọc.");

            foreach (var tb in unreadThongBaos)
            {
                tb.IsRead = true;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ServiceResult.Ok("Đã đánh dấu đọc tất cả.");
        }

        public async Task<ServiceResult> XoaThongBaoAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default)
        {
            var tb = await _store.GetByIdAndUserAsync(thongBaoId, nguoiDungId, cancellationToken);
            if (tb == null) return ServiceResult.Fail("Không tìm thấy thông báo hoặc bạn không có quyền xóa.");

            _store.Remove(tb);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            return ServiceResult.Ok("Đã xóa thông báo.");
        }

        public async Task<ServiceResult> XoaTatCaThongBaoAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            var thongBaos = await _store.GetAllByUserAsync(nguoiDungId, cancellationToken);

            if (!thongBaos.Any()) 
                return ServiceResult.Ok("Không có thông báo nào để xóa.");

            _store.RemoveRange(thongBaos);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            return ServiceResult.Ok("Đã xóa tất cả thông báo.");
        }

        public async Task<DataTableResponse<ThongBaoRes>> LayDanhSachPhanTrangAsync(DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default)
        {
            var res = await _store.LayDanhSachPhanTrangAsync(request, nguoiDungId, chuaDoc, cancellationToken);
            return new DataTableResponse<ThongBaoRes>
            {
                draw = res.draw,
                recordsTotal = res.recordsTotal,
                recordsFiltered = res.recordsFiltered,
                data = res.data.Select(MapToRes).ToList()
            };
        }
    }
}
