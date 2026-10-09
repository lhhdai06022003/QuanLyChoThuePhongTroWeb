using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardStore _store;
        private readonly IEmployeeAccessService _access;
        private readonly TimeProvider _timeProvider;

        public DashboardService(IDashboardStore store, IEmployeeAccessService access, TimeProvider timeProvider)
        {
            _store = store;
            _access = access;
            _timeProvider = timeProvider;
        }

        public async Task<DashboardDataDto> GetDashboardDataAsync(int actorId, int? branchId, int selectedYear, int selectedMonth)
        {
            // Admin: null (không giới hạn). Nhân viên: chi nhánh được phân công. Actor không hợp lệ hoặc chọn
            // chi nhánh ngoài phạm vi: danh sách rỗng, mọi truy vấn trả 0 nhưng biểu đồ vẫn đủ cấu trúc.
            var scope = await _access.GetScopeAsync(actorId);
            IReadOnlyCollection<int>? branchFilter = scope == null ? Array.Empty<int>() : scope.AllowedBranchIds;
            IReadOnlyCollection<int>? allowed = scope == null || (branchId.HasValue && !scope.CanAccessBranch(branchId.Value))
                ? Array.Empty<int>()
                : scope.AllowedBranchIds;

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var today = nowUtc.AddHours(7);
            var model = new DashboardDataDto
            {
                SelectedBranchId = branchId,
                SelectedYear = selectedYear,
                SelectedMonth = selectedMonth
            };

            // --- 1. CHUẨN BỊ DROPDOWNS ---
            var chiNhanhs = await _store.GetActiveBranchesAsync(branchFilter);
            model.AvailableBranches = chiNhanhs.ToList();

            for (int i = today.Year - 5; i <= today.Year; i++)
            {
                model.AvailableYears.Add(i);
            }

            // --- 2. THỐNG KÊ PHÒNG TRỌ (Có lọc chi nhánh) ---
            var (trong, daThue, baoTri) = await _store.GetRoomStatusCountsAsync(branchId, allowedBranchIds: allowed);
            model.SoPhongTrong = trong;
            model.SoPhongDaThue = daThue;
            model.SoPhongBaoTri = baoTri;
            model.TongSoPhong = trong + daThue + baoTri;

            model.TyLeLapDay = model.TongSoPhong > 0
                ? Math.Round((double)model.SoPhongDaThue / model.TongSoPhong * 100, 1)
                : 0;

            // --- 3. THỐNG KÊ HỢP ĐỒNG ĐANG HOẠT ĐỘNG ---
            model.TongSoHopDongHoatDong = await _store.CountActiveContractsAsync(branchId, allowedBranchIds: allowed);

            // --- 4. THỐNG KÊ HÓA ĐƠN & DOANH THU THÁNG ---
            var hoaDonStats = await _store.GetInvoiceMonthlyStatsAsync(branchId, selectedYear, allowedBranchIds: allowed);

            // Đã thu tính theo ledger để hóa đơn trả một phần được tính đúng phần đã nhận và phần còn nợ.
            model.DoanhThuThangNay = hoaDonStats
                .Where(h => h.Thang == selectedMonth)
                .Sum(h => h.DaThu);

            // Chờ thu chỉ tính hóa đơn đã gửi khách (khớp màn Công nợ).
            model.TongTienChoThu = hoaDonStats
                .Where(h => h.Thang == selectedMonth)
                .Sum(h => h.ChoThu);

            model.SoPhongChuaThanhToan = await _store.CountUnpaidRoomsAsync(branchId, allowedBranchIds: allowed);

            var quaHan = await _store.GetOverdueSummaryAsync(branchId, nowUtc, allowedBranchIds: allowed);
            model.TienQuaHan = quaHan.TongConNo;
            model.SoHoaDonQuaHan = quaHan.SoHoaDon;

            // --- 5. BIỂU ĐỒ DOANH THU 12 THÁNG (Theo năm được chọn) ---
            for (int month = 1; month <= 12; month++)
            {
                model.ChartLabels.Add($"T{month}");

                var daThu = hoaDonStats
                    .Where(h => h.Thang == month)
                    .Sum(h => h.DaThu);

                var choThu = hoaDonStats
                    .Where(h => h.Thang == month)
                    .Sum(h => h.ChoThu);

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

            // --- 7. HOẠT ĐỘNG GẦN ĐÂY (Recent Activities) ---
            var recentPayments = await _store.GetRecentPaymentsAsync(branchId, 5, allowedBranchIds: allowed);
            var recentContracts = await _store.GetRecentContractsAsync(branchId, 5, allowedBranchIds: allowed);

            var activities = new List<(DateTime Time, string Title, string Description, string Icon, string ColorClass)>();
            foreach (var p in recentPayments)
            {
                activities.Add((p.NgayThanhToan,
                    $"Đã thu tiền Phòng {p.SoPhong}",
                    $"Số tiền: {TienTe.DinhDang(p.SoTienThanhToan)} - {(p.PhuongThuc == PhuongThucThanhToan.TienMat ? "Tiền mặt" : "Chuyển khoản")}",
                    "fas fa-check-circle",
                    "bg-success-lt"));
            }
            foreach (var c in recentContracts)
            {
                activities.Add((c.NgayTao,
                    $"Hợp đồng mới - Phòng {c.SoPhong}",
                    $"Khách thuê: {c.HoVaTen} - Giá thuê: {TienTe.DinhDang(c.TienThuePhong)}",
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
