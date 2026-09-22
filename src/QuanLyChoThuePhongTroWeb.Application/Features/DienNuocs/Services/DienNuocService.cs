using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public class DienNuocService : IDienNuocService
    {
        private readonly IDienNuocStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DienNuocService> _logger;

        public DienNuocService(
            IDienNuocStore store,
            IUnitOfWork unitOfWork,
            ILogger<DienNuocService> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<DienNuocPhongRes>> GetDanhSachDienNuocAsync(int chiNhanhId, int thang, int nam)
        {
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

        public async Task<(bool IsSuccess, string? ErrorMessage)> SaveChotDienNuocAsync(ChotDienNuocReq input)
        {
            if (input == null || input.DanhSachPhong == null || !input.DanhSachPhong.Any())
            {
                return (false, "Không có dữ liệu để lưu.");
            }

            if (input.Thang < 1 || input.Thang > 12 || input.Nam < 2000)
            {
                return (false, "Thời gian chốt chỉ số điện nước không hợp lệ.");
            }

            if (input.ChiNhanhId <= 0)
            {
                return (false, "Chi nhánh không hợp lệ.");
            }

            decimal donGiaDien = await _store.GetServicePriceAsync("điện", input.ChiNhanhId);
            decimal donGiaNuoc = await _store.GetServicePriceAsync("nước", input.ChiNhanhId);

            var phongTroIds = input.DanhSachPhong.Select(x => x.PhongTroId).ToList();

            var currentRecords = await _store.GetCurrentMonthRecordsAsync(phongTroIds, input.Thang, input.Nam);

            int prevThang = input.Thang == 1 ? 12 : input.Thang - 1;
            int prevNam = input.Thang == 1 ? input.Nam - 1 : input.Nam;

            var prevRecords = await _store.GetPreviousMonthRecordsAsync(phongTroIds, prevThang, prevNam);
            var lockedPhongIds = await _store.GetLockedRoomIdsAsync(phongTroIds, input.Thang, input.Nam);
            var phongNames = await _store.GetRoomNumbersAsync(phongTroIds);

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var req in input.DanhSachPhong)
                {
                    string soPhong = phongNames.TryGetValue(req.PhongTroId, out var name) ? name : $"ID {req.PhongTroId}";

                    if (lockedPhongIds.Contains(req.PhongTroId))
                    {
                        return (false, $"Phòng {soPhong}: Không thể lưu chỉ số tháng {input.Thang}/{input.Nam} vì tháng tiếp theo đã được chốt.");
                    }

                    if (req.ChiSoDienCu < 0 || req.ChiSoDienMoi < 0 || req.ChiSoNuocCu < 0 || req.ChiSoNuocMoi < 0)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số điện/nước không được phép âm.");
                    }

                    if (req.ChiSoDienMoi < req.ChiSoDienCu)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                    }
                    if (req.ChiSoNuocMoi < req.ChiSoNuocCu)
                    {
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
                        return (false, $"Phòng {soPhong}: Chỉ số điện đầu kỳ ({req.ChiSoDienCu}) không khớp với chỉ số cuối kỳ trước ({actualPrevDienMoi}).");
                    }
                    if (req.ChiSoNuocCu != actualPrevNuocMoi)
                    {
                        return (false, $"Phòng {soPhong}: Chỉ số nước đầu kỳ ({req.ChiSoNuocCu}) không khớp với chỉ số cuối kỳ trước ({actualPrevNuocMoi}).");
                    }

                    if (currentRecords.TryGetValue(req.PhongTroId, out var record))
                    {
                        record.ChiSoDienCu = req.ChiSoDienCu;
                        record.ChiSoDienMoi = req.ChiSoDienMoi;
                        record.ChiSoNuocCu = req.ChiSoNuocCu;
                        record.ChiSoNuocMoi = req.ChiSoNuocMoi;
                        record.DonGiaDien = donGiaDien;
                        record.DonGiaNuoc = donGiaNuoc;
                        record.NgayCapNhat = DateTime.UtcNow;
                        _store.UpdateRecord(record);
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
