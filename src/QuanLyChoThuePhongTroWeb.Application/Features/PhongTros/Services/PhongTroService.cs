using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services
{
    public class PhongTroService : IPhongTroService
    {
        private const string KhongTimThayPhong = "Không tìm thấy phòng trọ!";
        private const string ChiAdmin = "Chỉ Admin được thêm, xóa hoặc phát sinh phòng trọ.";

        private readonly IPhongTroStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _access;

        public PhongTroService(IPhongTroStore store, IUnitOfWork unitOfWork, IEmployeeAccessService access)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _access = access;
        }


        private async Task<bool> IsAdminAsync(int actorId)
        {
            return (await _access.GetScopeAsync(actorId))?.IsAdmin == true;
        }

        public async Task<IReadOnlyList<SelectOptionDto>> GetDanhSachChiNhanhDropdownAsync(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null) return Array.Empty<SelectOptionDto>();

            return await _store.GetChiNhanhDropdownAsync(scope.AllowedBranchIds);
        }

        public async Task<IReadOnlyList<PhongTroListItemDto>> GetDanhSachPhongTroAsync(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null) return Array.Empty<PhongTroListItemDto>();

            return await _store.GetDanhSachPhongTroAsync(scope.AllowedBranchIds);
        }

        public async Task<ServiceResult> GetPhongTroByIdAsync(int actorId, int id)
        {
            var scope = await _access.GetScopeAsync(actorId);
            var phong = await _store.GetByIdAsync(id);
            // Phòng của chi nhánh khác trả "không tìm thấy" để không lộ việc phòng tồn tại.
            if (phong == null || scope == null || !scope.CanAccessBranch(phong.ChiNhanhId))
                return ServiceResult.NotFound(KhongTimThayPhong);

            var res = new PhongTroRes
            {
                PhongTroId = phong.PhongTroId,
                ChiNhanhId = phong.ChiNhanhId,
                TenChiNhanh = phong.ChiNhanh?.TenChiNhanh,
                SoPhong = phong.SoPhong,
                TangLau = phong.TangLau,
                GiaThue = phong.GiaThue,
                DienTich = phong.DienTich,
                SoNguoiToiDa = phong.SoNguoiToiDa,
                TrangThai = (QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiPhong)(int)phong.TrangThai,
                MoTa = phong.MoTa,
                NgayTao = phong.NgayTao
            };
            return ServiceResult.Ok("Thành công", res);
        }

        public async Task<ServiceResult> ThemPhongTroAsync(int actorId, PhongTroReq model)
        {
            if (!await IsAdminAsync(actorId))
                return ServiceResult.Forbidden(ChiAdmin);

            if (string.IsNullOrWhiteSpace(model.SoPhong))
                return ServiceResult.Fail("Số phòng không được để trống!");

            if (model.ChiNhanhId <= 0)
                return ServiceResult.Fail("Vui lòng chọn chi nhánh!");

            bool isExist = await _store.ExistsSoPhongAsync(model.SoPhong, model.ChiNhanhId);
            if (isExist)
                return ServiceResult.Fail("Số phòng này đã tồn tại trong chi nhánh!");

            var entity = new PhongTro
            {
                ChiNhanhId = model.ChiNhanhId,
                SoPhong = model.SoPhong,
                TangLau = model.TangLau,
                GiaThue = model.GiaThue,
                DienTich = model.DienTich,
                SoNguoiToiDa = model.SoNguoiToiDa,
                TrangThai = (TrangThaiPhong)(int)model.TrangThai,
                MoTa = model.MoTa ?? string.Empty,
                NgayTao = DateTime.UtcNow,
                IsDeleted = false
            };

            _store.Add(entity);
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult.Ok("Thêm phòng trọ thành công!");
        }

        public async Task<ServiceResult> CapNhatPhongTroAsync(int actorId, PhongTroReq model)
        {
            if (model.PhongTroId <= 0)
                return ServiceResult.Fail("ID không hợp lệ!");

            var scope = await _access.GetScopeAsync(actorId);
            var phongTonTai = await _store.GetByIdAsync(model.PhongTroId);
            if (phongTonTai == null)
                return ServiceResult.NotFound("Dữ liệu không tồn tại hoặc đã bị xóa!");

            // Kiểm cả chi nhánh hiện tại và chi nhánh mới: nhân viên không được chuyển phòng sang chi nhánh khác.
            if (scope == null || !scope.CanAccessBranch(phongTonTai.ChiNhanhId) || !scope.CanAccessBranch(model.ChiNhanhId))
                return ServiceResult.Forbidden("Bạn không có quyền cập nhật phòng trọ tại chi nhánh này.");

            bool isExist = await _store.ExistsSoPhongAsync(model.SoPhong, model.ChiNhanhId, model.PhongTroId);
            if (isExist)
                return ServiceResult.Fail("Số phòng này đã tồn tại trong chi nhánh!");

            var domainTrangThai = (TrangThaiPhong)(int)model.TrangThai;

            // Kiểm tra ràng buộc khi có hợp đồng đang hoạt động
            bool coHopDongHoatDong = await _store.HasActiveHopDongAsync(model.PhongTroId);

            if (coHopDongHoatDong)
            {
                if (phongTonTai.ChiNhanhId != model.ChiNhanhId)
                {
                    return ServiceResult.Fail("Không thể chuyển chi nhánh cho phòng trọ đang có hợp đồng hoạt động!");
                }
                if (phongTonTai.SoPhong != model.SoPhong)
                {
                    return ServiceResult.Fail("Không thể đổi số phòng của phòng trọ đang có hợp đồng hoạt động!");
                }
                if (domainTrangThai == TrangThaiPhong.Trong || domainTrangThai == TrangThaiPhong.BaoTri)
                {
                    return ServiceResult.Fail("Không thể chuyển trạng thái phòng về Trống hoặc Bảo trì khi đang có hợp đồng hoạt động!");
                }
                if (model.SoNguoiToiDa < phongTonTai.SoNguoiToiDa)
                {
                    int currentActiveMembers = await _store.GetActiveMembersCountForActiveContractAsync(model.PhongTroId);
                    if (model.SoNguoiToiDa < currentActiveMembers)
                    {
                        return ServiceResult.Fail($"Số người tối đa mới ({model.SoNguoiToiDa} người) không được nhỏ hơn số người đang ở thực tế trong phòng ({currentActiveMembers} người)!");
                    }
                }
            }

            // Cập nhật dữ liệu
            phongTonTai.ChiNhanhId = model.ChiNhanhId;
            phongTonTai.SoPhong = model.SoPhong;
            phongTonTai.TangLau = model.TangLau;
            phongTonTai.GiaThue = model.GiaThue;
            phongTonTai.DienTich = model.DienTich;
            phongTonTai.SoNguoiToiDa = model.SoNguoiToiDa;
            phongTonTai.TrangThai = domainTrangThai;
            phongTonTai.MoTa = model.MoTa ?? string.Empty;
            phongTonTai.NgayCapNhat = DateTime.UtcNow;

            _store.Update(phongTonTai);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok("Cập nhật phòng trọ thành công!");
        }

        public async Task<ServiceResult> XoaPhongTroAsync(int actorId, int id)
        {
            if (!await IsAdminAsync(actorId))
                return ServiceResult.Forbidden(ChiAdmin);

            var phong = await _store.GetByIdAsync(id);
            if (phong == null)
                return ServiceResult.NotFound(KhongTimThayPhong);

            if (phong.TrangThai == TrangThaiPhong.DaThue)
            {
                return ServiceResult.Fail("Không thể xóa phòng trọ đang trong trạng thái cho thuê!");
            }

            bool coHopDongHoatDong = await _store.HasActiveHopDongAsync(id);
            if (coHopDongHoatDong)
            {
                return ServiceResult.Fail("Không thể xóa phòng trọ đang có hợp đồng hoạt động!");
            }

            phong.IsDeleted = true;
            phong.NgayCapNhat = DateTime.UtcNow;

            _store.Update(phong);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok("Xóa phòng trọ thành công!");
        }

        public async Task<List<PhongTroOptionDto>> DanhSachPhongTroConTrong(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null) return new List<PhongTroOptionDto>();

            var list = await _store.GetDanhSachPhongTroConTrongAsync(scope.AllowedBranchIds);
            return list.Select(p => new PhongTroOptionDto
            {
                PhongTroId = p.PhongTroId,
                ChiNhanhId = p.ChiNhanhId,
                SoPhong = p.SoPhong,
                GiaThue = p.GiaThue,
                TrangThai = (QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiPhong)(int)p.TrangThai
            }).ToList();
        }

        public async Task<List<PhongCardRes>> GetSoDoPhongAsync(int actorId, int chiNhanhId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || (chiNhanhId > 0 && !scope.CanAccessBranch(chiNhanhId)))
                return new List<PhongCardRes>();

            return await _store.GetSoDoPhongAsync(chiNhanhId, scope.AllowedBranchIds);
        }

        public async Task<QuickContractDto?> GetQuickContractAsync(int actorId, int phongTroId)
        {
            return await CanAccessRoomAsync(actorId, phongTroId) ? await _store.GetQuickContractAsync(phongTroId) : null;
        }

        public async Task<UnpaidInvoiceDto?> GetUnpaidInvoiceAsync(int actorId, int phongTroId)
        {
            return await CanAccessRoomAsync(actorId, phongTroId) ? await _store.GetUnpaidInvoiceAsync(phongTroId) : null;
        }

        private async Task<bool> CanAccessRoomAsync(int actorId, int phongTroId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null) return false;

            var phong = await _store.GetByIdAsync(phongTroId);
            return phong != null && scope.CanAccessBranch(phong.ChiNhanhId);
        }

        public async Task<ServiceResult> PhatSinhNgauNhienAsync(int actorId)
        {
            if (!await IsAdminAsync(actorId))
                return ServiceResult.Forbidden(ChiAdmin);

            var chiNhanhs = await _store.GetAllActiveChiNhanhsAsync();
            if (!chiNhanhs.Any())
            {
                return ServiceResult.Fail("Không tìm thấy chi nhánh nào trong database. Vui lòng thêm chi nhánh trước!");
            }

            var random = new Random();
            var addedRooms = new List<string>();

            var allRooms = await _store.GetAllRoomNumbersAsync();

            var branchMaxRooms = new Dictionary<int, int>();
            foreach (var cn in chiNhanhs)
            {
                var maxRoom = allRooms.Where(p => p.ChiNhanhId == cn.ChiNhanhId).Select(p => p.SoPhong).ToList();
                int maxVal = 100;
                if (maxRoom.Any())
                {
                    maxVal = maxRoom
                        .Select(sp => int.TryParse(sp, out var n) ? n : 100)
                        .Max();
                }
                branchMaxRooms[cn.ChiNhanhId] = maxVal;
            }

            for (int i = 0; i < 10; i++)
            {
                var chiNhanh = chiNhanhs[random.Next(chiNhanhs.Count)];
                int nextRoomNum = branchMaxRooms[chiNhanh.ChiNhanhId] + 1;
                branchMaxRooms[chiNhanh.ChiNhanhId] = nextRoomNum;

                var pt = new PhongTro
                {
                    ChiNhanhId = chiNhanh.ChiNhanhId,
                    SoPhong = nextRoomNum.ToString(),
                    TangLau = (nextRoomNum / 100) == 0 ? 1 : (nextRoomNum / 100),
                    GiaThue = random.Next(18, 45) * 100000,
                    DienTich = random.Next(15, 30),
                    SoNguoiToiDa = random.Next(2, 4),
                    TrangThai = TrangThaiPhong.Trong,
                    MoTa = $"Phòng {nextRoomNum} ngẫu nhiên thuộc {chiNhanh.TenChiNhanh}",
                    NgayTao = DateTime.UtcNow
                };
                _store.Add(pt);
                addedRooms.Add($"{pt.SoPhong} ({chiNhanh.TenChiNhanh})");
            }

            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok($"Đã phát sinh 10 phòng thành công: {string.Join(", ", addedRooms)}");
        }
    }
}
