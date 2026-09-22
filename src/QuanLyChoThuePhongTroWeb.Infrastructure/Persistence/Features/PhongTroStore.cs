using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class PhongTroStore : IPhongTroStore
    {
        private readonly ApplicationDbContext _context;

        public PhongTroStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<SelectOptionDto>> GetChiNhanhDropdownAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ChiNhanhs
                .Where(c => !c.IsDeleted)
                .Select(c => new SelectOptionDto(c.ChiNhanhId.ToString(), c.TenChiNhanh))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PhongTroListItemDto>> GetDanhSachPhongTroAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros
                .Include(p => p.ChiNhanh)
                .Where(p => !p.IsDeleted)
                .Select(p => new PhongTroListItemDto
                {
                    PhongTroId = p.PhongTroId,
                    TenChiNhanh = p.ChiNhanh.TenChiNhanh,
                    SoPhong = p.SoPhong,
                    TangLau = p.TangLau,
                    GiaThue = p.GiaThue,
                    DienTich = p.DienTich,
                    TrangThai = (int)p.TrangThai
                })
                .OrderByDescending(p => p.PhongTroId)
                .ToListAsync(cancellationToken);
        }

        public async Task<PhongTro?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros.FirstOrDefaultAsync(p => p.PhongTroId == id && !p.IsDeleted, cancellationToken);
        }

        public async Task<bool> ExistsSoPhongAsync(string soPhong, int chiNhanhId, int excludeId = 0, CancellationToken cancellationToken = default)
        {
            var query = _context.PhongTros.Where(p => p.SoPhong == soPhong && p.ChiNhanhId == chiNhanhId && !p.IsDeleted);
            if (excludeId > 0)
            {
                query = query.Where(p => p.PhongTroId != excludeId);
            }
            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> HasActiveHopDongAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs.AnyAsync(h =>
                h.PhongTroId == phongTroId &&
                h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !h.IsDeleted,
                cancellationToken);
        }

        public async Task<int> GetActiveMembersCountForActiveContractAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            var hopDongActive = await _context.HopDongs
                .FirstOrDefaultAsync(h => h.PhongTroId == phongTroId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted, cancellationToken);

            if (hopDongActive == null) return 0;

            return await _context.ChiTietThanhVienHopDongs
                .CountAsync(x => x.HopDongId == hopDongActive.HopDongId && !x.IsDeleted && x.NgayChuyenDi == null, cancellationToken);
        }

        public async Task<List<PhongTro>> GetDanhSachPhongTroConTrongAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros
                .Where(p => !p.IsDeleted)
                .Select(p => new PhongTro
                {
                    PhongTroId = p.PhongTroId,
                    SoPhong = p.SoPhong,
                    ChiNhanhId = p.ChiNhanhId,
                    TrangThai = p.TrangThai,
                    GiaThue = p.GiaThue
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<PhongCardRes>> GetSoDoPhongAsync(int chiNhanhId, CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow;
            var in15Days = today.AddDays(15);

            var query = _context.PhongTros
                .Include(p => p.ChiNhanh)
                .Where(p => !p.IsDeleted);

            if (chiNhanhId > 0)
                query = query.Where(p => p.ChiNhanhId == chiNhanhId);

            var phongs = await query.OrderBy(p => p.TangLau).ThenBy(p => p.SoPhong).ToListAsync(cancellationToken);

            var phongTroIds = phongs.Select(p => p.PhongTroId).ToList();
            var hopDongs = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Where(h => phongTroIds.Contains(h.PhongTroId) &&
                            h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                            !h.IsDeleted)
                .ToListAsync(cancellationToken);

            var hopDongIds = hopDongs.Select(h => h.HopDongId).ToList();
            var unpaidInvoicesSums = await _context.HoaDons
                .Where(hd => hopDongIds.Contains(hd.HopDongId) &&
                             hd.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan &&
                             !hd.IsDeleted)
                .GroupBy(hd => hd.HopDongId)
                .Select(g => new { HopDongId = g.Key, Sum = g.Sum(hd => hd.TongTien) })
                .ToDictionaryAsync(x => x.HopDongId, x => x.Sum, cancellationToken);

            var result = new List<PhongCardRes>();

            foreach (var p in phongs)
            {
                var hopDong = hopDongs.FirstOrDefault(h => h.PhongTroId == p.PhongTroId);

                decimal soTienNo = 0m;
                bool coNoTien = false;
                if (hopDong != null)
                {
                    unpaidInvoicesSums.TryGetValue(hopDong.HopDongId, out soTienNo);
                    coNoTien = soTienNo > 0;
                }

                bool sapHetHan = false;
                int? soNgayConLai = null;
                if (hopDong?.ThoiDiemKetThuc != null)
                {
                    soNgayConLai = (int)(hopDong.ThoiDiemKetThuc.Value - today).TotalDays;
                    sapHetHan = hopDong.ThoiDiemKetThuc.Value <= in15Days && hopDong.ThoiDiemKetThuc.Value >= today;
                }

                string trangThaiText = p.TrangThai switch
                {
                    TrangThaiPhong.Trong => "Trống",
                    TrangThaiPhong.DaThue => "Đã thuê",
                    TrangThaiPhong.BaoTri => "Bảo trì",
                    _ => ""
                };

                result.Add(new PhongCardRes
                {
                    PhongTroId = p.PhongTroId,
                    SoPhong = p.SoPhong,
                    TangLau = p.TangLau,
                    GiaThue = p.GiaThue,
                    DienTich = p.DienTich,
                    SoNguoiToiDa = p.SoNguoiToiDa,
                    TrangThai = (int)p.TrangThai,
                    TrangThaiText = trangThaiText,
                    ChiNhanhId = p.ChiNhanhId,
                    TenChiNhanh = p.ChiNhanh.TenChiNhanh,
                    HopDongId = hopDong?.HopDongId,
                    TenNguoiThue = hopDong?.NguoiThue?.HoVaTen,
                    SdtNguoiThue = hopDong?.NguoiThue?.SoDienThoai,
                    NgayHetHan = hopDong?.ThoiDiemKetThuc,
                    CoNoTien = coNoTien,
                    SoTienNo = soTienNo,
                    SapHetHan = sapHetHan,
                    SoNgayConLai = soNgayConLai
                });
            }

            return result;
        }

        public async Task<QuickContractDto?> GetQuickContractAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            var contract = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Include(h => h.PhongTro)
                .Where(h => h.PhongTroId == phongTroId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (contract == null) return null;

            var members = await _context.ChiTietThanhVienHopDongs
                .Include(m => m.NguoiThue)
                .Where(m => m.HopDongId == contract.HopDongId && m.NgayChuyenDi == null && !m.IsDeleted)
                .Select(m => new QuickContractMemberDto
                {
                    hoVaTen = m.NguoiThue.HoVaTen,
                    soDienThoai = m.NguoiThue.SoDienThoai,
                    cccd = m.NguoiThue.CCCD,
                    ngayVao = m.NgayVao.ToString("dd/MM/yyyy")
                })
                .ToListAsync(cancellationToken);

            var services = await _context.DangKyDichVus
                .Include(s => s.DichVuChiNhanh.DichVu)
                .Where(s => s.PhongTroId == phongTroId && !s.DichVuChiNhanh.IsDeleted)
                .Select(s => new QuickContractServiceDto
                {
                    tenDichVu = s.DichVuChiNhanh.DichVu.TenDichVu,
                    donGia = s.DichVuChiNhanh.GiaDichVu,
                    donVi = s.DichVuChiNhanh.DichVu.DonVi,
                    soLuong = s.SoLuong
                })
                .ToListAsync(cancellationToken);

            return new QuickContractDto
            {
                hopDongId = contract.HopDongId,
                maHopDong = contract.MaHopDong,
                thoiDiemBatDau = contract.ThoiDiemBatDau.ToString("dd/MM/yyyy"),
                thoiDiemKetThuc = contract.ThoiDiemKetThuc.HasValue ? contract.ThoiDiemKetThuc.Value.ToString("dd/MM/yyyy") : "Không xác định",
                tienCocPhong = contract.TienCocPhong,
                tienThuePhong = contract.TienThuePhong,
                tenNguoiDaiDien = contract.NguoiThue.HoVaTen,
                soDienThoaiDaiDien = contract.NguoiThue.SoDienThoai,
                cccdDaiDien = contract.NguoiThue.CCCD,
                members = members,
                services = services
            };
        }

        public async Task<UnpaidInvoiceDto?> GetUnpaidInvoiceAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            var invoice = await _context.HoaDons
                .Include(i => i.HopDong)
                    .ThenInclude(h => h.PhongTro)
                .Include(i => i.HopDong)
                    .ThenInclude(h => h.NguoiThue)
                .Where(i => i.HopDong.PhongTroId == phongTroId && i.TrangThaiHoaDon == TrangThaiHoaDon.ChuaThanhToan && !i.IsDeleted)
                .OrderByDescending(i => i.NgayTao)
                .FirstOrDefaultAsync(cancellationToken);

            if (invoice == null) return null;

            var details = await _context.ChiTietHoaDons
                .Include(d => d.DichVu)
                .Where(d => d.HoaDonId == invoice.HoaDonId && !d.IsDeleted)
                .ToListAsync(cancellationToken);

            var mappedDetails = details.Select(d => new UnpaidInvoiceDetailDto
            {
                tenDichVu = d.TenDichVu,
                donGia = d.DonGia,
                soLuong = d.SoLuong,
                donVi = d.DichVu != null ? d.DichVu.DonVi : 
                        (d.TenDichVu.ToLower().Contains("điện") ? "kWh" : 
                        (d.TenDichVu.ToLower().Contains("nước") ? "m³" : "-")),
                tongTien = d.TongTien
            }).ToList();

            return new UnpaidInvoiceDto
            {
                hoaDonId = invoice.HoaDonId,
                maHoaDon = invoice.MaHoaDon,
                thang = invoice.Thang,
                nam = invoice.Nam,
                tongTien = invoice.TongTien,
                tenPhong = invoice.HopDong.PhongTro.SoPhong,
                tenNguoiThue = invoice.HopDong.NguoiThue.HoVaTen,
                chiTiets = mappedDetails
            };
        }

        public async Task<List<ChiNhanh>> GetAllActiveChiNhanhsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ChiNhanhs.Where(c => !c.IsDeleted).ToListAsync(cancellationToken);
        }

        public async Task<List<(int ChiNhanhId, string SoPhong)>> GetAllRoomNumbersAsync(CancellationToken cancellationToken = default)
        {
            var rooms = await _context.PhongTros
                .Where(p => !p.IsDeleted)
                .Select(p => new { p.ChiNhanhId, p.SoPhong })
                .ToListAsync(cancellationToken);

            return rooms.Select(r => (r.ChiNhanhId, r.SoPhong)).ToList();
        }

        public void Add(PhongTro phongTro)
        {
            _context.PhongTros.Add(phongTro);
        }

        public void Update(PhongTro phongTro)
        {
            _context.PhongTros.Update(phongTro);
        }
    }
}
