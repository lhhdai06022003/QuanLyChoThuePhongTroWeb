using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardStore _store;
        private readonly DashboardSettings _settings;

        public DashboardService(IDashboardStore store, DashboardSettings settings)
        {
            _store = store;
            _settings = settings;
        }

        public async Task<DashboardDataDto> GetDashboardDataAsync(int? branchId, int selectedYear, int selectedMonth)
        {
            var today = DateTime.UtcNow.AddHours(7);
            var model = new DashboardDataDto
            {
                SelectedBranchId = branchId,
                SelectedYear = selectedYear,
                SelectedMonth = selectedMonth
            };

            // --- 1. CHUẨN BỊ DROPDOWNS ---
            var chiNhanhs = await _store.GetActiveBranchesAsync();
            model.AvailableBranches = chiNhanhs.ToList();

            for (int i = today.Year - 5; i <= today.Year; i++)
            {
                model.AvailableYears.Add(i);
            }

            // --- 2. THỐNG KÊ PHÒNG TRỌ (Có lọc chi nhánh) ---
            var (trong, daThue, baoTri) = await _store.GetRoomStatusCountsAsync(branchId);
            model.SoPhongTrong = trong;
            model.SoPhongDaThue = daThue;
            model.SoPhongBaoTri = baoTri;
            model.TongSoPhong = trong + daThue + baoTri;

            model.TyLeLapDay = model.TongSoPhong > 0
                ? Math.Round((double)model.SoPhongDaThue / model.TongSoPhong * 100, 1)
                : 0;

            // --- 3. THỐNG KÊ HỢP ĐỒNG ĐANG HOẠT ĐỘNG ---
            model.TongSoHopDongHoatDong = await _store.CountActiveContractsAsync(branchId);

            // --- 4. THỐNG KÊ HÓA ĐƠN & DOANH THU THÁNG ---
            var hoaDonStats = await _store.GetInvoiceMonthlyStatsAsync(branchId, selectedYear);

            model.DoanhThuThangNay = hoaDonStats
                .Where(h => h.Thang == selectedMonth && h.TrangThai == TrangThaiHoaDon.DaThanhToan)
                .Sum(h => h.TongTien);

            model.TongTienChoThu = hoaDonStats
                .Where(h => h.Thang == selectedMonth && h.TrangThai == TrangThaiHoaDon.ChuaThanhToan)
                .Sum(h => h.TongTien);

            model.SoPhongChuaThanhToan = await _store.CountUnpaidRoomsAsync(branchId);

            // --- 5. BIỂU ĐỒ DOANH THU 12 THÁNG (Theo năm được chọn) ---
            for (int month = 1; month <= 12; month++)
            {
                model.ChartLabels.Add($"T{month}");

                var daThu = hoaDonStats
                    .Where(h => h.Thang == month && h.TrangThai == TrangThaiHoaDon.DaThanhToan)
                    .Sum(h => h.TongTien);

                var choThu = hoaDonStats
                    .Where(h => h.Thang == month && h.TrangThai == TrangThaiHoaDon.ChuaThanhToan)
                    .Sum(h => h.TongTien);

                model.ChartData.Add(daThu);
                model.ChartDataChoThu.Add(choThu);
            }

            // --- 6. BIỂU ĐỒ TÌNH TRẠNG PHÒNG ---
            model.RoomStatusLabels.Add("Phòng trống");
            model.RoomStatusLabels.Add("Đã thuê");
            model.RoomStatusLabels.Add("Đang bảo trì");
            model.RoomStatusData.Add(model.SoPhongTrong);
            model.RoomStatusData.Add(model.SoPhongDaThue);
            model.RoomStatusData.Add(model.SoPhongBaoTri);

            // --- 7. DANH SÁCH VIỆC CẦN LÀM (To-Dos) ---
            int triggerDay = _settings.ChotDienNuocDay;

            int targetMonth = today.Month;
            int targetYear = today.Year;

            if (today.Day < triggerDay) {
                targetMonth -= 2;
            } else {
                targetMonth -= 1;
            }

            if (targetMonth < 1) {
                targetMonth += 12;
                targetYear -= 1;
            }
            if (targetMonth < 1) {
                targetMonth += 12;
                targetYear -= 1;
            }

            var checkStartDate = new DateTime(targetYear, targetMonth, 1).AddMonths(-5);

            var activeContracts = await _store.GetActiveContractsForUtilityCheckAsync(branchId);
            var recordedUtilities = await _store.GetRecordedUtilitiesAsync(checkStartDate);

            int totalMissing = 0;

            var currentCheck = checkStartDate;
            var targetDate = new DateTime(targetYear, targetMonth, 1);

            while (currentCheck <= targetDate)
            {
                int m = currentCheck.Month;
                int y = currentCheck.Year;

                var validRoomsForThisMonth = activeContracts
                    .Where(c => {
                        var localStart = c.ThoiDiemBatDau.AddHours(7);
                        return new DateTime(localStart.Year, localStart.Month, 1) <= new DateTime(y, m, 1);
                    })
                    .ToList();

                var recordedRoomsThisMonth = recordedUtilities
                    .Where(r => r.Thang == m && r.Nam == y)
                    .Select(r => r.PhongTroId)
                    .ToHashSet();

                var missingRooms = validRoomsForThisMonth
                    .Where(r => !recordedRoomsThisMonth.Contains(r.PhongTroId))
                    .ToList();

                if (missingRooms.Count > 0)
                {
                    totalMissing += missingRooms.Count;

                    var branchGroups = missingRooms.GroupBy(r => r.TenChiNhanh)
                        .Select(g => $"{g.Key}: {g.Count()} phòng")
                        .ToList();
                    var branchText = string.Join(", ", branchGroups);

                    var targetBranchId = branchId ?? missingRooms.FirstOrDefault()?.ChiNhanhId;

                    model.ToDos.Add(new DashboardToDoDto
                    {
                        Type = "warning",
                        Title = $"Chưa chốt điện nước (T{m}/{y})",
                        Description = $"Tháng {m}/{y} còn {missingRooms.Count} phòng chưa chốt ({branchText}).",
                        Link = $"/QuanLyNhaTro/ChotDienNuoc?thang={m}&nam={y}{(targetBranchId.HasValue ? $"&chiNhanhId={targetBranchId.Value}" : "")}",
                        Icon = "fas fa-bolt"
                    });
                }

                currentCheck = currentCheck.AddMonths(1);
            }

            model.SoPhongChuaChotDienNuoc = totalMissing;

            // 7b. Hợp đồng sắp hết hạn (trong vòng 30 ngày)
            var limitDate = today.AddDays(30);
            var sapHetHanCount = await _store.CountExpiringContractsAsync(branchId, today, limitDate);
            model.SoHopDongSapHetHan = sapHetHanCount;
            if (sapHetHanCount > 0)
            {
                model.ToDos.Add(new DashboardToDoDto
                {
                    Type = "danger",
                    Title = "Hợp đồng sắp hết hạn",
                    Description = $"Có {sapHetHanCount} hợp đồng sắp hết hiệu lực trong 30 ngày tới.",
                    Link = $"/QuanLyNhaTro/QuanLyHopDong{(branchId.HasValue ? $"?chiNhanhId={branchId.Value}" : "")}",
                    Icon = "fas fa-file-contract"
                });
            }

            // 7c. Hóa đơn chưa thanh toán
            var unpaidGroupedByMonthBranch = await _store.GetUnpaidInvoicesGroupedAsync(branchId);

            foreach (var item in unpaidGroupedByMonthBranch)
            {
                model.ToDos.Add(new DashboardToDoDto
                {
                    Type = "danger",
                    Title = $"Hóa đơn T{item.Thang}/{item.Nam} chưa thu ({item.TenChiNhanh})",
                    Description = $"Có {item.Count} hóa đơn tháng {item.Thang}/{item.Nam} của {item.TenChiNhanh} chưa được thanh toán.",
                    Link = $"/QuanLyNhaTro/QuanLyHoaDon?thang={item.Thang}&nam={item.Nam}&trangThai=0&chiNhanhId={item.ChiNhanhId}",
                    Icon = "fas fa-file-invoice-dollar"
                });
            }

            // --- 8. HOẠT ĐỘNG GẦN ĐÂY (Recent Activities) ---
            var recentPayments = await _store.GetRecentPaymentsAsync(branchId, 5);
            var recentContracts = await _store.GetRecentContractsAsync(branchId, 5);

            var activities = new List<(DateTime Time, string Title, string Description, string Icon, string ColorClass)>();
            foreach (var p in recentPayments)
            {
                activities.Add((p.NgayThanhToan,
                    $"Đã thu tiền Phòng {p.SoPhong}",
                    $"Số tiền: {p.SoTienThanhToan:N0} ₫ - {(p.PhuongThuc == PhuongThucThanhToan.TienMat ? "TienMat" : "ChuyenKhoan")}",
                    "fas fa-check-circle",
                    "bg-success-lt"));
            }
            foreach (var c in recentContracts)
            {
                activities.Add((c.NgayTao,
                    $"Hợp đồng mới - Phòng {c.SoPhong}",
                    $"Khách thuê: {c.HoVaTen} - Giá thuê: {c.TienThuePhong:N0} ₫",
                    "fas fa-file-signature",
                    "bg-primary-lt"));
            }

            model.RecentActivities = activities
                .OrderByDescending(a => a.Time)
                .Take(5)
                .Select(a => new DashboardRecentActivityDto
                {
                    Title = a.Title,
                    Description = a.Description,
                    TimeText = a.Time.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
                    Icon = a.Icon,
                    ColorClass = a.ColorClass
                })
                .ToList();

            return model;
        }
    }
}
