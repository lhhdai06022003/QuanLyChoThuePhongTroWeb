using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichSuThanhToans.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class LichSuThanhToanStore : ILichSuThanhToanStore
    {
        private readonly ApplicationDbContext _context;

        public LichSuThanhToanStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTableResponse<LichSuThanhToanGiaoDichRes>> GetDanhSachThanhToanAsync(
            DataTableRequest request, int chiNhanhId, int phuongThuc, DateTime? tuNgay, DateTime? denNgay, CancellationToken cancellationToken = default)
        {
            var query = _context.LichSuThanhToans
                .Where(l => !l.IsDeleted);

            if (chiNhanhId > 0)
            {
                query = query.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            }

            if (phuongThuc >= 0)
            {
                query = query.Where(l => (int)l.PhuongThucThanhToan == phuongThuc);
            }

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

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchLower = request.SearchValue.ToLower();
                query = query.Where(l => l.MaGiaoDich.ToLower().Contains(searchLower) ||
                                         l.HoaDon.MaHoaDon.ToLower().Contains(searchLower) ||
                                         l.HoaDon.HopDong.PhongTro.SoPhong.ToLower().Contains(searchLower) ||
                                         l.HoaDon.HopDong.NguoiThue.HoVaTen.ToLower().Contains(searchLower));
            }

            int totalRecords = await query.CountAsync(cancellationToken);

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
                    NgayThanhToan = l.NgayThanhToan.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
                    NguoiXacNhan = l.NguoiXacNhan != null ? l.NguoiXacNhan.TenDangNhap : "Hệ thống",
                    GhiChu = l.GhiChu
                })
                .ToListAsync(cancellationToken);

            return new DataTableResponse<LichSuThanhToanGiaoDichRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = dataList
            };
        }

        public async Task<ThongKeThanhToanRes> GetThongKeThanhToanAsync(int chiNhanhId, DateTime? tuNgay, DateTime? denNgay, CancellationToken cancellationToken = default)
        {
            var query = _context.LichSuThanhToans
                .Where(l => !l.IsDeleted);

            if (chiNhanhId > 0)
            {
                query = query.Where(l => l.HoaDon.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            }

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

            var stats = await query
                .GroupBy(x => 1)
                .Select(g => new ThongKeThanhToanRes
                {
                    TongDoanhThu = g.Sum(p => p.SoTienThanhToan),
                    TongSoGiaoDich = g.Count(),
                    DoanhThuTienMat = g.Where(p => p.PhuongThucThanhToan == PhuongThucThanhToan.TienMat).Sum(p => p.SoTienThanhToan),
                    DoanhThuChuyenKhoan = g.Where(p => p.PhuongThucThanhToan == PhuongThucThanhToan.ChuyenKhoan).Sum(p => p.SoTienThanhToan)
                })
                .FirstOrDefaultAsync(cancellationToken);

            return stats ?? new ThongKeThanhToanRes
            {
                TongDoanhThu = 0,
                TongSoGiaoDich = 0,
                DoanhThuTienMat = 0,
                DoanhThuChuyenKhoan = 0
            };
        }

        public async Task<LichSuThanhToan?> GetByIdWithHoaDonAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.LichSuThanhToans
                .Include(l => l.HoaDon)
                .FirstOrDefaultAsync(l => l.LichSuThanhToanId == id && !l.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<LichSuThanhToan>> GetLichSuByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.LichSuThanhToans
                .Include(ls => ls.HoaDon).ThenInclude(hd => hd.HopDong).ThenInclude(h => h.PhongTro)
                .Where(ls => ls.HoaDon.HopDong.NguoiThueId == nguoiThueId && !ls.IsDeleted)
                .OrderByDescending(ls => ls.NgayThanhToan)
                .ToListAsync(cancellationToken);
        }

        public void UpdateLichSu(LichSuThanhToan lichSu)
        {
            _context.LichSuThanhToans.Update(lichSu);
        }

        public void UpdateHoaDon(HoaDon hoaDon)
        {
            _context.HoaDons.Update(hoaDon);
        }
    }
}
