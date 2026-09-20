using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class HoaDonStore : IHoaDonStore
    {
        private readonly ApplicationDbContext _context;

        public HoaDonStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ChiNhanh?> GetChiNhanhByIdAsync(int chiNhanhId, CancellationToken cancellationToken = default)
        {
            return await _context.ChiNhanhs.FirstOrDefaultAsync(c => c.ChiNhanhId == chiNhanhId, cancellationToken);
        }

        public async Task<IReadOnlyList<HopDong>> GetValidContractsForBillingAsync(int chiNhanhId, IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                            roomIds.Contains(h.PhongTroId) &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau <= endUtc &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc >= startUtc.AddDays(1)))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<int>> GetExistingInvoiceContractIdsAsync(IReadOnlyList<int> contractIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons
                .Where(x => contractIds.Contains(x.HopDongId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                .Select(x => x.HopDongId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetDichVuDienNuocByRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .Where(x => roomIds.Contains(x.PhongTroId) && x.Thang == thang && x.Nam == nam && !x.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DangKyDichVu>> GetDangKyDichVusForBillingAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
        {
            return await _context.DangKyDichVus
                .Include(d => d.DichVuChiNhanh)
                    .ThenInclude(dcn => dcn.DichVu)
                .Where(d => roomIds.Contains(d.PhongTroId) &&
                            d.NgayBatDau <= endUtc &&
                            (d.NgayKetThuc == null || d.NgayKetThuc >= startUtc))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<YeuCauSuCo>> GetBillableSuCosAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
        {
            return await _context.YeuCauSuCos
                .Where(x => roomIds.Contains(x.PhongTroId)
                    && x.TrangThai == TrangThaiSuCo.DaHoanThanh
                    && x.CongVaoHoaDon && !x.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PhongTro>> GetPhongTrosByChiNhanhIdAsync(int chiNhanhId, CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros.AsNoTracking()
                .Where(p => p.ChiNhanhId == chiNhanhId && !p.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<HoaDon?> GetHoaDonWithDetailsForUpdateAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons
                .Include(h => h.ChiTietHoaDonDichVus)
                .FirstOrDefaultAsync(h => h.HoaDonId == id && !h.IsDeleted, cancellationToken);
        }

        public async Task<DataTableResponse<HoaDonRes>> GetHoaDonsDataTableAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted);

            if (chiNhanhId > 0)
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            if (thang > 0)
                query = query.Where(h => h.Thang == thang);
            if (nam > 0)
                query = query.Where(h => h.Nam == nam);
            if (trangThai >= 0)
                query = query.Where(h => (int)h.TrangThaiHoaDon == trangThai);

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string s = request.SearchValue.ToLower();
                query = query.Where(h => h.MaHoaDon.ToLower().Contains(s) ||
                                         h.HopDong.PhongTro.SoPhong.ToLower().Contains(s) ||
                                         h.HopDong.NguoiThue.HoVaTen.ToLower().Contains(s));
            }

            int totalRecords = await query.CountAsync(cancellationToken);

            query = query.OrderByDescending(h => h.HoaDonId);

            var data = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(h => new HoaDonRes
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    HopDongId = h.HopDongId,
                    MaHopDong = h.HopDong.MaHopDong,
                    TenPhong = h.HopDong.PhongTro.SoPhong,
                    TenNguoiThue = h.HopDong.NguoiThue.HoVaTen,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    Thang = h.Thang,
                    Nam = h.Nam,
                    TongTien = h.TongTien,
                    TrangThaiHoaDon = h.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                    TrangThaiHoaDonValue = (int)h.TrangThaiHoaDon,
                    NgayTao = h.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                    SoDienThoai = h.HopDong.NguoiThue.SoDienThoai,
                    Email = h.HopDong.NguoiThue.Email ?? ""
                })
                .ToListAsync(cancellationToken);

            return new DataTableResponse<HoaDonRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = data
            };
        }

        public async Task<HoaDonChiTietRes?> GetHoaDonDetailByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var hd = await _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Include(h => h.ChiTietHoaDonDichVus).ThenInclude(ct => ct.DichVu)
                .Include(h => h.LichSuThanhToans).ThenInclude(l => l.NguoiXacNhan)
                .FirstOrDefaultAsync(h => h.HoaDonId == id && !h.IsDeleted, cancellationToken);

            if (hd == null) return null;

            var dienDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Điện") || d.TenDichVu.Contains("điện")) && !d.IsDeleted, cancellationToken);
            var nuocDichVu = await _context.DichVus.FirstOrDefaultAsync(d => (d.TenDichVu.Contains("Nước") || d.TenDichVu.Contains("nước")) && !d.IsDeleted, cancellationToken);

            string donViDien = dienDichVu?.DonVi ?? "kWh";
            string donViNuoc = nuocDichVu?.DonVi ?? "m³";

            return new HoaDonChiTietRes
            {
                HoaDonId = hd.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                TenPhong = hd.HopDong.PhongTro.SoPhong,
                TenNguoiThue = hd.HopDong.NguoiThue.HoVaTen,
                TenChiNhanh = hd.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                DiaChiChiNhanh = hd.HopDong.PhongTro.ChiNhanh.DiaChi,
                SoDienThoaiChiNhanh = hd.HopDong.PhongTro.ChiNhanh.SoDienThoai ?? "",
                Thang = hd.Thang,
                Nam = hd.Nam,
                TongTien = hd.TongTien,
                TrangThaiHoaDon = hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                NgayTao = hd.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                Email = hd.HopDong.NguoiThue.Email ?? "",
                ChiTietHoaDons = hd.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).Select(ct => new ChiTietHoaDonRes
                {
                    ChiTietHoaDonId = ct.ChiTietHoaDonId,
                    TenDichVu = ct.TenDichVu,
                    DonGia = ct.DonGia,
                    SoLuong = ct.SoLuong,
                    TongTien = ct.TongTien,
                    DonVi = ct.DichVu != null ? ct.DichVu.DonVi :
                            (ct.TenDichVu.Contains("Tiền thuê phòng") ? "Tháng" :
                            (ct.TenDichVu.ToLower().Contains("điện") ? donViDien :
                            (ct.TenDichVu.ToLower().Contains("nước") ? donViNuoc : "")))
                }).ToList(),
                LichSuThanhToans = hd.LichSuThanhToans.Where(x => !x.IsDeleted).Select(ls => new LichSuThanhToanRes
                {
                    LichSuThanhToanId = ls.LichSuThanhToanId,
                    MaGiaoDich = ls.MaGiaoDich,
                    SoTienThanhToan = ls.SoTienThanhToan,
                    PhuongThucThanhToan = ls.PhuongThucThanhToan == PhuongThucThanhToan.TienMat ? "Tiền mặt" : "Chuyển khoản",
                    NgayThanhToan = ls.NgayThanhToan.ToString("dd/MM/yyyy HH:mm"),
                    NguoiXacNhan = ls.NguoiXacNhan?.TenDangNhap ?? "",
                    GhiChu = ls.GhiChu
                }).ToList()
            };
        }

        public async Task<HoaDon?> GetActiveHoaDonByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons.FirstOrDefaultAsync(h => h.HoaDonId == id && !h.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<HoaDonRes>> GetUnpaidInvoicesAsync(int chiNhanhId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var query = _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan);

            if (chiNhanhId > 0)
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            if (thang > 0)
                query = query.Where(h => h.Thang == thang);
            if (nam > 0)
                query = query.Where(h => h.Nam == nam);

            return await query
                .OrderByDescending(h => h.HoaDonId)
                .Select(h => new HoaDonRes
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    HopDongId = h.HopDongId,
                    MaHopDong = h.HopDong.MaHopDong,
                    TenPhong = h.HopDong.PhongTro.SoPhong,
                    TenNguoiThue = h.HopDong.NguoiThue.HoVaTen,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    Thang = h.Thang,
                    Nam = h.Nam,
                    TongTien = h.TongTien,
                    TrangThaiHoaDon = "Chưa thanh toán",
                    TrangThaiHoaDonValue = (int)h.TrangThaiHoaDon,
                    NgayTao = h.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                    SoDienThoai = h.HopDong.NguoiThue.SoDienThoai,
                    Email = h.HopDong.NguoiThue.Email ?? ""
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<int>> GetContractIdsByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted)
                .Select(x => x.HopDongId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<HoaDonRes>> GetInvoicesByContractIdsAsync(IReadOnlyList<int> contractIds, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons
                .Include(x => x.HopDong).ThenInclude(h => h.PhongTro)
                .Where(x => contractIds.Contains(x.HopDongId) && !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .Select(x => new HoaDonRes
                {
                    HoaDonId = x.HoaDonId,
                    MaHoaDon = x.MaHoaDon,
                    HopDongId = x.HopDongId,
                    MaHopDong = x.HopDong.MaHopDong,
                    TenPhong = x.HopDong.PhongTro.SoPhong,
                    Thang = x.Thang,
                    Nam = x.Nam,
                    TongTien = x.TongTien,
                    TrangThaiHoaDon = x.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                    TrangThaiHoaDonValue = (int)x.TrangThaiHoaDon,
                    NgayTao = x.NgayTao.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> CheckHoaDonOwnershipAsync(int hoaDonId, int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons
                .Include(h => h.HopDong)
                .AnyAsync(h => h.HoaDonId == hoaDonId && h.HopDong.NguoiThueId == nguoiThueId && !h.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<HoaDon>> GetOverdueInvoicesAsync(DateTime thresholdUtc, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons
                .Include(h => h.HopDong)
                    .ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted
                    && h.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan
                    && h.NgayTao <= thresholdUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task AddInvoicesAsync(IEnumerable<HoaDon> invoices, CancellationToken cancellationToken = default)
        {
            await _context.HoaDons.AddRangeAsync(invoices, cancellationToken);
        }

        public void UpdateSuCos(IEnumerable<YeuCauSuCo> suCos)
        {
            _context.YeuCauSuCos.UpdateRange(suCos);
        }

        public void RemoveChiTietHoaDons(IEnumerable<ChiTietHoaDon> chiTiets)
        {
            _context.ChiTietHoaDons.RemoveRange(chiTiets);
        }

        public void UpdateHoaDon(HoaDon hoaDon)
        {
            _context.HoaDons.Update(hoaDon);
        }

        public async Task AddLichSuThanhToanAsync(LichSuThanhToan lichSu, CancellationToken cancellationToken = default)
        {
            await _context.LichSuThanhToans.AddAsync(lichSu, cancellationToken);
        }
    }
}
