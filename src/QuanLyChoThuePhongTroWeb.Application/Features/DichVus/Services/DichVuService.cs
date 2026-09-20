using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Services
{
    public class DichVuService : IDichVuService
    {
        private readonly IDichVuStore _store;
        private readonly IUnitOfWork _unitOfWork;

        public DichVuService(IDichVuStore store, IUnitOfWork unitOfWork)
        {
            _store = store;
            _unitOfWork = unitOfWork;
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> CreateDichVuAsync(DichVuReq input)
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateDichVuAsync(int id, DichVuReq input)
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteDichVuAsync(int id)
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

        public async Task<DataTableResponse<DichVuChiNhanhRes>> GetDanhSachDichVuChiNhanhAsync(DataTableRequest request, int chiNhanhId)
        {
            return await _store.GetPagedDichVuChiNhanhAsync(request, chiNhanhId);
        }

        public async Task<DichVuChiNhanhRes?> GetDichVuChiNhanhByIdAsync(int id)
        {
            var dcn = await _store.GetDichVuChiNhanhByIdAsync(id);
            if (dcn == null) return null;

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

        public async Task<(bool IsSuccess, string? ErrorMessage)> CreateDichVuChiNhanhAsync(DichVuChiNhanhReq input)
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> UpdateDichVuChiNhanhAsync(int id, DichVuChiNhanhReq input)
        {
            var dcn = await _store.GetDichVuChiNhanhByIdAsync(id);
            if (dcn == null) return (false, "Không tìm thấy bảng giá dịch vụ.");

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

        public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteDichVuChiNhanhAsync(int id)
        {
            var dcn = await _store.GetDichVuChiNhanhByIdAsync(id);
            if (dcn == null) return (false, "Không tìm thấy bảng giá dịch vụ.");

            dcn.IsDeleted = true;
            dcn.NgayCapNhat = DateTime.UtcNow;
            _store.UpdateDichVuChiNhanh(dcn);
            await _unitOfWork.SaveChangesAsync();
            return (true, null);
        }

        // ===== ĐĂNG KÝ DỊCH VỤ CHO PHÒNG =====

        public async Task<List<DichVuChiNhanhWithDangKyRes>> GetDichVuVaDangKyCuaPhongAsync(int phongTroId)
        {
            var phong = await _store.GetPhongTroByIdAsync(phongTroId);
            if (phong == null) return new List<DichVuChiNhanhWithDangKyRes>();

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

        public async Task<(bool IsSuccess, string? ErrorMessage)> LuuDangKyDichVuAsync(DangKyDichVuReq input)
        {
            var phong = await _store.GetPhongTroByIdAsync(input.PhongTroId);
            if (phong == null) return (false, "Phòng trọ không tồn tại.");

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
