using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Services
{
    public class NguoiThueService : INguoiThueService
    {
        private const string KhongTimThay = "Không tìm thấy người thuê.";

        private readonly INguoiThueStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _access;

        public NguoiThueService(INguoiThueStore store, IUnitOfWork unitOfWork, IEmployeeAccessService access)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _access = access;
        }


        private async Task<bool> IsVisibleAsync(EmployeeAccessScope scope, int nguoiThueId)
        {
            return scope.IsAdmin || await _store.IsVisibleAsync(nguoiThueId, scope.AllowedBranchIds!);
        }

        private static NguoiThueRes MapToRes(NguoiThue entity)
        {
            return new NguoiThueRes
            {
                NguoiThueId = entity.NguoiThueId,
                HoVaTen = entity.HoVaTen,
                Email = entity.Email,
                SoDienThoai = entity.SoDienThoai,
                CCCD = entity.CCCD,
                NgayCapCCCD = entity.NgayCapCCCD,
                NoiCapCCCD = entity.NoiCapCCCD,
                NgaySinh = entity.NgaySinh,
                QueQuan = entity.QueQuan,
                GhiChu = entity.GhiChu,
                NgayTao = entity.NgayTao
            };
        }

        public async Task<IEnumerable<NguoiThueRes>> GetAllAsync(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null) return new List<NguoiThueRes>();

            var list = await _store.GetAllAsync(scope.AllowedBranchIds);
            var result = new List<NguoiThueRes>();
            foreach (var item in list)
            {
                result.Add(MapToRes(item));
            }
            return result;
        }

        public async Task<IEnumerable<NguoiThueRes>> GetAvailableAsync(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null) return new List<NguoiThueRes>();

            var list = await _store.GetAvailableAsync(scope.AllowedBranchIds);
            var result = new List<NguoiThueRes>();
            foreach (var item in list)
            {
                result.Add(MapToRes(item));
            }
            return result;
        }

        public async Task<NguoiThueRes?> GetByIdAsync(int id)
        {
            var entity = await _store.GetByIdAsync(id);
            return entity == null ? null : MapToRes(entity);
        }

        public async Task<NguoiThueRes?> GetByIdForStaffAsync(int actorId, int id)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !await IsVisibleAsync(scope, id)) return null;

            return await GetByIdAsync(id);
        }

        public async Task<ServiceResult> CreateAsync(int actorId, NguoiThueReq input)
        {
            // Người thuê mới chưa có hợp đồng nên không thuộc chi nhánh nào; chỉ cần actor hợp lệ.
            if (await _access.GetScopeAsync(actorId) == null)
                return ServiceResult.Forbidden("Bạn không có quyền thêm người thuê.");

            return ServiceResult.FromTuple(await CreateCoreAsync(input));
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> CreateCoreAsync(NguoiThueReq input)
        {
            try
            {
                bool isDuplicate = await _store.ExistsDuplicateAsync(input.CCCD, input.SoDienThoai, input.Email);
                if (isDuplicate) return (false, "CCCD, Số điện thoại hoặc Email đã tồn tại trong hệ thống.");

                var entity = new NguoiThue
                {
                    HoVaTen = input.HoVaTen,
                    Email = input.Email,
                    SoDienThoai = input.SoDienThoai,
                    CCCD = input.CCCD,
                    NoiCapCCCD = input.NoiCapCCCD,
                    QueQuan = input.QueQuan,
                    GhiChu = input.GhiChu,
                    NgaySinh = input.NgaySinh?.ToUniversalTime(),
                    NgayCapCCCD = input.NgayCapCCCD?.ToUniversalTime(),
                    NgayTao = DateTime.UtcNow,
                    IsDeleted = false
                };

                _store.Add(entity);
                await _unitOfWork.SaveChangesAsync();
                return (true, string.Empty);
            }
            catch (Exception e)
            {
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống: {errorDetails}");
            }
        }

        public async Task<ServiceResult> UpdateAsync(int actorId, int id, NguoiThueUpdateDto input)
        {
            var entity = await _store.GetByIdAsync(id);
            if (entity == null) return ServiceResult.NotFound(KhongTimThay);

            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !await IsVisibleAsync(scope, id))
                return ServiceResult.Forbidden("Bạn không có quyền cập nhật người thuê thuộc chi nhánh khác.");

            bool isDuplicate = await _store.ExistsDuplicateAsync(input.CCCD, input.SoDienThoai, input.Email, id);
            if (isDuplicate) return ServiceResult.Fail("CCCD, Số điện thoại hoặc Email bị trùng với khách khác.");

            entity.HoVaTen = input.HoVaTen;
            entity.Email = input.Email;
            entity.SoDienThoai = input.SoDienThoai;
            entity.CCCD = input.CCCD;
            entity.NoiCapCCCD = input.NoiCapCCCD;
            entity.QueQuan = input.QueQuan;
            entity.GhiChu = input.GhiChu;
            entity.NgaySinh = input.NgaySinh?.ToUniversalTime();
            entity.NgayCapCCCD = input.NgayCapCCCD?.ToUniversalTime();
            entity.NgayCapNhat = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAsync(int actorId, int id)
        {
            var entity = await _store.GetByIdAsync(id);
            if (entity == null) return ServiceResult.NotFound(KhongTimThay);

            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !await IsVisibleAsync(scope, id))
                return ServiceResult.Forbidden("Bạn không có quyền xóa người thuê thuộc chi nhánh khác.");

            bool laDaiDienHopDongHoatDong = await _store.IsDaiDienHopDongHoatDongAsync(id);
            if (laDaiDienHopDongHoatDong)
            {
                return ServiceResult.Fail("Không thể xóa do người thuê đang là đại diện ký hợp đồng còn hiệu lực.");
            }

            bool laThanhVienHopDongHoatDong = await _store.IsThanhVienHopDongHoatDongAsync(id);
            if (laThanhVienHopDongHoatDong)
            {
                return ServiceResult.Fail("Không thể xóa do người thuê đang là thành viên ở chung trong một hợp đồng còn hiệu lực.");
            }

            entity.IsDeleted = true;
            entity.NgayCapNhat = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            return ServiceResult.Ok();
        }

        public async Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThue(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            return scope == null ? Array.Empty<SelectOptionDto>() : await _store.GetDropdownListAsync(scope.AllowedBranchIds);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueChuaCoPhong(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            return scope == null ? Array.Empty<SelectOptionDto>() : await _store.GetDropdownChuaCoPhongAsync(scope.AllowedBranchIds);
        }

        public async Task<IReadOnlyList<SelectOptionDto>> DanhSachNguoiThueCoHopDongAsync(int actorId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            return scope == null ? Array.Empty<SelectOptionDto>() : await _store.GetDropdownCoHopDongAsync(scope.AllowedBranchIds);
        }

        public async Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(int actorId, string searchTerm)
        {
            var scope = await _access.GetScopeAsync(actorId);
            return scope == null ? Array.Empty<NguoiThueAutocompleteDto>() : await _store.SearchAutocompleteAsync(searchTerm, scope.AllowedBranchIds);
        }

        public async Task<ServiceResult> PhatSinhNgauNhienAsync(int actorId)
        {
            if ((await _access.GetScopeAsync(actorId))?.IsAdmin != true)
                return ServiceResult.Forbidden("Chỉ Admin được phát sinh người thuê ngẫu nhiên.");

            var random = new Random();

            string[] hoList = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng" };
            string[] demList = { "Văn", "Thị", "Hữu", "Minh", "Anh", "Đức", "Ngọc", "Tuấn", "Hoàng", "Quốc" };
            string[] tenList = { "Anh", "Dũng", "Hùng", "Cường", "Trang", "Vy", "Hải", "Tuấn", "Nam", "Lan", "Hương", "Long", "Minh", "Khánh", "Đức" };
            string[] tinhList = { "Hà Nội", "TP. Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Hải Phòng", "Đồng Nai", "Bình Dương", "Long An", "Tiền Giang", "Lâm Đồng" };

            var addedTenants = new List<string>();

            for (int i = 0; i < 10; i++)
            {
                string hoTen = $"{hoList[random.Next(hoList.Length)]} {demList[random.Next(demList.Length)]} {tenList[random.Next(tenList.Length)]}";

                var nguoiThue = new NguoiThue
                {
                    HoVaTen = hoTen,
                    Email = $"tenant.{random.Next(1000, 9999)}@example.com",
                    SoDienThoai = $"09{random.Next(10000000, 99999999)}",
                    CCCD = $"{random.Next(100000000, 999999999)}{random.Next(100, 999)}",
                    QueQuan = tinhList[random.Next(tinhList.Length)],
                    NgayTao = DateTime.UtcNow,
                    IsDeleted = false
                };
                _store.Add(nguoiThue);
                addedTenants.Add(hoTen);
            }

            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok($"Đã thêm 10 người thuê ngẫu nhiên thành công: {string.Join(", ", addedTenants)}");
        }
    }
}