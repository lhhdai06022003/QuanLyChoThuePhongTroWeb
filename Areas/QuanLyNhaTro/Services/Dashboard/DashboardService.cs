using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
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

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(int? branchId, int selectedYear, int selectedMonth)
        {
            var today = DateTime.Now;
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
            var phongTros = await phongTrosQuery.ToListAsync();
            model.TongSoPhong = phongTros.Count;
            model.SoPhongTrong = phongTros.Count(p => p.TrangThai == TrangThaiPhong.Trong);
            model.SoPhongDaThue = phongTros.Count(p => p.TrangThai == TrangThaiPhong.DaThue);
            model.SoPhongBaoTri = phongTros.Count(p => p.TrangThai == TrangThaiPhong.BaoTri);

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

            var hoaDonsTheoNam = await hoaDonsQuery.ToListAsync();
            var hoaDonThangNay = hoaDonsTheoNam.Where(h => h.Thang == selectedMonth).ToList();

            model.DoanhThuThangNay = hoaDonThangNay
                .Where(h => h.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                .Sum(h => h.TongTien);

            model.TongTienChoThu = hoaDonThangNay
                .Where(h => h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan)
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

                var daThu = hoaDonsTheoNam
                    .Where(h => h.Thang == month && h.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                    .Sum(h => h.TongTien);

                var choThu = hoaDonsTheoNam
                    .Where(h => h.Thang == month && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan)
                    .Sum(h => h.TongTien);

                model.ChartData.Add(daThu);
                model.ChartDataChoThu.Add(choThu);
            }

            // --- 6. BIỂU ĐỒ PHƯƠNG THỨC THANH TOÁN (Của tháng được chọn) ---
            var thanhToansQuery = _context.LichSuThanhToans
                .Include(l => l.HoaDon)
                    .ThenInclude(h => h.HopDong)
.ThenInclude(hd => hd.PhongTro)
                .Where(l => !l.IsDeleted && l.NgayThanhToan.Year == selectedYear && l.NgayThanhToan.Month == selectedMonth);
            if (branchId.HasValue)
            {
                thanhToansQuery = thanhToansQuery.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }
            var thanhToans = await thanhToansQuery.ToListAsync();
            var cashTotal = thanhToans.Where(l => l.PhuongThucThanhToan == PhuongThucThanhToan.TienMat).Sum(l => l.SoTienThanhToan);
            var bankTotal = thanhToans.Where(l => l.PhuongThucThanhToan == PhuongThucThanhToan.ChuyenKhoan).Sum(l => l.SoTienThanhToan);

            model.PaymentMethodLabels.Add("Tiền mặt");
            model.PaymentMethodLabels.Add("Chuyển khoản");
            model.PaymentMethodData.Add(cashTotal);
            model.PaymentMethodData.Add(bankTotal);

            // --- 7. DANH SÁCH VIỆC CẦN LÀM (To-Dos) ---
            // 7a. Cảnh báo phòng chưa chốt điện nước
            var chotDienNuocs = await _context.DichVuDienNuocCuaPhongs
                .Where(d => !d.IsDeleted && d.Thang == selectedMonth && d.Nam == selectedYear)
                .Select(d => d.PhongTroId)
                .ToListAsync();

            var activePhongIdsQuery = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);
            if (branchId.HasValue)
            {
                activePhongIdsQuery = activePhongIdsQuery.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }
            var activePhongIds = await activePhongIdsQuery.Select(h => h.PhongTroId).ToListAsync();

            var needingRecordCount = activePhongIds.Except(chotDienNuocs).Count();
            model.SoPhongChuaChotDienNuoc = needingRecordCount;
            if (needingRecordCount > 0)
            {
                model.ToDos.Add(new ToDoItemViewModel
                {
                    Type = "warning",
                    Title = "Chưa chốt chỉ số điện nước",
                    Description = $"Có {needingRecordCount} phòng chưa chốt chỉ số điện nước Tháng {selectedMonth}/{selectedYear}.",
                    Link = "/QuanLyNhaTro/ChotDienNuoc",
                    Icon = "fas fa-bolt"
                });
            }

            // 7b. Hợp đồng sắp hết hạn (trong vòng 30 ngày)
            var limitDate = DateTime.UtcNow.AddDays(30);
            var sapHetHanQuery = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && h.ThoiDiemKetThuc.HasValue && h.ThoiDiemKetThuc.Value <= limitDate && h.ThoiDiemKetThuc.Value >= DateTime.UtcNow);
            if (branchId.HasValue)
            {
                sapHetHanQuery = sapHetHanQuery.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }
            var sapHetHanList = await sapHetHanQuery.ToListAsync();
            model.SoHopDongSapHetHan = sapHetHanList.Count;
            if (sapHetHanList.Count > 0)
            {
                model.ToDos.Add(new ToDoItemViewModel
                {
                    Type = "danger",
                    Title = "Hợp đồng sắp hết hạn",
                    Description = $"Có {sapHetHanList.Count} hợp đồng sắp hết hiệu lực trong 30 ngày tới.",
                    Link = "/QuanLyNhaTro/HopDong",
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
                    Link = $"/QuanLyNhaTro/QuanLyHoaDon?phongTroId={item.PhongTroId}&trangThai=0" + (branchId.HasValue ? $"&chiNhanhId={branchId.Value}" : ""),
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