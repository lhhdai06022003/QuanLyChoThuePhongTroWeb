using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class InvoiceViewStore : IInvoiceViewStore
    {
        private readonly ApplicationDbContext _context;

        public InvoiceViewStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTableResponse<HoaDonRes>> GetEmployeeListAsync(
            DataTableRequest request,
            InvoiceListFilter filter,
            IReadOnlyList<int>? allowedBranchIds,
            CancellationToken ct = default)
        {
            var query = _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .AsQueryable();

            if (filter.ChiNhanhId > 0)
            {
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == filter.ChiNhanhId);
            }
            else if (allowedBranchIds != null)
            {
                query = query.Where(h => allowedBranchIds.Contains(h.HopDong.PhongTro.ChiNhanhId));
            }

            if (filter.Thang > 0)
            {
                query = query.Where(h => h.Thang == filter.Thang);
            }

            if (filter.Nam > 0)
            {
                query = query.Where(h => h.Nam == filter.Nam);
            }

            if (filter.TrangThaiThanhToan >= 0)
            {
                query = query.Where(h => (int)h.TrangThaiHoaDon == filter.TrangThaiThanhToan);
            }

            if (filter.TrangThaiPhatHanh == (int)TrangThaiPhatHanhHoaDon.DaHuy)
            {
                query = query.Where(h => h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy);
            }
            else if (filter.TrangThaiPhatHanh >= 0 && filter.TrangThaiPhatHanh <= 3)
            {
                query = query.Where(h => !h.IsDeleted && (int)h.TrangThaiPhatHanh == filter.TrangThaiPhatHanh);
            }
            else
            {
                // Mặc định (-1 hoặc giá trị ngoài phạm vi): tất cả trừ DaHuy và trừ bản xóa cũ
                query = query.Where(h => !h.IsDeleted && h.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchValue))
            {
                string s = request.SearchValue.Trim().ToLower();
                query = query.Where(h => h.MaHoaDon.ToLower().Contains(s) ||
                                         h.HopDong.PhongTro.SoPhong.ToLower().Contains(s) ||
                                         h.HopDong.NguoiThue.HoVaTen.ToLower().Contains(s));
            }

            int totalRecords = await query.CountAsync(ct);

            query = query.OrderByDescending(h => h.HoaDonId);

            var rawData = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(h => new
                {
                    h.HoaDonId,
                    h.MaHoaDon,
                    h.HopDongId,
                    h.HopDong.MaHopDong,
                    TenPhong = h.HopDong.PhongTro.SoPhong,
                    TenNguoiThue = h.HopDong.NguoiThue.HoVaTen,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    h.Thang,
                    h.Nam,
                    h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan),
                    h.TrangThaiHoaDon,
                    h.TrangThaiPhatHanh,
                    h.NgayTao,
                    SoDienThoai = h.HopDong.NguoiThue.SoDienThoai,
                    Email = h.HopDong.NguoiThue.Email ?? ""
                })
                .ToListAsync(ct);

            var data = rawData.Select(h => new HoaDonRes
            {
                HoaDonId = h.HoaDonId,
                MaHoaDon = h.MaHoaDon,
                HopDongId = h.HopDongId,
                MaHopDong = h.MaHopDong,
                TenPhong = h.TenPhong,
                TenNguoiThue = h.TenNguoiThue,
                TenChiNhanh = h.TenChiNhanh,
                Thang = h.Thang,
                Nam = h.Nam,
                TongTien = h.TongTien,
                DaThu = h.DaThu,
                ConLai = h.TongTien - h.DaThu,
                TrangThaiHoaDon = InvoiceStatusLabels.HienThi(h.TrangThaiPhatHanh, h.TrangThaiHoaDon),
                TrangThaiHoaDonValue = (int)h.TrangThaiHoaDon,
                TrangThaiPhatHanhValue = (int)h.TrangThaiPhatHanh,
                NgayTao = h.NgayTao.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                SoDienThoai = h.SoDienThoai,
                Email = h.Email
            }).ToList();

            return new DataTableResponse<HoaDonRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = data
            };
        }

        public async Task<int?> GetViewableBranchIdAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .Where(h => h.HoaDonId == hoaDonId && (!h.IsDeleted || h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy))
                .Select(h => (int?)h.HopDong.PhongTro.ChiNhanhId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<HoaDonChiTietRes?> GetDetailAsync(int hoaDonId, bool includeHistory, CancellationToken ct = default)
        {
            var hd = await _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Include(h => h.ChiTietHoaDonDichVus).ThenInclude(ct => ct.DichVu)
                .Include(h => h.LichSuThanhToans).ThenInclude(l => l.NguoiXacNhan)
                .FirstOrDefaultAsync(h => h.HoaDonId == hoaDonId && (!h.IsDeleted || h.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy), ct);

            if (hd == null) return null;

            var dienDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Điện") || d.TenDichVu.Contains("điện")) && !d.IsDeleted, ct);
            var nuocDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Nước") || d.TenDichVu.Contains("nước")) && !d.IsDeleted, ct);

            string donViDien = dienDichVu?.DonVi ?? "kWh";
            string donViNuoc = nuocDichVu?.DonVi ?? "m³";

            if (includeHistory)
            {
                var histories = await _context.LichSuTrangThaiHoaDons
                    .Where(l => l.HoaDonId == hoaDonId)
                    .OrderBy(l => l.NgayThucHien)
                    .ThenBy(l => l.LichSuTrangThaiHoaDonId)
                    .ToListAsync(ct);

                var userIds = histories
                    .Where(h => h.NguoiThucHienId.HasValue)
                    .Select(h => h.NguoiThucHienId!.Value)
                    .Distinct()
                    .ToList();

                var users = userIds.Count > 0
                    ? await _context.NguoiDungs.IgnoreQueryFilters()
                        .Where(u => userIds.Contains(u.NguoiDungId))
                        .ToDictionaryAsync(u => u.NguoiDungId, u => u.TenDangNhap, ct)
                    : new Dictionary<int, string>();

                var historyDtos = histories.Select(l => new InvoiceStatusHistoryRes
                {
                    ThoiGian = l.NgayThucHien.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                    PhatHanhTu = InvoiceStatusLabels.PhatHanh(l.TrangThaiPhatHanhCu),
                    PhatHanhDen = InvoiceStatusLabels.PhatHanh(l.TrangThaiPhatHanhMoi),
                    ThanhToanTu = InvoiceStatusLabels.ThanhToan(l.TrangThaiThanhToanCu),
                    ThanhToanDen = InvoiceStatusLabels.ThanhToan(l.TrangThaiThanhToanMoi),
                    NguoiThucHien = (l.NguoiThucHienId.HasValue && users.TryGetValue(l.NguoiThucHienId.Value, out var name) && !string.IsNullOrEmpty(name))
                        ? name
                        : "Hệ thống",
                    LyDo = l.LyDo ?? string.Empty
                }).ToList();

                string? latestCancelReason = null;
                if (hd.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
                {
                    latestCancelReason = histories
                        .Where(l => l.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaHuy)
                        .OrderByDescending(l => l.NgayThucHien)
                        .ThenByDescending(l => l.LichSuTrangThaiHoaDonId)
                        .Select(l => l.LyDo)
                        .FirstOrDefault() ?? string.Empty;
                }

                return InvoiceDetailProjection.ProjectToRes(hd, donViDien, donViNuoc, historyDtos, latestCancelReason);
            }
            else
            {
                string? latestCancelReason = null;
                if (hd.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
                {
                    latestCancelReason = await _context.LichSuTrangThaiHoaDons
                        .Where(l => l.HoaDonId == hoaDonId && l.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaHuy)
                        .OrderByDescending(l => l.NgayThucHien)
                        .ThenByDescending(l => l.LichSuTrangThaiHoaDonId)
                        .Select(l => l.LyDo)
                        .FirstOrDefaultAsync(ct) ?? string.Empty;
                }

                return InvoiceDetailProjection.ProjectToRes(hd, donViDien, donViNuoc, null, latestCancelReason);
            }
        }
    }
}
