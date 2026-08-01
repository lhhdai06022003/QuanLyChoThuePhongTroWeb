using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Hubs;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.ThongBaos
{
    public class ThongBaoService : IThongBaoService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ThongBaoHub> _hubContext;

        public ThongBaoService(ApplicationDbContext context, IHubContext<ThongBaoHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<List<ThongBao>> LayDanhSachTheoNguoiDungAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos
                .AsNoTracking()
                .Where(x => x.NguoiDungId == nguoiDungId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);
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

            _context.ThongBaos.Add(thongBao);
            var saved = await _context.SaveChangesAsync(cancellationToken) > 0;

            if (saved)
            {
                await _hubContext.Clients.User(nguoiDungId.ToString()).SendAsync("NhanThongBao", new
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
            var user = await _context.NguoiDungs
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.NguoiThueId == nguoiThueId && !u.IsDeleted && u.IsActive, cancellationToken);
                
            if (user == null) 
                return ServiceResult.Fail("Không tìm thấy người dùng khách thuê hoặc tài khoản đã bị khóa.");

            return await GuiChoNguoiDungAsync(user.NguoiDungId, tieuDe, noiDung, linhVuc, linkDieuHuong, cancellationToken);
        }

        public async Task<ServiceResult> GuiChoQuyenAsync(Role role, string tieuDe, string noiDung, string? linhVuc = null, string? linkDieuHuong = null, CancellationToken cancellationToken = default)
        {
            var targetUsers = await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.Role == role && !u.IsDeleted && u.IsActive)
                .Select(u => u.NguoiDungId)
                .ToListAsync(cancellationToken);

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

            _context.ThongBaos.AddRange(thongBaos);
            var saved = await _context.SaveChangesAsync(cancellationToken) > 0;

            if (saved)
            {
                await _hubContext.Clients.Group(role.ToString()).SendAsync("NhanThongBao", new
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
            var tb = await _context.ThongBaos.FirstOrDefaultAsync(x => x.Id == thongBaoId, cancellationToken);
            if (tb == null) return ServiceResult.Fail("Không tìm thấy thông báo.");
            if (tb.IsRead) return ServiceResult.Ok("Thông báo đã được đọc.");

            tb.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
            
            return ServiceResult.Ok("Đã đánh dấu đọc.");
        }

        public async Task<int> LaySoLuongChuaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            return await _context.ThongBaos
                .AsNoTracking()
                .CountAsync(x => x.NguoiDungId == nguoiDungId && !x.IsRead, cancellationToken);
        }

        public async Task<ServiceResult> DanhDauTatCaDaDocAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            var unreadThongBaos = await _context.ThongBaos
                .Where(x => x.NguoiDungId == nguoiDungId && !x.IsRead)
                .ToListAsync(cancellationToken);

            if (!unreadThongBaos.Any()) 
                return ServiceResult.Ok("Không có thông báo chưa đọc.");

            foreach (var tb in unreadThongBaos)
            {
                tb.IsRead = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return ServiceResult.Ok("Đã đánh dấu đọc tất cả.");
        }

        public async Task<ServiceResult> XoaThongBaoAsync(int thongBaoId, int nguoiDungId, CancellationToken cancellationToken = default)
        {
            var tb = await _context.ThongBaos.FirstOrDefaultAsync(x => x.Id == thongBaoId && x.NguoiDungId == nguoiDungId, cancellationToken);
            if (tb == null) return ServiceResult.Fail("Không tìm thấy thông báo hoặc bạn không có quyền xóa.");

            _context.ThongBaos.Remove(tb);
            await _context.SaveChangesAsync(cancellationToken);
            
            return ServiceResult.Ok("Đã xóa thông báo.");
        }

        public async Task<ServiceResult> XoaTatCaThongBaoAsync(int nguoiDungId, CancellationToken cancellationToken = default)
        {
            var thongBaos = await _context.ThongBaos
                .Where(x => x.NguoiDungId == nguoiDungId)
                .ToListAsync(cancellationToken);

            if (!thongBaos.Any()) 
                return ServiceResult.Ok("Không có thông báo nào để xóa.");

            _context.ThongBaos.RemoveRange(thongBaos);
            await _context.SaveChangesAsync(cancellationToken);
            
            return ServiceResult.Ok("Đã xóa tất cả thông báo.");
        }

        public async Task<QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableResponse<ThongBao>> LayDanhSachPhanTrangAsync(QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableRequest request, int nguoiDungId, bool? chuaDoc, CancellationToken cancellationToken = default)
        {
            int recordsTotal = await _context.ThongBaos
                .AsNoTracking()
                .CountAsync(x => x.NguoiDungId == nguoiDungId, cancellationToken);
            
            var query = _context.ThongBaos
                .AsNoTracking()
                .Where(x => x.NguoiDungId == nguoiDungId);

            if (chuaDoc.HasValue)
            {
                query = query.Where(x => x.IsRead == !chuaDoc.Value);
            }

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                query = query.Where(x => x.TieuDe.ToLower().Contains(request.SearchValue.ToLower()) 
                                      || x.NoiDung.ToLower().Contains(request.SearchValue.ToLower()));
            }

            int recordsFiltered = await query.CountAsync(cancellationToken);

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync(cancellationToken);

            return new QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests.DataTableResponse<ThongBao>
            {
                draw = request.Draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = data
            };
        }
    }
}
