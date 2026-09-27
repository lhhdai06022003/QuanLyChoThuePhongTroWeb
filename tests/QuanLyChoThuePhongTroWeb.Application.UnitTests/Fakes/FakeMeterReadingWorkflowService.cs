using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    internal class FakeMeterReadingWorkflowService : IMeterReadingWorkflowService
    {
        private readonly IDienNuocStore? _dienNuocStore;
        private readonly IUnitOfWork? _unitOfWork;
        private readonly IHoaDonStore? _hoaDonStore;
        private readonly IHoaDonCalculatorService? _calculatorService;

        public FakeMeterReadingWorkflowService(
            IDienNuocStore? dienNuocStore = null,
            IUnitOfWork? unitOfWork = null,
            IHoaDonStore? hoaDonStore = null,
            IHoaDonCalculatorService? calculatorService = null)
        {
            _dienNuocStore = dienNuocStore;
            _unitOfWork = unitOfWork;
            _hoaDonStore = hoaDonStore;
            _calculatorService = calculatorService;
        }

        public List<ApproveMeterPeriodsRequest> ApprovedBatchRequests { get; } = new();
        public Func<ApproveMeterPeriodsRequest, int, ServiceResult<MeterPeriodsApprovalResult>>? OnApproveBatch { get; set; }

        public Task<ServiceResult<MeterImageWorkflowResult>> UploadImageAsync(UploadMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ServiceResult<MeterImageWorkflowResult>> RetryOcrAsync(int anhChiSoDongHoId, int actorUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ServiceResult<MeterImageWorkflowResult>> ConfirmImageAsync(ConfirmMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ServiceResult<MeterImageWorkflowResult>> CorrectConfirmedImageAsync(CorrectConfirmedMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();


        public async Task<ServiceResult<MeterPeriodsApprovalResult>> ApprovePeriodsAsync(ApproveMeterPeriodsRequest request, int actorUserId, CancellationToken cancellationToken = default)
        {
            ApprovedBatchRequests.Add(request);

            if (OnApproveBatch != null)
            {
                return OnApproveBatch(request, actorUserId);
            }

            if (_dienNuocStore != null)
            {
                var tx = _unitOfWork != null ? await _unitOfWork.BeginTransactionAsync(cancellationToken) : null;
                var currentRecords = await _dienNuocStore.GetCurrentMonthRecordsAsync(request.DanhSachPhong.Select(x => x.PhongTroId).ToList(), request.Thang, request.Nam, cancellationToken);
                var readingIds = currentRecords.Values.Select(x => x.DichVuDienNuocCuaPhongId).Where(id => id > 0).ToList();
                var invoices = _hoaDonStore != null
                    ? await _hoaDonStore.GetInvoicesByMeterReadingIdsAsync(readingIds, cancellationToken)
                    : Array.Empty<HoaDon>();
                var invoicesByReading = invoices
                    .Where(x => x.DichVuDienNuocCuaPhongId.HasValue)
                    .GroupBy(x => x.DichVuDienNuocCuaPhongId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var lockedRoomsProp = _dienNuocStore.GetType().GetProperty("LockedRooms", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var savedRecordsProp = _dienNuocStore.GetType().GetProperty("SavedRecords", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var item in request.DanhSachPhong)
                {
                    // Kiểm tra tháng sau đã chốt (LockedRooms)
                    if (lockedRoomsProp?.GetValue(_dienNuocStore) is IEnumerable<int> lockedList && lockedList.Contains(item.PhongTroId))
                    {
                        if (tx != null) await tx.RollbackAsync(cancellationToken);
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Tháng {request.Thang}/{request.Nam} không thể điều chỉnh vì tháng tiếp theo đã được chốt.");
                    }

                    if (currentRecords.TryGetValue(item.PhongTroId, out var record))
                    {
                        if (invoicesByReading.TryGetValue(record.DichVuDienNuocCuaPhongId, out var invoicesForReading))
                        {
                            if (invoicesForReading.Any(x => x.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap))
                            {
                                if (tx != null) await tx.RollbackAsync(cancellationToken);
                                return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId}: Hóa đơn của kỳ đã rời trạng thái nháp nên chỉ số đã bị khóa.");
                            }
                        }

                        record.CapNhatChiSo(
                            item.ChiSoDienMoiThuCong ?? record.ChiSoDienMoi,
                            item.ChiSoNuocMoiThuCong ?? record.ChiSoNuocMoi);
                        record.TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet;
                        _dienNuocStore.UpdateRecord(record);

                        if (_calculatorService != null && invoicesByReading.TryGetValue(record.DichVuDienNuocCuaPhongId, out var draftInvoices))
                        {
                            int daysInMonth = DateTime.DaysInMonth(request.Nam, request.Thang);
                            foreach (var inv in draftInvoices)
                            {
                                int soNgayO = daysInMonth;
                                if (inv.HopDong != null)
                                {
                                    var tienPhong = _calculatorService.TinhTienPhong(
                                        inv.HopDong.TienThuePhong,
                                        inv.HopDong.ThoiDiemBatDau,
                                        inv.HopDong.ThoiDiemKetThuc,
                                        request.Thang,
                                        request.Nam);
                                    soNgayO = tienPhong.SoNgayO;
                                }

                                var dienData = _calculatorService.TinhTienDienNuoc(record.ChiSoDienMoi, record.ChiSoDienCu, record.DonGiaDien, "Tiền điện", soNgayO, daysInMonth);
                                var nuocData = _calculatorService.TinhTienDienNuoc(record.ChiSoNuocMoi, record.ChiSoNuocCu, record.DonGiaNuoc, "Tiền nước", soNgayO, daysInMonth);

                                var dienCt = inv.ChiTietHoaDonDichVus.FirstOrDefault(x => !x.IsDeleted && (x.DichVu?.LoaiDichVu == LoaiDichVu.Dien || (x.DichVu == null && x.TenDichVu.StartsWith("Tiền điện", StringComparison.OrdinalIgnoreCase))));
                                if (dienCt == null && dienData.SoTien > 0)
                                {
                                    dienCt = new ChiTietHoaDon
                                    {
                                        HoaDonId = inv.HoaDonId,
                                        HoaDon = inv,
                                        TenDichVu = dienData.DienGiai,
                                        DonGia = record.DonGiaDien,
                                        SoLuong = decimal.Round(dienData.SoLuong, 3, MidpointRounding.AwayFromZero),
                                        TongTien = dienData.SoTien
                                    };
                                    inv.ChiTietHoaDonDichVus.Add(dienCt);
                                }
                                else if (dienCt != null)
                                {
                                    dienCt.DonGia = record.DonGiaDien;
                                    dienCt.SoLuong = decimal.Round(dienData.SoLuong, 3, MidpointRounding.AwayFromZero);
                                    dienCt.TongTien = dienData.SoTien;
                                    dienCt.TenDichVu = dienData.DienGiai;
                                }

                                var nuocCt = inv.ChiTietHoaDonDichVus.FirstOrDefault(x => !x.IsDeleted && (x.DichVu?.LoaiDichVu == LoaiDichVu.Nuoc || (x.DichVu == null && x.TenDichVu.StartsWith("Tiền nước", StringComparison.OrdinalIgnoreCase))));
                                if (nuocCt == null && nuocData.SoTien > 0)
                                {
                                    nuocCt = new ChiTietHoaDon
                                    {
                                        HoaDonId = inv.HoaDonId,
                                        HoaDon = inv,
                                        TenDichVu = nuocData.DienGiai,
                                        DonGia = record.DonGiaNuoc,
                                        SoLuong = decimal.Round(nuocData.SoLuong, 3, MidpointRounding.AwayFromZero),
                                        TongTien = nuocData.SoTien
                                    };
                                    inv.ChiTietHoaDonDichVus.Add(nuocCt);
                                }
                                else if (nuocCt != null)
                                {
                                    nuocCt.DonGia = record.DonGiaNuoc;
                                    nuocCt.SoLuong = decimal.Round(nuocData.SoLuong, 3, MidpointRounding.AwayFromZero);
                                    nuocCt.TongTien = nuocData.SoTien;
                                    nuocCt.TenDichVu = nuocData.DienGiai;
                                }

                                inv.TongTien = inv.ChiTietHoaDonDichVus
                                    .Where(x => !x.IsDeleted)
                                    .Sum(x => x.TongTien);
                                inv.NgayCapNhat = DateTime.UtcNow;
                                _hoaDonStore?.UpdateHoaDon(inv);
                            }
                        }
                    }
                    else
                    {
                        var newRecord = new DichVuDienNuocCuaPhong
                        {
                            PhongTroId = item.PhongTroId,
                            Thang = request.Thang,
                            Nam = request.Nam,
                            ChiSoDienMoi = item.ChiSoDienMoiThuCong ?? 0,
                            ChiSoNuocMoi = item.ChiSoNuocMoiThuCong ?? 0,
                            TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                            NgayTao = DateTime.UtcNow
                        };
                        await _dienNuocStore.AddRecordAsync(newRecord, cancellationToken);
                    }
                }

                if (_unitOfWork != null)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    if (tx != null)
                    {
                        await tx.CommitAsync(cancellationToken);
                    }
                }
            }

            return ServiceResult<MeterPeriodsApprovalResult>.Ok(new MeterPeriodsApprovalResult());
        }
    }
}
