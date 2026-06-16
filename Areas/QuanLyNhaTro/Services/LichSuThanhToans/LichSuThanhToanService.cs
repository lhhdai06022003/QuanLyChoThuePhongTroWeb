using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.LichSuThanhToans
{
    public class LichSuThanhToanService : ILichSuThanhToanService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LichSuThanhToanService> _logger;

        public LichSuThanhToanService(ApplicationDbContext context, ILogger<LichSuThanhToanService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay)
        {
            var query = _context.LichSuThanhToans
                .Include(l => l.NguoiXacNhan)
                .Include(l => l.HoaDon)
                    .ThenInclude(h => h.HopDong)
                        .ThenInclude(hd => hd.PhongTro)
                            .ThenInclude(p => p.ChiNhanh)
                .Include(l => l.HoaDon)
                    .ThenInclude(h => h.HopDong)
                        .ThenInclude(hd => hd.NguoiThue)
                .Where(l => !l.IsDeleted);

            // Lọc theo chi nhánh
            if (chiNhanhId > 0)
            {
                query = query.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            }

            // Lọc theo phương thức
            if (phuongThuc >= 0)
            {
                query = query.Where(l => (int)l.PhuongThucThanhToan == phuongThuc);
            }

            // Lọc theo khoảng ngày
            if (tuNgay.HasValue)
            {
                var utcTu = DateTime.SpecifyKind(tuNgay.Value.Date, DateTimeKind.Utc);
                query = query.Where(l => l.NgayThanhToan >= utcTu);
            }
            if (denNgay.HasValue)
            {
                var utcDen = DateTime.SpecifyKind(denNgay.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(l => l.NgayThanhToan <= utcDen);
            }

            // Lọc theo ô tìm kiếm
            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchLower = request.SearchValue.ToLower();
                query = query.Where(l => l.MaGiaoDich.ToLower().Contains(searchLower) ||
                                         l.HoaDon.MaHoaDon.ToLower().Contains(searchLower) ||
                                         l.HoaDon.HopDong.PhongTro.SoPhong.ToLower().Contains(searchLower) ||
                                         l.HoaDon.HopDong.NguoiThue.HoVaTen.ToLower().Contains(searchLower));
            }

            int totalRecords = await query.CountAsync();

            // Sắp xếp mặc định: ngày giao dịch mới nhất lên trên
            query = query.OrderByDescending(l => l.NgayThanhToan);

            var dataList = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(l => new LichSuThanhToanGiaoDichRes
                {
                    LichSuThanhToanId = l.LichSuThanhToanId,
                    MaGiaoDich = l.MaGiaoDich,
                    HoaDonId = l.HoaDonId,
                    MaHoaDon = l.HoaDon.MaHoaDon,
                    TenPhong = l.HoaDon.HopDong.PhongTro.SoPhong,
                    TenNguoiThue = l.HoaDon.HopDong.NguoiThue.HoVaTen,
                    TenChiNhanh = l.HoaDon.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    SoTienThanhToan = l.SoTienThanhToan,
                    PhuongThucThanhToan = (int)l.PhuongThucThanhToan,
                    PhuongThucThanhToanText = l.PhuongThucThanhToan == PhuongThucThanhToan.TienMat ? "Tiền mặt" : "Chuyển khoản",
                    NgayThanhToan = l.NgayThanhToan.AddHours(7).ToString("dd/MM/yyyy HH:mm"), // Convert to GMT+7
                    NguoiXacNhan = l.NguoiXacNhan != null ? l.NguoiXacNhan.TenDangNhap : "Hệ thống",
                    GhiChu = l.GhiChu
                })
                .ToListAsync();

            return new DataTableResponse<LichSuThanhToanGiaoDichRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = dataList
            };
        }

        public async Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay)
        {
            var query = _context.LichSuThanhToans
                .Include(l => l.HoaDon)
                    .ThenInclude(h => h.HopDong)
                        .ThenInclude(hd => hd.PhongTro)
                .Where(l => !l.IsDeleted);

            // Lọc theo chi nhánh
            if (chiNhanhId > 0)
            {
                query = query.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            }

            // Lọc theo khoảng ngày
            if (tuNgay.HasValue)
            {
                var utcTu = DateTime.SpecifyKind(tuNgay.Value.Date, DateTimeKind.Utc);
                query = query.Where(l => l.NgayThanhToan >= utcTu);
            }
            if (denNgay.HasValue)
            {
                var utcDen = DateTime.SpecifyKind(denNgay.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(l => l.NgayThanhToan <= utcDen);
            }

            var payments = await query.ToListAsync();

            var thongKe = new ThongKeThanhToanRes
            {
                TongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                TongSoGiaoDich = payments.Count,
                DoanhThuTienMat = payments.Where(p => p.PhuongThucThanhToan == PhuongThucThanhToan.TienMat).Sum(p => p.SoTienThanhToan),
                DoanhThuChuyenKhoan = payments.Where(p => p.PhuongThucThanhToan == PhuongThucThanhToan.ChuyenKhoan).Sum(p => p.SoTienThanhToan)
            };

            return thongKe;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> HuyGiaoDichAsync(int id, int adminUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lstt = await _context.LichSuThanhToans
                    .Include(l => l.HoaDon)
                    .FirstOrDefaultAsync(l => l.LichSuThanhToanId == id && !l.IsDeleted);

                if (lstt == null)
                {
                    return (false, "Không tìm thấy giao dịch hoặc giao dịch đã bị hủy trước đó.");
                }

                var hoaDon = lstt.HoaDon;
                if (hoaDon == null)
                {
                    return (false, "Không tìm thấy hóa đơn liên quan.");
                }

                // 1. Soft-delete giao dịch thanh toán
                lstt.IsDeleted = true;
                _context.LichSuThanhToans.Update(lstt);

                // 2. Chuyển hóa đơn về trạng thái Chưa thanh toán
                hoaDon.TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan;
                hoaDon.NgayCapNhat = DateTime.UtcNow;
                _context.HoaDons.Update(hoaDon);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi hệ thống khi hủy giao dịch thanh toán ID: {Id} bởi Admin: {AdminId}", id, adminUserId);
                return (false, "Lỗi hệ thống khi hủy giao dịch thanh toán.");
            }
        }
    }
}
