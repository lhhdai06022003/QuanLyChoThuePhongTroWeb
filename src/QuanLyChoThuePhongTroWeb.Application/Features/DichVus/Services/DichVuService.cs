using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Services
{
    public class DichVuService : IDichVuService
    {
        private const string KhongCoQuyenBangGia = "Bạn không có quyền thiết lập bảng giá dịch vụ tại chi nhánh này.";

        private readonly IDichVuStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _access;

        public DichVuService(IDichVuStore store, IUnitOfWork unitOfWork, IEmployeeAccessService access)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _access = access;
        }


        private async Task<bool> IsAdminAsync(int actorId)
        {
            return (await _access.GetScopeAsync(actorId))?.IsAdmin == true;
        }

        public async Task<ServiceResult> CreateDichVuAsync(int actorId, DichVuReq input)
        {
            return await IsAdminAsync(actorId) ? ServiceResult.FromTuple(await CreateDichVuCoreAsync(input)) : ServiceResult.Forbidden("Chỉ Admin được thêm dịch vụ hệ thống.");
        }

        public async Task<ServiceResult> UpdateDichVuAsync(int actorId, int id, DichVuReq input)
        {
            return await IsAdminAsync(actorId) ? ServiceResult.FromTuple(await UpdateDichVuCoreAsync(id, input)) : ServiceResult.Forbidden("Chỉ Admin được sửa dịch vụ hệ thống.");
        }

        public async Task<ServiceResult> DeleteDichVuAsync(int actorId, int id)
        {
            return await IsAdminAsync(actorId) ? ServiceResult.FromTuple(await DeleteDichVuCoreAsync(id)) : ServiceResult.Forbidden("Chỉ Admin được xóa dịch vụ hệ thống.");
        }

        public async Task<DataTableResponse<DichVuRes>> GetDanhSachDichVuAsync(DataTableRequest request)
        {
            return await _store.GetPagedDichVuAsync(request);
        }

        public async Task<DichVuRes?> GetDichVuByIdAsync(int id)
        {
            var dv = await _store.GetDichVuByIdAsync(id);
            if (dv == null) return null;
            return new DichVuRes { DichVuId = dv.DichVuId, TenDichVu = dv.TenDichVu, DonVi = dv.DonVi, GhiChu = dv.GhiChu };
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> CreateDichVuCoreAsync(DichVuReq input)
        {
            bool exists = await _store.ExistsDichVuNameAsync(input.TenDichVu);
            if (exists) return (false, "Tên dịch vụ đã tồn tại.");

            var dv = new DichVu
            {
                TenDichVu = input.TenDichVu,
                DonVi = input.DonVi,
                GhiChu = input.GhiChu
            };
            _store.AddDichVu(dv);
            await _unitOfWork.SaveChangesAsync();
            return (true, null);
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> UpdateDichVuCoreAsync(int id, DichVuReq input)
        {
            var dv = await _store.GetDichVuByIdAsync(id);
            if (dv == null) return (false, "Không tìm thấy dịch vụ.");

            bool exists = await _store.ExistsDichVuNameAsync(input.TenDichVu, id);
            if (exists) return (false, "Tên dịch vụ đã tồn tại.");

            dv.TenDichVu = input.TenDichVu;
            dv.DonVi = input.DonVi;
            dv.GhiChu = input.GhiChu;
            dv.NgayCapNhat = DateTime.UtcNow;

            _store.UpdateDichVu(dv);
            await _unitOfWork.SaveChangesAsync();
            return (true, null);
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> DeleteDichVuCoreAsync(int id)
        {
            var dv = await _store.GetDichVuByIdAsync(id);
            if (dv == null) return (false, "Không tìm thấy dịch vụ.");

            // Check if any active Branch Service exists
            bool inUse = await _store.IsDichVuInUseAsync(id);
            if (inUse) return (false, "Dịch vụ đang được sử dụng ở chi nhánh. Vui lòng xóa bảng giá trước.");

            dv.IsDeleted = true;
            dv.NgayCapNhat = DateTime.UtcNow;

            _store.UpdateDichVu(dv);
            await _unitOfWork.SaveChangesAsync();
            return (true, null);
        }

        public async Task<DataTableResponse<DichVuChiNhanhRes>> GetDanhSachDichVuChiNhanhAsync(int actorId, DataTableRequest request, int chiNhanhId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || (chiNhanhId > 0 && !scope.CanAccessBranch(chiNhanhId)))
            {
                return new DataTableResponse<DichVuChiNhanhRes> { draw = request.Draw };
            }

            return await _store.GetPagedDichVuChiNhanhAsync(request, chiNhanhId, scope.AllowedBranchIds);
        }

        public async Task<DichVuChiNhanhRes?> GetDichVuChiNhanhByIdAsync(int actorId, int id)
        {
            var scope = await _access.GetScopeAsync(actorId);
            var dcn = scope == null ? null : await _store.GetDichVuChiNhanhByIdAsync(id);
            if (dcn == null || !scope!.CanAccessBranch(dcn.ChiNhanhId)) return null;

            return new DichVuChiNhanhRes
            {
                DichVuChiNhanhId = dcn.DichVuChiNhanhId,
                ChiNhanhId = dcn.ChiNhanhId,
                TenChiNhanh = dcn.ChiNhanh.TenChiNhanh,
                DichVuId = dcn.DichVuId,
                TenDichVu = dcn.DichVu.TenDichVu,
                GiaDichVu = dcn.GiaDichVu,
                MacDinh = dcn.MacDinh
            };
        }

        public async Task<ServiceResult> CreateDichVuChiNhanhAsync(int actorId, DichVuChiNhanhReq input)
        {
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !scope.CanAccessBranch(input.ChiNhanhId))
                return ServiceResult.Forbidden(KhongCoQuyenBangGia);

            return ServiceResult.FromTuple(await CreateDichVuChiNhanhCoreAsync(input));
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> CreateDichVuChiNhanhCoreAsync(DichVuChiNhanhReq input)
        {
            bool exists = await _store.ExistsDichVuChiNhanhAsync(input.ChiNhanhId, input.DichVuId);
            if (exists) return (false, "Dịch vụ này đã được cài đặt giá cho chi nhánh đã chọn.");

            var dcn = new DichVuChiNhanh
            {
                ChiNhanhId = input.ChiNhanhId,
                DichVuId = input.DichVuId,
                GiaDichVu = input.GiaDichVu,
                MacDinh = input.MacDinh
            };
            _store.AddDichVuChiNhanh(dcn);
            await _unitOfWork.SaveChangesAsync();
            return (true, null);
        }

        public async Task<ServiceResult> UpdateDichVuChiNhanhAsync(int actorId, int id, DichVuChiNhanhReq input)
        {
            var dcn = await _store.GetDichVuChiNhanhByIdAsync(id);
            if (dcn == null) return ServiceResult.NotFound("Không tìm thấy bảng giá dịch vụ.");

            // Kiểm cả chi nhánh hiện tại và chi nhánh mới: không được chuyển dòng giá sang chi nhánh khác.
            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !scope.CanAccessBranch(dcn.ChiNhanhId) || !scope.CanAccessBranch(input.ChiNhanhId))
                return ServiceResult.Forbidden(KhongCoQuyenBangGia);

            return ServiceResult.FromTuple(await UpdateDichVuChiNhanhCoreAsync(dcn, id, input));
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> UpdateDichVuChiNhanhCoreAsync(DichVuChiNhanh dcn, int id, DichVuChiNhanhReq input)
        {

            bool exists = await _store.ExistsDichVuChiNhanhAsync(input.ChiNhanhId, input.DichVuId, id);
            if (exists) return (false, "Dịch vụ này đã được cài đặt giá cho chi nhánh đã chọn.");

            dcn.ChiNhanhId = input.ChiNhanhId;
            dcn.DichVuId = input.DichVuId;
            dcn.GiaDichVu = input.GiaDichVu;
            dcn.MacDinh = input.MacDinh;
            dcn.NgayCapNhat = DateTime.UtcNow;

            _store.UpdateDichVuChiNhanh(dcn);
            await _unitOfWork.SaveChangesAsync();
            return (true, null);
        }

        public async Task<ServiceResult> DeleteDichVuChiNhanhAsync(int actorId, int id)
        {
            var dcn = await _store.GetDichVuChiNhanhByIdAsync(id);
            if (dcn == null) return ServiceResult.NotFound("Không tìm thấy bảng giá dịch vụ.");

            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !scope.CanAccessBranch(dcn.ChiNhanhId))
                return ServiceResult.Forbidden(KhongCoQuyenBangGia);

            dcn.IsDeleted = true;
            dcn.NgayCapNhat = DateTime.UtcNow;
            _store.UpdateDichVuChiNhanh(dcn);
            await _unitOfWork.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // ===== ĐĂNG KÝ DỊCH VỤ CHO PHÒNG =====

        public async Task<List<DichVuChiNhanhWithDangKyRes>?> GetDichVuVaDangKyCuaPhongAsync(int actorId, int phongTroId)
        {
            var scope = await _access.GetScopeAsync(actorId);
            var phong = scope == null ? null : await _store.GetPhongTroByIdAsync(phongTroId);
            if (phong == null || !scope!.CanAccessBranch(phong.ChiNhanhId)) return null;

            int chiNhanhId = phong.ChiNhanhId;

            var dichVuChiNhanhs = await _store.GetActiveDichVuChiNhanhsAsync(chiNhanhId);
            var dangKys = await _store.GetActiveDangKyDichVusAsync(phongTroId);

            var result = dichVuChiNhanhs.Select(dcn =>
            {
                var dk = dangKys.FirstOrDefault(d => d.DichVuChiNhanhId == dcn.DichVuChiNhanhId);
                return new DichVuChiNhanhWithDangKyRes
                {
                    DichVuChiNhanhId = dcn.DichVuChiNhanhId,
                    TenDichVu = dcn.DichVu.TenDichVu,
                    DonVi = dcn.DichVu.DonVi,
                    GiaDichVu = dcn.GiaDichVu,
                    MacDinh = dcn.MacDinh,
                    IsSelected = dk != null,
                    SoLuong = dk?.SoLuong ?? 1,
                    DangKyDichVuId = dk?.DangKyDichVuId,
                    NgayBatDau = dk?.NgayBatDau
                };
            }).OrderByDescending(x => x.MacDinh).ThenBy(x => x.TenDichVu).ToList();

            return result;
        }

        public async Task<ServiceResult> LuuDangKyDichVuAsync(int actorId, DangKyDichVuReq input)
        {
            var phong = await _store.GetPhongTroByIdAsync(input.PhongTroId);
            if (phong == null) return ServiceResult.NotFound("Phòng trọ không tồn tại.");

            var scope = await _access.GetScopeAsync(actorId);
            if (scope == null || !scope.CanAccessBranch(phong.ChiNhanhId))
                return ServiceResult.Forbidden("Bạn không có quyền đăng ký dịch vụ cho phòng tại chi nhánh này.");

            // Dịch vụ được chọn phải là bảng giá đang dùng của đúng chi nhánh chứa phòng (áp dụng cả Admin).
            var bangGiaCuaChiNhanh = (await _store.GetActiveDichVuChiNhanhsAsync(phong.ChiNhanhId))
                .Select(x => x.DichVuChiNhanhId)
                .ToHashSet();
            if (input.DichVus.Any(x => x.IsSelected && !bangGiaCuaChiNhanh.Contains(x.DichVuChiNhanhId)))
                return ServiceResult.Fail("Có dịch vụ không thuộc bảng giá của chi nhánh chứa phòng.");

            return ServiceResult.FromTuple(await LuuDangKyDichVuCoreAsync(input));
        }

        private async Task<(bool IsSuccess, string? ErrorMessage)> LuuDangKyDichVuCoreAsync(DangKyDichVuReq input)
        {

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var activeDangKys = await _store.GetActiveDangKyDichVusAsync(input.PhongTroId);

                foreach (var item in input.DichVus)
                {
                    var existing = activeDangKys.FirstOrDefault(x => x.DichVuChiNhanhId == item.DichVuChiNhanhId);

                    if (item.IsSelected)
                    {
                        int targetQty = item.SoLuong > 0 ? item.SoLuong : 1;
                        DateTime targetStart = item.NgayBatDau.HasValue
                            ? DateTime.SpecifyKind(item.NgayBatDau.Value, DateTimeKind.Utc)
                            : DateTime.UtcNow;

                        if (existing != null)
                        {
                            if (existing.SoLuong != targetQty)
                            {
                                existing.NgayKetThuc = item.NgayBatDau.HasValue
                                    ? DateTime.SpecifyKind(item.NgayBatDau.Value.AddDays(-1), DateTimeKind.Utc)
                                    : DateTime.UtcNow;

                                _store.AddDangKyDichVu(new DangKyDichVu
                                {
                                    PhongTroId = input.PhongTroId,
                                    DichVuChiNhanhId = item.DichVuChiNhanhId,
                                    SoLuong = targetQty,
                                    NgayBatDau = targetStart
                                });
                            }
                            else
                            {
                                if (item.NgayBatDau.HasValue)
                                {
                                    existing.NgayBatDau = targetStart;
                                }
                            }
                        }
                        else
                        {
                            _store.AddDangKyDichVu(new DangKyDichVu
                            {
                                PhongTroId = input.PhongTroId,
                                DichVuChiNhanhId = item.DichVuChiNhanhId,
                                SoLuong = targetQty,
                                NgayBatDau = targetStart
                            });
                        }
                    }
                    else
                    {
                        if (existing != null)
                        {
                            existing.NgayKetThuc = DateTime.UtcNow;
                        }
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, null);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                string errorDetails = e.InnerException != null ? e.InnerException.Message : e.Message;
                return (false, $"Lỗi hệ thống: {errorDetails}");
            }
        }
    }
}
