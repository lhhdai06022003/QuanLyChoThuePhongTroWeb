using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public class DienNuocService : IDienNuocService
    {
        private readonly IDienNuocStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly ILogger<DienNuocService> _logger;
        private readonly IHoaDonStore _hoaDonStore;
        private readonly IHoaDonCalculatorService _calculatorService;

        public DienNuocService(
            IDienNuocStore store,
            IUnitOfWork unitOfWork,
            IEmployeeAccessService employeeAccessService,
            ILogger<DienNuocService> logger,
            IHoaDonStore hoaDonStore,
            IHoaDonCalculatorService calculatorService)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _employeeAccessService = employeeAccessService;
            _logger = logger;
            _hoaDonStore = hoaDonStore;
            _calculatorService = calculatorService;
        }

        public async Task<List<DienNuocPhongRes>> GetDanhSachDienNuocAsync(int chiNhanhId, int thang, int nam, int actorId)
        {
            if (actorId <= 0 ||
                !await _employeeAccessService.CanPerformAsync(actorId, chiNhanhId, EmployeeActionCodes.MeterRead))
            {
                return new List<DienNuocPhongRes>();
            }

            var startOfMonth = new DateTime(nam, thang, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

            var hopDongsActive = await _store.GetActiveContractsInBranchAsync(chiNhanhId, startOfMonth, endOfMonth);

            if (!hopDongsActive.Any()) return new List<DienNuocPhongRes>();

            var phongĐangThue = hopDongsActive
                .GroupBy(h => h.PhongTroId)
                .Select(g => g.OrderByDescending(h => h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                              .ThenByDescending(h => h.ThoiDiemBatDau)
                              .First())
                .ToList();

            var phongTroIds = phongĐangThue.Select(h => h.PhongTroId).ToList();

            var currentRecords = await _store.GetCurrentMonthRecordsAsync(phongTroIds, thang, nam);

            int prevThang = thang == 1 ? 12 : thang - 1;
            int prevNam = thang == 1 ? nam - 1 : nam;

            var prevRecords = await _store.GetPreviousMonthRecordsAsync(phongTroIds, prevThang, prevNam);
            var lockedPhongIds = await _store.GetLockedRoomIdsAsync(phongTroIds, thang, nam);

            var result = new List<DienNuocPhongRes>();

            foreach (var hd in phongĐangThue)
            {
                var phongTroId = hd.PhongTroId;
                bool isLocked = lockedPhongIds.Contains(phongTroId);

                if (currentRecords.TryGetValue(phongTroId, out var currentRecord))
                {
                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = currentRecord.DichVuDienNuocCuaPhongId,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = currentRecord.ChiSoDienCu,
                        ChiSoDienMoi = currentRecord.ChiSoDienMoi,
                        ChiSoNuocCu = currentRecord.ChiSoNuocCu,
                        ChiSoNuocMoi = currentRecord.ChiSoNuocMoi,
                        IsDaChot = true,
                        IsLocked = isLocked
                    });
                }
                else
                {
                    decimal dienCu = 0m;
                    decimal nuocCu = 0m;
                    if (prevRecords.TryGetValue(phongTroId, out var prevRecord))
                    {
                        dienCu = prevRecord.ChiSoDienMoi;
                        nuocCu = prevRecord.ChiSoNuocMoi;
                    }
                    else
                    {
                        var ganNhat = await _store.GetNearestPreviousReadingAsync(phongTroId, thang, nam);
                        dienCu = ganNhat.ChiSoDienMoi;
                        nuocCu = ganNhat.ChiSoNuocMoi;
                    }

                    result.Add(new DienNuocPhongRes
                    {
                        DichVuDienNuocCuaPhongId = 0,
                        PhongTroId = phongTroId,
                        TenPhong = hd.PhongTro.SoPhong,
                        TenNguoiDaiDien = hd.NguoiThue.HoVaTen,
                        ChiSoDienCu = dienCu,
                        ChiSoDienMoi = 0m,
                        ChiSoNuocCu = nuocCu,
                        ChiSoNuocMoi = 0m,
                        IsDaChot = false,
                        IsLocked = isLocked
                    });
                }
            }

            return result.OrderBy(x => x.TenPhong).ToList();
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> SaveChotDienNuocAsync(ChotDienNuocReq input, int actorId = 0)
        {
            if (input == null || input.DanhSachPhong == null || !input.DanhSachPhong.Any())
            {
                return (false, "Không có dữ liệu để lưu.");
            }

            if (input.Thang < 1 || input.Thang > 12 || input.Nam < 2000)
            {
                return (false, "Thời gian chốt chỉ số điện nước không hợp lệ.");
            }

            if (actorId <= 0)
            {
                return (false, "Người thực hiện không hợp lệ.");
            }

            var phongTroIds = input.DanhSachPhong.Select(x => x.PhongTroId).Distinct().ToList();
            var roomBranches = await _store.GetRoomBranchIdsAsync(phongTroIds);
            if (roomBranches.Count != phongTroIds.Count)
            {
                return (false, "Một hoặc nhiều phòng trọ không tồn tại hoặc đã bị xóa.");
            }

            var distinctBranchIds = roomBranches.Values.Distinct().ToList();
            if (distinctBranchIds.Count > 1)
            {
                return (false, "Dữ liệu chốt điện nước không hợp lệ: các phòng trọ thuộc nhiều chi nhánh khác nhau.");
            }

            var actualBranchId = distinctBranchIds.First();
            if (input.ChiNhanhId != actualBranchId)
            {
                return (false, "Chi nhánh yêu cầu không khớp với chi nhánh thực tế của các phòng trọ.");
            }

            var canPerform = await _employeeAccessService.CanPerformAsync(actorId, actualBranchId, EmployeeActionCodes.MeterReview);
            if (!canPerform)
            {
                return (false, "Bạn không có quyền chốt chỉ số điện nước tại chi nhánh này.");
            }

            decimal donGiaDien = await _store.GetServicePriceAsync("điện", actualBranchId);
            decimal donGiaNuoc = await _store.GetServicePriceAsync("nước", actualBranchId);

            var currentRecords = await _store.GetCurrentMonthRecordsAsync(phongTroIds, input.Thang, input.Nam);

            int prevThang = input.Thang == 1 ? 12 : input.Thang - 1;
            int prevNam = input.Thang == 1 ? input.Nam - 1 : input.Nam;

            var prevRecords = await _store.GetPreviousMonthRecordsAsync(phongTroIds, prevThang, prevNam);
            var lockedPhongIds = await _store.GetLockedRoomIdsAsync(phongTroIds, input.Thang, input.Nam);
            var phongNames = await _store.GetRoomNumbersAsync(phongTroIds);

            var readingIds = currentRecords.Values
                .Select(r => r.DichVuDienNuocCuaPhongId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            var linkedInvoices = readingIds.Count > 0
                ? await _hoaDonStore.GetInvoicesByMeterReadingIdsAsync(readingIds)
                : (IReadOnlyList<HoaDon>)Array.Empty<HoaDon>();

            var invoicesByReadingId = linkedInvoices
                .Where(h => h.DichVuDienNuocCuaPhongId.HasValue)
                .GroupBy(h => h.DichVuDienNuocCuaPhongId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            try
            {
                foreach (var req in input.DanhSachPhong)
                {
                    string soPhong = phongNames.TryGetValue(req.PhongTroId, out var name) ? name : $"ID {req.PhongTroId}";

                    if (lockedPhongIds.Contains(req.PhongTroId))
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Phòng {soPhong}: Không thể lưu chỉ số tháng {input.Thang}/{input.Nam} vì tháng tiếp theo đã được chốt.");
                    }

                    if (req.ChiSoDienCu < 0 || req.ChiSoDienMoi < 0 || req.ChiSoNuocCu < 0 || req.ChiSoNuocMoi < 0)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Phòng {soPhong}: Chỉ số điện/nước không được phép âm.");
                    }

                    if (req.ChiSoDienMoi < req.ChiSoDienCu)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Phòng {soPhong}: Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                    }
                    if (req.ChiSoNuocMoi < req.ChiSoNuocCu)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Phòng {soPhong}: Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
                    }

                    decimal actualPrevDienMoi = 0m;
                    decimal actualPrevNuocMoi = 0m;
                    if (prevRecords.TryGetValue(req.PhongTroId, out var prevRecord))
                    {
                        actualPrevDienMoi = prevRecord.ChiSoDienMoi;
                        actualPrevNuocMoi = prevRecord.ChiSoNuocMoi;
                    }
                    else
                    {
                        var ganNhat = await _store.GetNearestPreviousReadingAsync(req.PhongTroId, input.Thang, input.Nam);
                        actualPrevDienMoi = ganNhat.ChiSoDienMoi;
                        actualPrevNuocMoi = ganNhat.ChiSoNuocMoi;
                    }

                    if (req.ChiSoDienCu != actualPrevDienMoi)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Phòng {soPhong}: Chỉ số điện đầu kỳ ({req.ChiSoDienCu}) không khớp với chỉ số cuối kỳ trước ({actualPrevDienMoi}).");
                    }
                    if (req.ChiSoNuocCu != actualPrevNuocMoi)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Phòng {soPhong}: Chỉ số nước đầu kỳ ({req.ChiSoNuocCu}) không khớp với chỉ số cuối kỳ trước ({actualPrevNuocMoi}).");
                    }

                    if (currentRecords.TryGetValue(req.PhongTroId, out var record))
                    {
                        if (invoicesByReadingId.TryGetValue(record.DichVuDienNuocCuaPhongId, out var invoicesForReading))
                        {
                            if (invoicesForReading.Any(x => x.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap))
                            {
                                await transaction.RollbackAsync();
                                return (false, $"Phòng {soPhong}: Hóa đơn của kỳ đã rời trạng thái nháp nên chỉ số đã bị khóa.");
                            }
                        }

                        record.CapNhatChiSo(req.ChiSoDienMoi, req.ChiSoNuocMoi);
                        record.DonGiaDien = donGiaDien;
                        record.DonGiaNuoc = donGiaNuoc;
                        _store.UpdateRecord(record);

                        if (invoicesByReadingId.TryGetValue(record.DichVuDienNuocCuaPhongId, out invoicesForReading))
                        {
                            int daysInMonth = DateTime.DaysInMonth(input.Nam, input.Thang);
                            foreach (var inv in invoicesForReading)
                            {
                                int soNgayO = daysInMonth;
                                if (inv.HopDong != null)
                                {
                                    var tienPhong = _calculatorService.TinhTienPhong(
                                        inv.HopDong.TienThuePhong,
                                        inv.HopDong.ThoiDiemBatDau,
                                        inv.HopDong.ThoiDiemKetThuc,
                                        input.Thang,
                                        input.Nam);
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
                                _hoaDonStore.UpdateHoaDon(inv);
                            }
                        }
                    }
                    else
                    {
                        var newRecord = new DichVuDienNuocCuaPhong
                        {
                            PhongTroId = req.PhongTroId,
                            Thang = input.Thang,
                            Nam = input.Nam,
                            ChiSoDienCu = req.ChiSoDienCu,
                            ChiSoDienMoi = req.ChiSoDienMoi,
                            DonGiaDien = donGiaDien,
                            ChiSoNuocCu = req.ChiSoNuocCu,
                            ChiSoNuocMoi = req.ChiSoNuocMoi,
                            DonGiaNuoc = donGiaNuoc,
                            NgayTao = DateTime.UtcNow
                        };
                        await _store.AddRecordAsync(newRecord);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi hệ thống khi lưu chốt điện nước.");
                return (false, "Lỗi hệ thống khi lưu chốt điện nước.");
            }
        }
    }
}
