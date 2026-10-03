using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services
{
    public class YeuCauSuCoService : IYeuCauSuCoService
    {
        private readonly IYeuCauSuCoStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IInvoiceIssuanceStore _invoiceIssuanceStore;
        private readonly ILogger<YeuCauSuCoService> _logger;

        public YeuCauSuCoService(
            IYeuCauSuCoStore store,
            IUnitOfWork unitOfWork,
            IEmployeeAccessService employeeAccessService,
            IInvoiceIssuanceStore invoiceIssuanceStore,
            ILogger<YeuCauSuCoService> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _employeeAccessService = employeeAccessService;
            _invoiceIssuanceStore = invoiceIssuanceStore;
            _logger = logger;
        }

        private static YeuCauSuCoRes MapToRes(YeuCauSuCo entity)
        {
            return new YeuCauSuCoRes
            {
                Id = entity.Id,
                PhongTroId = entity.PhongTroId,
                NguoiThueId = entity.NguoiThueId,
                TieuDe = entity.TieuDe,
                MoTa = entity.MoTa,
                HinhAnhUrl = entity.HinhAnhUrl,
                TrangThai = (AppTrangThaiSuCo)(int)entity.TrangThai,
                ChiPhiSuaChua = entity.ChiPhiSuaChua,
                CongVaoHoaDon = entity.CongVaoHoaDon,
                LyDoTuChoi = entity.LyDoTuChoi,
                GhiChuAdmin = entity.GhiChuAdmin,
                NgayGui = entity.NgayGui,
                NgayXuLy = entity.NgayXuLy,
                PhongTro = new PhongTroInfo
                {
                    SoPhong = entity.PhongTro?.SoPhong ?? "",
                    ChiNhanh = new ChiNhanhInfo
                    {
                        TenChiNhanh = entity.PhongTro?.ChiNhanh?.TenChiNhanh ?? ""
                    }
                },
                NguoiThue = new NguoiThueInfo
                {
                    HoVaTen = entity.NguoiThue?.HoVaTen ?? "",
                    SoDienThoai = entity.NguoiThue?.SoDienThoai ?? ""
                }
            };
        }

        public async Task<List<YeuCauSuCoRes>> GetAllAsync(int? chiNhanhId, AppTrangThaiSuCo? trangThai, int? soThang = 6)
        {
            var domainTrangThai = trangThai.HasValue ? (TrangThaiSuCo?)(int)trangThai.Value : null;
            var list = await _store.GetAllAsync(chiNhanhId, domainTrangThai, soThang);
            return list.Select(MapToRes).ToList();
        }

        public async Task<List<YeuCauSuCoRes>> GetByNguoiThueAsync(int nguoiThueId)
        {
            var list = await _store.GetByNguoiThueAsync(nguoiThueId);
            return list.Select(MapToRes).ToList();
        }

        public async Task<YeuCauSuCoRes?> GetByIdAsync(int id)
        {
            var entity = await _store.GetByIdAsync(id);
            return entity == null ? null : MapToRes(entity);
        }

        public async Task<bool> CreateAsync(CreateYeuCauSuCoReq req)
        {
            var entity = new YeuCauSuCo
            {
                PhongTroId = req.PhongTroId,
                NguoiThueId = req.NguoiThueId,
                TieuDe = req.TieuDe,
                MoTa = req.MoTa ?? string.Empty,
                HinhAnhUrl = req.HinhAnhUrl,
                TrangThai = (TrangThaiSuCo)(int)req.TrangThai,
                NgayGui = DateTime.UtcNow,
                IsDeleted = false
            };
            _store.Add(entity);
            return await _unitOfWork.SaveChangesAsync() > 0;
        }

        public async Task<ServiceResult> UpdateStatusAsync(
            int id,
            AppTrangThaiSuCo trangThai,
            decimal chiPhi,
            bool congVaoHoaDon,
            string? lyDoTuChoi,
            string? ghiChuAdmin,
            int actorId,
            CancellationToken ct = default)
        {
            if (chiPhi < 0 || !InvoiceMoney.IsWholeVnd(chiPhi))
            {
                return ServiceResult.Fail("Chi phí sửa chữa phải là số nguyên VND không âm.");
            }

            var suco = await _store.GetByIdAsync(id, ct);
            if (suco == null || suco.IsDeleted)
            {
                return ServiceResult.Fail("Không tìm thấy sự cố.");
            }

            var branchId = suco.PhongTro?.ChiNhanhId ?? 0;
            var canUpdate = await _employeeAccessService.CanPerformAsync(actorId, branchId, EmployeeActionCodes.InvoiceDraft, ct);
            if (!canUpdate)
            {
                return ServiceResult.Fail("Bạn không có quyền cập nhật sự cố tại chi nhánh này.");
            }

            // Ghi nhận giá trị trước khi sửa
            var oldTrangThai = suco.TrangThai;
            var oldChiPhi = suco.ChiPhiSuaChua;
            var oldCongVaoHoaDon = suco.CongVaoHoaDon;
            var oldNgayXuLy = suco.NgayXuLy;
            var oldLyDoTuChoi = suco.LyDoTuChoi;
            var oldGhiChuAdmin = suco.GhiChuAdmin;

            // Áp giá trị mới
            var newDomainTrangThai = (TrangThaiSuCo)(int)trangThai;
            suco.TrangThai = newDomainTrangThai;
            suco.ChiPhiSuaChua = chiPhi;
            suco.CongVaoHoaDon = congVaoHoaDon;
            suco.GhiChuAdmin = ghiChuAdmin;

            // Gán NgayXuLy = DateTime.UtcNow khi trangThaiCu != trangThaiMoi và trạng thái mới là DangXuLy, DaHoanThanh hoặc DaHuy (D1)
            if (oldTrangThai != newDomainTrangThai && (newDomainTrangThai == TrangThaiSuCo.DangXuLy || newDomainTrangThai == TrangThaiSuCo.DaHoanThanh || newDomainTrangThai == TrangThaiSuCo.DaHuy))
            {
                suco.NgayXuLy = DateTime.UtcNow;
            }

            if (newDomainTrangThai == TrangThaiSuCo.DaHuy)
            {
                suco.LyDoTuChoi = lyDoTuChoi;
            }
            else
            {
                suco.LyDoTuChoi = null;
            }

            // Tính số tiền tính phí cũ và mới
            var oldBilledAmount = (oldTrangThai == TrangThaiSuCo.DaHoanThanh && oldCongVaoHoaDon && oldChiPhi > 0 && !suco.IsDeleted) ? oldChiPhi : 0m;
            var newBilledAmount = (newDomainTrangThai == TrangThaiSuCo.DaHoanThanh && congVaoHoaDon && chiPhi > 0 && !suco.IsDeleted) ? chiPhi : 0m;

            var oldPeriod = GetVnPeriod(oldNgayXuLy);
            var newPeriod = GetVnPeriod(suco.NgayXuLy);

            if ((oldBilledAmount == 0m && newBilledAmount == 0m) || (oldBilledAmount == newBilledAmount && oldPeriod == newPeriod))
            {
                // Lưu bình thường không đụng hóa đơn
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Ok("Cập nhật trạng thái thành công.");
            }

            // Ngược lại: đồng bộ với hóa đơn trong transaction
            void RevertInMemoryChanges()
            {
                suco.TrangThai = oldTrangThai;
                suco.ChiPhiSuaChua = oldChiPhi;
                suco.CongVaoHoaDon = oldCongVaoHoaDon;
                suco.NgayXuLy = oldNgayXuLy;
                suco.LyDoTuChoi = oldLyDoTuChoi;
                suco.GhiChuAdmin = oldGhiChuAdmin;
            }

            return await SaveWithInvoiceSyncAsync(suco, oldBilledAmount, oldPeriod, newBilledAmount, newPeriod,
                RevertInMemoryChanges, "Cập nhật trạng thái thành công.", "Không thể cập nhật sự cố. Vui lòng thử lại.", ct);
        }

        private async Task<ServiceResult> SaveWithInvoiceSyncAsync(
            YeuCauSuCo suco,
            decimal oldBilledAmount,
            (int Thang, int Nam)? oldPeriod,
            decimal newBilledAmount,
            (int Thang, int Nam)? newPeriod,
            Action revertInMemoryChanges,
            string successMessage,
            string systemErrorMessage,
            CancellationToken ct)
        {
            var affectedPeriods = new List<(int Thang, int Nam)>();
            if (oldPeriod.HasValue && oldBilledAmount > 0)
            {
                affectedPeriods.Add(oldPeriod.Value);
            }
            if (newPeriod.HasValue && newBilledAmount > 0)
            {
                affectedPeriods.Add(newPeriod.Value);
            }

            var distinctPeriods = affectedPeriods.Distinct().OrderBy(p => p.Nam).ThenBy(p => p.Thang).ToList();

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                var contractIds = await _invoiceIssuanceStore.GetContractIdsForTenantRoomAsync(suco.PhongTroId, suco.NguoiThueId, ct);
                var sortedContractIds = contractIds.OrderBy(x => x).ToList();
                if (sortedContractIds.Any())
                {
                    await _invoiceIssuanceStore.LockContractsAsync(sortedContractIds, ct);
                }

                foreach (var (thang, nam) in distinctPeriods)
                {
                    var hoaDon = await _invoiceIssuanceStore.GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(suco.PhongTroId, suco.NguoiThueId, thang, nam, ct);
                    if (hoaDon == null)
                    {
                        continue;
                    }

                    if (hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap)
                    {
                        revertInMemoryChanges();
                        await tx.RollbackAsync(ct);
                        return ServiceResult.Fail($"Hóa đơn tháng {thang}/{nam} đã gửi duyệt hoặc đã chốt. Admin cần trả lại hoặc hủy hóa đơn trước khi sửa chi phí sự cố.");
                    }

                    var startOfMonthVn = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
                    var startUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
                    var nextMonthStartUtc = DateTime.SpecifyKind(startOfMonthVn.AddMonths(1).AddHours(-7), DateTimeKind.Utc);

                    var billableIncidents = await _invoiceIssuanceStore.GetBillableIncidentsInPeriodAsync(new[] { suco.PhongTroId }, startUtc, nextMonthStartUtc, ct);
                    var tenantIncidents = billableIncidents
                        .Where(x => x.NguoiThueId == suco.NguoiThueId && x.Id != suco.Id)
                        .ToList();

                    if (newPeriod.HasValue && newPeriod.Value.Thang == thang && newPeriod.Value.Nam == nam && newBilledAmount > 0)
                    {
                        tenantIncidents.Add(suco);
                    }

                    var newLines = InvoiceIncidentLines.BuildIncidentLines(tenantIncidents);
                    InvoiceIncidentLines.ReplaceIncidentLines(hoaDon, newLines);

                    hoaDon.TongTien = InvoiceMoney.RoundVnd(hoaDon.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).Sum(x => x.TongTien));
                }

                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return ServiceResult.Ok(successMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lưu sự cố và đồng bộ hóa đơn Id={Id}", suco.Id);
                revertInMemoryChanges();
                await tx.RollbackAsync(ct);
                return ServiceResult.Fail(systemErrorMessage);
            }
        }

        public async Task<ServiceResult> SoftDeleteAsync(int id, int actorId, CancellationToken ct = default)
        {
            var suco = await _store.GetByIdAsync(id, ct);
            if (suco == null || suco.IsDeleted)
            {
                return ServiceResult.Fail("Không tìm thấy sự cố.");
            }

            var branchId = suco.PhongTro?.ChiNhanhId ?? 0;
            var canDelete = await _employeeAccessService.CanPerformAsync(actorId, branchId, EmployeeActionCodes.InvoiceDraft, ct);
            if (!canDelete)
            {
                return ServiceResult.Fail("Bạn không có quyền xóa sự cố tại chi nhánh này.");
            }

            var oldBilledAmount = (suco.TrangThai == TrangThaiSuCo.DaHoanThanh && suco.CongVaoHoaDon && suco.ChiPhiSuaChua > 0) ? suco.ChiPhiSuaChua : 0m;
            var oldPeriod = GetVnPeriod(suco.NgayXuLy);

            suco.IsDeleted = true;

            if (oldBilledAmount == 0m)
            {
                try
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xóa sự cố Id={Id}", suco.Id);
                    suco.IsDeleted = false;
                    return ServiceResult.Fail("Không thể xóa sự cố. Vui lòng thử lại.");
                }

                return ServiceResult.Ok("Xóa sự cố thành công.");
            }

            return await SaveWithInvoiceSyncAsync(suco, oldBilledAmount, oldPeriod, 0m, null,
                () => suco.IsDeleted = false, "Xóa sự cố thành công.", "Không thể xóa sự cố. Vui lòng thử lại.", ct);
        }

        private static (int Thang, int Nam)? GetVnPeriod(DateTime? utc)
        {
            if (!utc.HasValue) return null;
            var vn = utc.Value.AddHours(7);
            return (vn.Month, vn.Year);
        }
    }
}
