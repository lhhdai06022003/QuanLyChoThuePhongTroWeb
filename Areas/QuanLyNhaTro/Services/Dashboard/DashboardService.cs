using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public DashboardService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(int? branchId, int selectedYear, int selectedMonth)
        {
            var today = DateTime.UtcNow.AddHours(7);
            var model = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                SelectedYear = selectedYear,
                SelectedMonth = selectedMonth
            };

            // --- 1. CHUẨN BỊ DROPDOWNS ---
            var chiNhanhs = await _context.ChiNhanhs.Where(c => !c.IsDeleted).ToListAsync();
            var branchesList = chiNhanhs.Select(c => new
            {
                Id = c.ChiNhanhId,
                Ten = c.TenChiNhanh
            }).ToList();
            model.Branches = new SelectList(branchesList, "Id", "Ten", branchId);

            var yearsList = new List<int>();
            for (int i = today.Year - 5; i <= today.Year; i++)
            {
                yearsList.Add(i);
            }
            model.Years = new SelectList(yearsList, selectedYear);

            var monthsList = new List<SelectListItem>();
            for (int m = 1; m <= 12; m++)
            {
                monthsList.Add(new SelectListItem
                {
                    Value = m.ToString(),
                    Text = $"Tháng {m}",
                    Selected = (m == selectedMonth)
                });
            }
            model.Months = monthsList;

            // --- 2. THỐNG KÊ PHÒNG TRỌ (Có lọc chi nhánh) ---
            var phongTrosQuery = _context.PhongTros.Where(p => !p.IsDeleted);
            if (branchId.HasValue)
            {
                phongTrosQuery = phongTrosQuery.Where(p => p.ChiNhanhId == branchId.Value);
            }
            
            var phongStats = await phongTrosQuery
                .GroupBy(p => p.TrangThai)
                .Select(g => new { TrangThai = g.Key, Count = g.Count() })
                .ToListAsync();

            model.SoPhongTrong = phongStats.FirstOrDefault(s => s.TrangThai == TrangThaiPhong.Trong)?.Count ?? 0;
            model.SoPhongDaThue = phongStats.FirstOrDefault(s => s.TrangThai == TrangThaiPhong.DaThue)?.Count ?? 0;
            model.SoPhongBaoTri = phongStats.FirstOrDefault(s => s.TrangThai == TrangThaiPhong.BaoTri)?.Count ?? 0;
            model.TongSoPhong = model.SoPhongTrong + model.SoPhongDaThue + model.SoPhongBaoTri;

            // Tỷ lệ lấp đầy
            model.TyLeLapDay = model.TongSoPhong > 0
                ? Math.Round((double)model.SoPhongDaThue / model.TongSoPhong * 100, 1)
                : 0;
            // --- 3. THỐNG KÊ HỢP ĐỒNG ĐANG HOẠT ĐỘNG ---
            var hopDongsQuery = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);
            if (branchId.HasValue)
            {
                hopDongsQuery = hopDongsQuery.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }
            model.TongSoHopDongHoatDong = await hopDongsQuery.CountAsync();

            // --- 4. THỐNG KÊ HÓA ĐƠN & DOANH THU THÁNG ---
            var hoaDonsQuery = _context.HoaDons
                .Where(h => !h.IsDeleted && h.Nam == selectedYear);
            if (branchId.HasValue)
            {
                hoaDonsQuery = hoaDonsQuery.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            var hoaDonStats = await hoaDonsQuery
                .GroupBy(h => new { h.Thang, h.TrangThaiHoaDon })
                .Select(g => new
                {
                    Thang = g.Key.Thang,
                    TrangThai = g.Key.TrangThaiHoaDon,
                    TongTien = g.Sum(h => h.TongTien)
                })
                .ToListAsync();

            model.DoanhThuThangNay = hoaDonStats
                .Where(h => h.Thang == selectedMonth && h.TrangThai == TrangThaiHoaDon.DaThanhToan)
                .Sum(h => h.TongTien);

            model.TongTienChoThu = hoaDonStats
                .Where(h => h.Thang == selectedMonth && h.TrangThai == TrangThaiHoaDon.ChuaThanhToan)
                .Sum(h => h.TongTien);

            // [THAY ĐỔI YÊU CẦU 1]: Đếm tổng số phòng DUY NHẤT chưa thanh toán từ trước tới nay (Xuyên suốt lịch sử)
            var allUnpaidBillsQuery = _context.HoaDons
                .Where(h => !h.IsDeleted && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan);

            if (branchId.HasValue)
            {
                allUnpaidBillsQuery = allUnpaidBillsQuery.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            model.SoPhongChuaThanhToan = await allUnpaidBillsQuery
                .Select(h => h.HopDong.PhongTroId)
                .Distinct() // Loại bỏ trùng lặp phòng (Phòng nợ nhiều tháng chỉ tính là 1 phòng)
                .CountAsync();


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
            // 7a. Cảnh báo phòng chưa chốt điện nước (Toàn cục - Các tháng trước)
            int triggerDay = _configuration.GetValue<int>("DashboardSettings:ChotDienNuocDay", 5);
            
            // Xác định tháng/năm tối đa cần phải chốt
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
            if (targetMonth < 1) { // Trường hợp trừ 2 bị âm
                targetMonth += 12;
                targetYear -= 1;
            }

            // Chỉ kiểm tra trong 6 tháng gần nhất để tối ưu hiệu năng
            var checkStartDate = new DateTime(targetYear, targetMonth, 1).AddMonths(-5);

            var activeContractsQuery = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);
            if (branchId.HasValue)
            {
                activeContractsQuery = activeContractsQuery.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }
            var activeContracts = await activeContractsQuery
                .Select(h => new { h.PhongTroId, h.ThoiDiemBatDau, ChiNhanhId = h.PhongTro.ChiNhanhId, TenChiNhanh = h.PhongTro.ChiNhanh.TenChiNhanh })
                .ToListAsync();

            var recordedUtilities = await _context.DichVuDienNuocCuaPhongs
                .Where(d => !d.IsDeleted && 
                       (d.Nam > checkStartDate.Year || (d.Nam == checkStartDate.Year && d.Thang >= checkStartDate.Month)))
                .Select(d => new { d.PhongTroId, d.Thang, d.Nam })
                .ToListAsync();

            int totalMissing = 0;

            // Kiểm tra từng tháng từ checkStartDate đến targetMonth
            var currentCheck = checkStartDate;
            var targetDate = new DateTime(targetYear, targetMonth, 1);

            while (currentCheck <= targetDate)
            {
                int m = currentCheck.Month;
                int y = currentCheck.Year;

                // Các phòng đang thuê và đã bắt đầu thuê từ tháng này hoặc trước đó (Cộng 7 tiếng để fix lỗi múi giờ khi so sánh)
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

                    model.ToDos.Add(new ToDoItemViewModel
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

            model.SoPhongChuaChotDienNuoc = totalMissing; // Hiển thị tổng số lượng cảnh báo lên Badge nếu có

            // 7b. Hợp đồng sắp hết hạn (trong vòng 30 ngày)
            var limitDate = today.AddDays(30);
            var sapHetHanQuery = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && h.ThoiDiemKetThuc.HasValue && h.ThoiDiemKetThuc.Value <= limitDate && h.ThoiDiemKetThuc.Value >= today);
            if (branchId.HasValue)
            {
                sapHetHanQuery = sapHetHanQuery.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }
            var sapHetHanCount = await sapHetHanQuery.CountAsync();
            model.SoHopDongSapHetHan = sapHetHanCount;
            if (sapHetHanCount > 0)
            {
                model.ToDos.Add(new ToDoItemViewModel
                {
                    Type = "danger",
                    Title = "Hợp đồng sắp hết hạn",
                    Description = $"Có {sapHetHanCount} hợp đồng sắp hết hiệu lực trong 30 ngày tới.",
                    Link = $"/QuanLyNhaTro/QuanLyHopDong{(branchId.HasValue ? $"?chiNhanhId={branchId.Value}" : "")}",
                    Icon = "fas fa-file-contract"
                });
            }

            // [THAY ĐỔI YÊU CẦU 2]: 7c. Hóa đơn chưa thanh toán gom theo từng PHÒNG từ trước tới nay
            var unpaidQuery = _context.HoaDons
                .Where(h => !h.IsDeleted && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan);

            if (branchId.HasValue)
            {
                unpaidQuery = unpaidQuery.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            // Group theo PhongTroId và SoPhong để thống kê số tháng nợ tổng quát
            var unpaidGroupedByRoom = await unpaidQuery
                .GroupBy(h => new { h.HopDong.PhongTroId, h.HopDong.PhongTro.SoPhong })
                .Select(g => new
                {
                    PhongTroId = g.Key.PhongTroId,
                    SoPhong = g.Key.SoPhong,
                    UnpaidMonthsCount = g.Count() // Số lượng hóa đơn chưa trả = số tháng nợ
                })
                .OrderByDescending(g => g.UnpaidMonthsCount) // Ưu tiên đưa phòng nợ dai nhất lên đầu
                .ToListAsync();

            foreach (var item in unpaidGroupedByRoom)
            {
                model.ToDos.Add(new ToDoItemViewModel
                {
                    Type = "danger",
                    Title = $"Hóa đơn Phòng {item.SoPhong} chưa thu",
                    Description = $"Phòng {item.SoPhong} chưa thanh toán hóa đơn (Nợ {item.UnpaidMonthsCount} tháng).",
                    Link = $"/QuanLyNhaTro/QuanLyHoaDon?search={item.SoPhong}&trangThai=0" + (branchId.HasValue ? $"&chiNhanhId={branchId.Value}" : ""),
                    Icon = "fas fa-exclamation-circle"
                });
            }

            // --- 8. HOẠT ĐỘNG GẦN ĐÂY (Recent Activities) ---
            var recentPaymentsQuery = _context.LichSuThanhToans
                .Include(l => l.HoaDon)
                    .ThenInclude(h => h.HopDong)
                        .ThenInclude(hd => hd.PhongTro)
                .Where(l => !l.IsDeleted);
            if (branchId.HasValue)
            {
                recentPaymentsQuery = recentPaymentsQuery.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }
            var recentPayments = await recentPaymentsQuery.OrderByDescending(l => l.NgayThanhToan).Take(5).ToListAsync();

            var recentContractsQuery = _context.HopDongs
.Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => !h.IsDeleted);
            if (branchId.HasValue)
            {
                recentContractsQuery = recentContractsQuery.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }
            var recentContracts = await recentContractsQuery.OrderByDescending(h => h.NgayTao).Take(5).ToListAsync();

            var activities = new List<(DateTime Time, string Title, string Description, string Icon, string ColorClass)>();
            foreach (var p in recentPayments)
            {
                activities.Add((p.NgayThanhToan,
                    $"Đã thu tiền Phòng {p.HoaDon.HopDong.PhongTro.SoPhong}",
                    $"Số tiền: {p.SoTienThanhToan:N0} ₫ - {(p.PhuongThucThanhToan == PhuongThucThanhToan.TienMat ? "TienMat" : "ChuyenKhoan")}",
                    "fas fa-check-circle",
                    "bg-success-lt"));
            }
            foreach (var c in recentContracts)
            {
                activities.Add((c.NgayTao,
                    $"Hợp đồng mới - Phòng {c.PhongTro.SoPhong}",
                    $"Khách thuê: {c.NguoiThue.HoVaTen} - Giá thuê: {c.TienThuePhong:N0} ₫",
                    "fas fa-file-signature",
                    "bg-primary-lt"));
            }

            model.RecentActivities = activities
                .OrderByDescending(a => a.Time)
                .Take(5)
                .Select(a => new RecentActivityViewModel
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