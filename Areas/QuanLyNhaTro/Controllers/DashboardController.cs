using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers
{
    [Area("QuanLyNhaTro")]
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int? branchId, int? year)
        {
            var today = DateTime.Now;
            int currentMonth = today.Month;

            // Nếu không chọn năm, mặc định lấy năm hiện tại
            int selectedYear = year ?? today.Year;

            var model = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                SelectedYear = selectedYear
            };

            // --- 1. CHUẨN BỊ DỮ LIỆU CHO DROPDOWN LỌC ---
            var chiNhanhs = _context.ChiNhanhs.Where(c => !c.IsDeleted).ToList();

            // ĐÃ SỬA: Chuyển c.Id thành c.ChiNhanhId cho đúng với database của bạn
            var danhSachChiNhanhDaChuanHoa = chiNhanhs.Select(c => new
            {
                Id = c.ChiNhanhId,
                Ten = c.TenChiNhanh
            }).ToList();

            model.Branches = new SelectList(danhSachChiNhanhDaChuanHoa, "Id", "Ten", branchId);

            // Tạo danh sách năm (từ 5 năm trước đến năm hiện tại)
            var yearsList = new List<int>();
            for (int i = today.Year - 5; i <= today.Year; i++)
            {
                yearsList.Add(i);
            }
            model.Years = new SelectList(yearsList, selectedYear);

            // --- 2. THỐNG KÊ PHÒNG TRỌ (Có lọc chi nhánh) ---
            var phongTrosQuery = _context.PhongTros.Where(p => !p.IsDeleted);
            if (branchId.HasValue)
            {
                phongTrosQuery = phongTrosQuery.Where(p => p.ChiNhanhId == branchId.Value);
            }

            var phongTros = phongTrosQuery.ToList();
            model.TongSoPhong = phongTros.Count;
            model.SoPhongTrong = phongTros.Count(p => p.TrangThai == TrangThaiPhong.Trong);
            model.SoPhongDaThue = phongTros.Count(p => p.TrangThai == TrangThaiPhong.DaThue);
            model.SoPhongBaoTri = phongTros.Count(p => p.TrangThai == TrangThaiPhong.BaoTri);

            // --- 3. THỐNG KÊ HỢP ĐỒNG ĐANG HOẠT ĐỘNG ---
            var hopDongsQuery = _context.HopDongs
                .Where(h => !h.IsDeleted && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong);
            if (branchId.HasValue)
            {
                hopDongsQuery = hopDongsQuery.Where(h => h.PhongTro.ChiNhanhId == branchId.Value);
            }
            model.TongSoHopDongHoatDong = hopDongsQuery.Count();

            // --- 4. THỐNG KÊ HÓA ĐƠN & DOANH THU ---
            var hoaDonsQuery = _context.HoaDons
                .Where(h => !h.IsDeleted && h.Nam == selectedYear);
            if (branchId.HasValue)
            {
                hoaDonsQuery = hoaDonsQuery.Where(h => h.HopDong.PhongTro.ChiNhanhId == branchId.Value);
            }

            // Tải dữ liệu hóa đơn của năm được chọn ra memory để tính toán cho nhanh
            var hoaDonsTheoNam = hoaDonsQuery.ToList();

            // Lấy riêng tháng hiện tại (của năm được chọn) để tính KPI
            var hoaDonThangNay = hoaDonsTheoNam.Where(h => h.Thang == currentMonth).ToList();

            model.DoanhThuThangNay = hoaDonThangNay
                .Where(h => h.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                .Sum(h => h.TongTien);

            model.TongTienChoThu = hoaDonThangNay
                .Where(h => h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan)
                .Sum(h => h.TongTien);

            // --- 5. BIỂU ĐỒ DOANH THU 12 THÁNG (Theo năm được chọn) ---
            for (int month = 1; month <= 12; month++)
            {
                model.ChartLabels.Add($"T{month}/{selectedYear}");

                var doanhThuThang = hoaDonsTheoNam
                    .Where(h => h.Thang == month && h.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                    .Sum(h => h.TongTien);

                model.ChartData.Add(doanhThuThang);
            }

            return View(model);
        }
    }
}