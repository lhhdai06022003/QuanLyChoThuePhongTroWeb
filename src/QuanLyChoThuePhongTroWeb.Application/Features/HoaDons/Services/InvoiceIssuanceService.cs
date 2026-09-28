using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class InvoiceIssuanceService : IInvoiceIssuanceService
    {
        private readonly IInvoiceIssuanceStore _issuanceStore;
        private readonly IHoaDonStore _hoaDonStore;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IHoaDonCalculatorService _calculatorService;
        private readonly ILogger<InvoiceIssuanceService> _logger;

        public InvoiceIssuanceService(
            IInvoiceIssuanceStore issuanceStore,
            IHoaDonStore hoaDonStore,
            IUnitOfWork unitOfWork,
            IEmployeeAccessService employeeAccessService,
            IHoaDonCalculatorService calculatorService,
            ILogger<InvoiceIssuanceService> logger)
        {
            _issuanceStore = issuanceStore;
            _hoaDonStore = hoaDonStore;
            _unitOfWork = unitOfWork;
            _employeeAccessService = employeeAccessService;
            _calculatorService = calculatorService;
            _logger = logger;
        }

        public async Task<ServiceResult<InvoiceDraftBatchResult>> CreateDraftsAsync(CreateInvoiceDraftsRequest request, int actorId, CancellationToken ct = default)
        {
            if (actorId <= 0)
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Người thực hiện không hợp lệ.");
            }

            if (request.Thang < 1 || request.Thang > 12 || request.Nam < 2000)
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Tháng hoặc năm không hợp lệ.");
            }

            if (request.PhongTroIds == null || !request.PhongTroIds.Any())
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Không có phòng nào được chọn để phát sinh hóa đơn nháp.");
            }

            var distinctSelectedRoomIds = request.PhongTroIds.Distinct().ToList();
            var roomBranches = await _hoaDonStore.GetRoomBranchIdsAsync(distinctSelectedRoomIds, ct);
            if (roomBranches.Count != distinctSelectedRoomIds.Count)
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Một hoặc nhiều phòng trọ không tồn tại hoặc đã bị xóa.");
            }

            var distinctBranchIds = roomBranches.Values.Distinct().ToList();
            if (distinctBranchIds.Count > 1)
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Dữ liệu không hợp lệ: các phòng trọ thuộc nhiều chi nhánh khác nhau.");
            }

            var actualBranchId = distinctBranchIds.First();
            if (request.ChiNhanhId != actualBranchId)
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Chi nhánh yêu cầu không khớp với chi nhánh thực tế của các phòng trọ.");
            }

            var canDraft = await _employeeAccessService.CanPerformAsync(actorId, actualBranchId, EmployeeActionCodes.InvoiceDraft, ct);
            if (!canDraft)
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Bạn không có quyền tạo hóa đơn nháp tại chi nhánh này.");
            }

            var chiNhanh = await _hoaDonStore.GetChiNhanhByIdAsync(actualBranchId, ct);
            if (chiNhanh == null)
            {
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Chi nhánh không tồn tại.");
            }

            var startOfMonthVn = new DateTime(request.Nam, request.Thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endOfMonthVn = startOfMonthVn.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);
            var startOfMonthUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var endOfMonthUtc = DateTime.SpecifyKind(endOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            var nextMonthStartUtc = DateTime.SpecifyKind(startOfMonthVn.AddMonths(1).AddHours(-7), DateTimeKind.Utc);
            var daysInMonth = DateTime.DaysInMonth(request.Nam, request.Thang);

            var hopDongs = await _hoaDonStore.GetValidContractsForBillingAsync(actualBranchId, distinctSelectedRoomIds, startOfMonthUtc, endOfMonthUtc, ct);

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                var hopDongIds = hopDongs.Select(h => h.HopDongId).OrderBy(x => x).ToList();
                if (hopDongIds.Any())
                {
                    await _issuanceStore.LockContractsAsync(hopDongIds, ct);
                }

                var existingInvoiceContractIds = await _hoaDonStore.GetExistingInvoiceContractIdsAsync(hopDongIds, request.Thang, request.Nam, ct);
                var existingInvoiceSet = new HashSet<int>(existingInvoiceContractIds);

                var meterReadings = await _hoaDonStore.GetDichVuDienNuocByRoomIdsAsync(distinctSelectedRoomIds, request.Thang, request.Nam, ct);
                var meterDict = meterReadings.ToDictionary(x => x.PhongTroId);

                var serviceRegistrations = await _hoaDonStore.GetDangKyDichVusForBillingAsync(distinctSelectedRoomIds, startOfMonthUtc, endOfMonthUtc, ct);
                var serviceLookup = serviceRegistrations.ToLookup(x => x.PhongTroId);

                var billableIncidents = await _issuanceStore.GetBillableIncidentsInPeriodAsync(distinctSelectedRoomIds, startOfMonthUtc, nextMonthStartUtc, ct);
                var incidentLookup = billableIncidents.ToLookup(x => x.PhongTroId);

                var items = new List<InvoiceDraftItemResult>();
                var createdCount = 0;

                foreach (var hd in hopDongs.OrderBy(x => x.HopDongId))
                {
                    var baseRoomName = hd.PhongTro?.SoPhong ?? distinctSelectedRoomIds.First(x => x == hd.PhongTroId).ToString();

                    if (existingInvoiceSet.Contains(hd.HopDongId))
                    {
                        items.Add(new InvoiceDraftItemResult
                        {
                            PhongTroId = hd.PhongTroId,
                            SoPhong = baseRoomName,
                            HopDongId = hd.HopDongId,
                            MaHopDong = hd.MaHopDong,
                            Status = InvoiceDraftItemStatus.SkippedHasActiveInvoice,
                            Message = $"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Đã phát sinh hóa đơn trong tháng này."
                        });
                        continue;
                    }

                    if (!meterDict.TryGetValue(hd.PhongTroId, out var meterPeriod))
                    {
                        items.Add(new InvoiceDraftItemResult
                        {
                            PhongTroId = hd.PhongTroId,
                            SoPhong = baseRoomName,
                            HopDongId = hd.HopDongId,
                            MaHopDong = hd.MaHopDong,
                            Status = InvoiceDraftItemStatus.SkippedNoMeterReading,
                            Message = $"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Chưa có chỉ số điện/nước tháng này."
                        });
                        continue;
                    }

                    if (meterPeriod.TrangThaiGhiNhan != TrangThaiGhiNhan.DaDuyet)
                    {
                        items.Add(new InvoiceDraftItemResult
                        {
                            PhongTroId = hd.PhongTroId,
                            SoPhong = baseRoomName,
                            HopDongId = hd.HopDongId,
                            MaHopDong = hd.MaHopDong,
                            Status = InvoiceDraftItemStatus.SkippedMeterNotApproved,
                            Message = $"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Chỉ số điện/nước chưa được duyệt."
                        });
                        continue;
                    }

                    (decimal SoTien, string DienGiai, int SoNgayO) tienPhongData;
                    try
                    {
                        tienPhongData = _calculatorService.TinhTienPhong(hd.TienThuePhong, hd.ThoiDiemBatDau, hd.ThoiDiemKetThuc, request.Thang, request.Nam);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex, "Calculator error for contract {HopDongId}", hd.HopDongId);
                        items.Add(new InvoiceDraftItemResult
                        {
                            PhongTroId = hd.PhongTroId,
                            SoPhong = baseRoomName,
                            HopDongId = hd.HopDongId,
                            MaHopDong = hd.MaHopDong,
                            Status = InvoiceDraftItemStatus.Invalid,
                            Message = $"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Dữ liệu tính tiền phòng không hợp lệ."
                        });
                        continue;
                    }

                    if (tienPhongData.SoNgayO <= 0)
                    {
                        items.Add(new InvoiceDraftItemResult
                        {
                            PhongTroId = hd.PhongTroId,
                            SoPhong = baseRoomName,
                            HopDongId = hd.HopDongId,
                            MaHopDong = hd.MaHopDong,
                            Status = InvoiceDraftItemStatus.SkippedNoOccupancy,
                            Message = $"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Số ngày ở bằng 0."
                        });
                        continue;
                    }

                    var chiTietList = new List<ChiTietHoaDon>();
                    var tongTien = 0m;

                    if (tienPhongData.SoTien > 0)
                    {
                        chiTietList.Add(new ChiTietHoaDon
                        {
                            TenDichVu = tienPhongData.DienGiai,
                            DonGia = hd.TienThuePhong,
                            SoLuong = 1,
                            TongTien = tienPhongData.SoTien,
                            DichVuId = null
                        });
                        tongTien += tienPhongData.SoTien;
                    }

                    try
                    {
                        var dienData = _calculatorService.TinhTienDienNuoc(meterPeriod.ChiSoDienMoi, meterPeriod.ChiSoDienCu, meterPeriod.DonGiaDien, "Điện", tienPhongData.SoNgayO, daysInMonth);
                        if (dienData.SoTien > 0)
                        {
                            chiTietList.Add(new ChiTietHoaDon
                            {
                                TenDichVu = dienData.DienGiai,
                                DonGia = meterPeriod.DonGiaDien,
                                SoLuong = decimal.Round(dienData.SoLuong, 3, MidpointRounding.AwayFromZero),
                                TongTien = dienData.SoTien,
                                DichVuId = null
                            });
                            tongTien += dienData.SoTien;
                        }

                        var nuocData = _calculatorService.TinhTienDienNuoc(meterPeriod.ChiSoNuocMoi, meterPeriod.ChiSoNuocCu, meterPeriod.DonGiaNuoc, "Nước", tienPhongData.SoNgayO, daysInMonth);
                        if (nuocData.SoTien > 0)
                        {
                            chiTietList.Add(new ChiTietHoaDon
                            {
                                TenDichVu = nuocData.DienGiai,
                                DonGia = meterPeriod.DonGiaNuoc,
                                SoLuong = decimal.Round(nuocData.SoLuong, 3, MidpointRounding.AwayFromZero),
                                TongTien = nuocData.SoTien,
                                DichVuId = null
                            });
                            tongTien += nuocData.SoTien;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex, "Meter calculation error for contract {HopDongId}", hd.HopDongId);
                        items.Add(new InvoiceDraftItemResult
                        {
                            PhongTroId = hd.PhongTroId,
                            SoPhong = baseRoomName,
                            HopDongId = hd.HopDongId,
                            MaHopDong = hd.MaHopDong,
                            Status = InvoiceDraftItemStatus.Invalid,
                            Message = $"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Dữ liệu tính điện nước không hợp lệ."
                        });
                        continue;
                    }

                    var dsDichVu = serviceLookup[hd.PhongTroId];
                    foreach (var dk in dsDichVu)
                    {
                        var dvChiNhanh = dk.DichVuChiNhanh;
                        var dv = dvChiNhanh?.DichVu;
                        if (dvChiNhanh == null || dv == null || dv.LoaiDichVu == LoaiDichVu.Dien || dv.LoaiDichVu == LoaiDichVu.Nuoc)
                            continue;

                        var dvData = _calculatorService.TinhTienDichVuCoDinh(dvChiNhanh.GiaDichVu, dk.SoLuong, dv.TenDichVu, dk.NgayBatDau, dk.NgayKetThuc, request.Thang, request.Nam, hd.ThoiDiemBatDau, hd.ThoiDiemKetThuc);
                        if (dvData.SoTien > 0)
                        {
                            chiTietList.Add(new ChiTietHoaDon
                            {
                                TenDichVu = dvData.DienGiai,
                                DonGia = dvChiNhanh.GiaDichVu,
                                SoLuong = dk.SoLuong,
                                TongTien = dvData.SoTien,
                                DichVuId = dv.DichVuId
                            });
                            tongTien += dvData.SoTien;
                        }
                    }

                    var suCos = incidentLookup[hd.PhongTroId]
                        .Where(sc => sc.NguoiThueId == hd.NguoiThueId)
                        .ToList();
                    var incidentLines = InvoiceIncidentLines.BuildIncidentLines(suCos);
                    foreach (var scLine in incidentLines)
                    {
                        chiTietList.Add(scLine);
                        tongTien += scLine.TongTien;
                    }

                    var baseInvoiceCode = $"HD-{chiNhanh.MaChiNhanh}-P{baseRoomName}-{hd.HopDongId}-{request.Thang:D2}{request.Nam}";
                    var cancelledCount = await _issuanceStore.CountCancelledInvoicesAsync(hd.HopDongId, request.Thang, request.Nam, ct);
                    var finalInvoiceCode = cancelledCount > 0 ? $"{baseInvoiceCode}-R{cancelledCount}" : baseInvoiceCode;

                    var hoaDon = new HoaDon
                    {
                        MaHoaDon = finalInvoiceCode,
                        HopDongId = hd.HopDongId,
                        Thang = request.Thang,
                        Nam = request.Nam,
                        TongTien = decimal.Round(tongTien, 2, MidpointRounding.AwayFromZero),
                        TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                        TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                        DichVuDienNuocCuaPhongId = meterPeriod.DichVuDienNuocCuaPhongId,
                        ChiTietHoaDonDichVus = chiTietList
                    };

                    hoaDon.KhoiTaoNhap(actorId);
                    await _issuanceStore.AddInvoiceAsync(hoaDon, ct);

                    items.Add(new InvoiceDraftItemResult
                    {
                        PhongTroId = hd.PhongTroId,
                        SoPhong = baseRoomName,
                        HopDongId = hd.HopDongId,
                        MaHopDong = hd.MaHopDong,
                        Status = InvoiceDraftItemStatus.Created,
                        HoaDonId = hoaDon.HoaDonId,
                        MaHoaDon = hoaDon.MaHoaDon,
                        Message = $"Phòng {baseRoomName} (HĐ: {hd.MaHopDong}): Đã tạo hóa đơn nháp."
                    });
                    createdCount++;
                }

                if (createdCount > 0)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);

                return ServiceResult<InvoiceDraftBatchResult>.Ok(new InvoiceDraftBatchResult
                {
                    Items = items,
                    CreatedCount = createdCount
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi phát sinh hóa đơn nháp chi nhánh {ChiNhanhId}, kỳ {Thang}/{Nam}", request.ChiNhanhId, request.Thang, request.Nam);
                return ServiceResult<InvoiceDraftBatchResult>.Fail("Không thể tạo hóa đơn nháp. Vui lòng thử lại.");
            }
        }

        public async Task<ServiceResult<InvoiceTransitionResult>> SubmitAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            if (actorId <= 0) return ServiceResult<InvoiceTransitionResult>.Fail("Người thực hiện không hợp lệ.");

            var lockTargets = await _issuanceStore.GetInvoiceLockTargetsAsync(hoaDonId, ct);
            if (lockTargets == null) return ServiceResult<InvoiceTransitionResult>.Fail("Không tìm thấy hóa đơn.");

            var (meterPeriodId, branchId) = lockTargets.Value;
            var canSubmit = await _employeeAccessService.CanPerformAsync(actorId, branchId, EmployeeActionCodes.InvoiceSubmit, ct);
            if (!canSubmit) return ServiceResult<InvoiceTransitionResult>.Fail("Bạn không có quyền gửi duyệt hóa đơn tại chi nhánh này.");

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                if (meterPeriodId.HasValue)
                {
                    await _issuanceStore.LockMeterPeriodAsync(meterPeriodId.Value, ct);
                }

                var hoaDon = await _issuanceStore.GetInvoiceForUpdateAsync(hoaDonId, ct);
                if (hoaDon == null)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Không tìm thấy hóa đơn hoặc hóa đơn đã bị hủy.");
                }

                if (hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail($"Chỉ hóa đơn ở trạng thái Nháp mới có thể gửi duyệt.");
                }

                var activeDetails = hoaDon.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
                if (!activeDetails.Any())
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Hóa đơn không có chi tiết dịch vụ nào.");
                }

                var detailTotal = activeDetails.Sum(x => x.TongTien);
                if (hoaDon.TongTien <= 0 || hoaDon.TongTien != detailTotal)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Tổng tiền hóa đơn không khớp với tổng chi tiết dịch vụ.");
                }

                hoaDon.GuiDuyet(actorId, "Nhân viên gửi duyệt hóa đơn");
                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return ServiceResult<InvoiceTransitionResult>.Ok(new InvoiceTransitionResult
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaHoaDon = hoaDon.MaHoaDon,
                    TrangThaiPhatHanh = hoaDon.TrangThaiPhatHanh
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi gửi duyệt hóa đơn {HoaDonId}", hoaDonId);
                return ServiceResult<InvoiceTransitionResult>.Fail("Không thể gửi duyệt hóa đơn. Vui lòng thử lại.");
            }
        }

        public async Task<ServiceResult<InvoiceTransitionResult>> FinalizeAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            if (actorId <= 0) return ServiceResult<InvoiceTransitionResult>.Fail("Người thực hiện không hợp lệ.");

            var lockTargets = await _issuanceStore.GetInvoiceLockTargetsAsync(hoaDonId, ct);
            if (lockTargets == null) return ServiceResult<InvoiceTransitionResult>.Fail("Không tìm thấy hóa đơn.");

            var (meterPeriodId, branchId) = lockTargets.Value;
            var canFinalize = await _employeeAccessService.CanPerformAsync(actorId, branchId, EmployeeActionCodes.InvoiceFinalize, ct);
            if (!canFinalize) return ServiceResult<InvoiceTransitionResult>.Fail("Chỉ Quản trị viên mới có quyền chốt hóa đơn.");

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                TrangThaiGhiNhan? periodStatus = null;
                if (meterPeriodId.HasValue)
                {
                    periodStatus = await _issuanceStore.LockMeterPeriodAsync(meterPeriodId.Value, ct);
                }

                var hoaDon = await _issuanceStore.GetInvoiceForUpdateAsync(hoaDonId, ct);
                if (hoaDon == null)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Không tìm thấy hóa đơn hoặc hóa đơn đã bị hủy.");
                }

                if (hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.ChoDuyet)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Chỉ hóa đơn ở trạng thái chờ duyệt mới có thể chốt.");
                }

                if (meterPeriodId.HasValue && periodStatus != TrangThaiGhiNhan.DaDuyet)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Kỳ chỉ số điện nước liên kết không còn ở trạng thái đã duyệt.");
                }

                var activeDetails = hoaDon.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
                var detailTotal = activeDetails.Sum(x => x.TongTien);
                if (hoaDon.TongTien <= 0 || hoaDon.TongTien != detailTotal)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Tổng tiền hóa đơn không khớp với tổng chi tiết dịch vụ.");
                }

                var hasOtherActive = await _issuanceStore.HasOtherActiveInvoiceAsync(hoaDon.HopDongId, hoaDon.Thang, hoaDon.Nam, hoaDon.HoaDonId, ct);
                if (hasOtherActive)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Đã tồn tại hóa đơn đang hoạt động khác cho hợp đồng trong kỳ này.");
                }

                hoaDon.ChotHoaDon(actorId, "Admin chốt hóa đơn");
                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return ServiceResult<InvoiceTransitionResult>.Ok(new InvoiceTransitionResult
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaHoaDon = hoaDon.MaHoaDon,
                    TrangThaiPhatHanh = hoaDon.TrangThaiPhatHanh
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi chốt hóa đơn {HoaDonId}", hoaDonId);
                return ServiceResult<InvoiceTransitionResult>.Fail("Không thể chốt hóa đơn. Vui lòng thử lại.");
            }
        }

        public async Task<ServiceResult<InvoiceTransitionResult>> RejectAsync(int hoaDonId, string lyDo, int actorId, CancellationToken ct = default)
        {
            if (actorId <= 0) return ServiceResult<InvoiceTransitionResult>.Fail("Người thực hiện không hợp lệ.");

            if (string.IsNullOrWhiteSpace(lyDo))
            {
                return ServiceResult<InvoiceTransitionResult>.Fail("Lý do trả lại hóa đơn là bắt buộc.");
            }

            var trimmedReason = lyDo.Trim();
            if (trimmedReason.Length < 1 || trimmedReason.Length > 500)
            {
                return ServiceResult<InvoiceTransitionResult>.Fail("Lý do trả lại phải từ 1 đến 500 ký tự.");
            }

            var lockTargets = await _issuanceStore.GetInvoiceLockTargetsAsync(hoaDonId, ct);
            if (lockTargets == null) return ServiceResult<InvoiceTransitionResult>.Fail("Không tìm thấy hóa đơn.");

            var (meterPeriodId, branchId) = lockTargets.Value;
            var canReject = await _employeeAccessService.CanPerformAsync(actorId, branchId, EmployeeActionCodes.InvoiceReject, ct);
            if (!canReject) return ServiceResult<InvoiceTransitionResult>.Fail("Chỉ Quản trị viên mới có quyền trả lại hóa đơn.");

            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                if (meterPeriodId.HasValue)
                {
                    await _issuanceStore.LockMeterPeriodAsync(meterPeriodId.Value, ct);
                }

                var hoaDon = await _issuanceStore.GetInvoiceForUpdateAsync(hoaDonId, ct);
                if (hoaDon == null)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Không tìm thấy hóa đơn hoặc hóa đơn đã bị hủy.");
                }

                if (hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.ChoDuyet)
                {
                    await tx.RollbackAsync(ct);
                    return ServiceResult<InvoiceTransitionResult>.Fail("Chỉ có thể trả lại hóa đơn khi đang ở trạng thái chờ duyệt.");
                }

                hoaDon.TraLai(actorId, trimmedReason);
                await _unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return ServiceResult<InvoiceTransitionResult>.Ok(new InvoiceTransitionResult
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaHoaDon = hoaDon.MaHoaDon,
                    TrangThaiPhatHanh = hoaDon.TrangThaiPhatHanh
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi trả lại hóa đơn {HoaDonId}", hoaDonId);
                return ServiceResult<InvoiceTransitionResult>.Fail("Không thể trả lại hóa đơn. Vui lòng thử lại.");
            }
        }
    }
}
