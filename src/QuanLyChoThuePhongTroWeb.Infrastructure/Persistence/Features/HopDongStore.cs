using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class HopDongStore : IHopDongStore
    {
        private readonly ApplicationDbContext _context;

        public HopDongStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTableResponse<HopDongRes>> GetDataTableResponseAsync(HopDongFilterReq request, CancellationToken cancellationToken = default)
        {
            IQueryable<HopDong> query = _context.HopDongs
                .Include(x => x.PhongTro)
                .Include(x => x.NguoiThue)
                .Where(x => !x.IsDeleted);

            int totalRecords = await query.CountAsync(cancellationToken);

            if (request.ChiNhanhId.HasValue && request.ChiNhanhId > 0)
            {
                query = query.Where(x => x.PhongTro.ChiNhanhId == request.ChiNhanhId.Value);
            }
            if (request.NguoiThueId.HasValue && request.NguoiThueId > 0)
            {
                query = query.Where(x => x.NguoiThueId == request.NguoiThueId.Value);
            }
            if (request.TrangThai.HasValue && request.TrangThai > 0)
            {
                query = query.Where(x => (int)x.TrangThaiHopDong == request.TrangThai.Value);
            }
            if (request.StartDate.HasValue && request.EndDate.HasValue)
            {
                var startUtc = DateTime.SpecifyKind(request.StartDate.Value.Date, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind(request.EndDate.Value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
                query = query.Where(x => x.ThoiDiemBatDau >= startUtc && x.ThoiDiemBatDau <= endUtc);
            }

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string searchValue = request.SearchValue.ToLower();
                query = query.Where(x =>
                    x.MaHopDong.ToLower().Contains(searchValue) ||
                    x.PhongTro.SoPhong.ToLower().Contains(searchValue) ||
                    x.NguoiThue.HoVaTen.ToLower().Contains(searchValue));
            }

            int filteredRecords = await query.CountAsync(cancellationToken);

            if (!string.IsNullOrEmpty(request.SortColumnName))
            {
                var sortColumnData = request.SortColumnName;
                bool isAscending = request.SortDirection?.ToLower() == "asc";

                query = sortColumnData switch
                {
                    "maHopDong" => isAscending ? query.OrderBy(x => x.MaHopDong) : query.OrderByDescending(x => x.MaHopDong),
                    "soPhong" => isAscending ? query.OrderBy(x => x.PhongTro.SoPhong) : query.OrderByDescending(x => x.PhongTro.SoPhong),
                    "tienThuePhong" => isAscending ? query.OrderBy(x => x.TienThuePhong) : query.OrderByDescending(x => x.TienThuePhong),
                    "thoiDiemBatDau" => isAscending ? query.OrderBy(x => x.ThoiDiemBatDau) : query.OrderByDescending(x => x.ThoiDiemBatDau),
                    _ => query.OrderByDescending(x => x.NgayTao)
                };
            }
            else
            {
                query = query.OrderByDescending(x => x.NgayTao);
            }

            var data = await query
                .Select(x => new HopDongRes
                {
                    HopDongId = x.HopDongId,
                    MaHopDong = x.MaHopDong,
                    SoPhong = x.PhongTro.SoPhong,
                    TenNguoiThue = x.NguoiThue.HoVaTen,
                    ThoiDiemBatDau = x.ThoiDiemBatDau,
                    ThoiDiemKetThuc = x.ThoiDiemKetThuc,
                    TienCocPhong = x.TienCocPhong,
                    TienThuePhong = x.TienThuePhong,
                    TrangThaiHopDong = (QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppTrangThaiHopDong)(int)x.TrangThaiHopDong,
                    TenChiNhanh = x.PhongTro.ChiNhanh.TenChiNhanh
                })
                .Skip(request.Start)
                .Take(request.Length)
                .ToListAsync(cancellationToken);

            return new DataTableResponse<HopDongRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = filteredRecords,
                data = data
            };
        }

        public async Task<HopDongDetailRes?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Where(x => x.HopDongId == id && !x.IsDeleted)
                .Select(x => new HopDongDetailRes
                {
                    HopDongId = x.HopDongId,
                    MaHopDong = x.MaHopDong,
                    PhongTroId = x.PhongTroId,
                    SoPhong = x.PhongTro.SoPhong,
                    ChiNhanhId = x.PhongTro.ChiNhanhId,
                    TenChiNhanh = x.PhongTro.ChiNhanh.TenChiNhanh,
                    NguoiThueId = x.NguoiThueId,
                    TenNguoiThue = x.NguoiThue.HoVaTen,
                    ThoiDiemBatDau = x.ThoiDiemBatDau,
                    ThoiDiemKetThuc = x.ThoiDiemKetThuc,
                    TienCocPhong = x.TienCocPhong,
                    TienThuePhong = x.TienThuePhong,
                    TrangThaiHopDong = (int)x.TrangThaiHopDong,
                    DanhSachTieuDeDieuKhoan = x.HopDongDieuKhoans.Select(d => d.TieuDe).ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<HopDong?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<HopDong?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .FirstOrDefaultAsync(x => x.HopDongId == id && !x.IsDeleted, cancellationToken);
        }

        public async Task<string?> GetMaChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default)
        {
            var chiNhanh = await _context.ChiNhanhs.FindAsync(new object[] { chiNhanhId }, cancellationToken);
            if (chiNhanh == null) return null;
            return !string.IsNullOrEmpty(chiNhanh.MaChiNhanh) ? chiNhanh.MaChiNhanh : $"CN{chiNhanhId}";
        }

        public async Task<int> CountContractsWithPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs.CountAsync(x => x.MaHopDong.StartsWith(prefix), cancellationToken);
        }

        public async Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs.AnyAsync(x => x.MaHopDong == code, cancellationToken);
        }

        public async Task<bool> IsRoomRentedAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs.AnyAsync(x =>
                x.PhongTroId == phongTroId &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted, cancellationToken);
        }

        public async Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int? excludeHopDongId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.HopDongs.Where(x =>
                x.NguoiThueId == nguoiThueId &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted);

            if (excludeHopDongId.HasValue)
            {
                query = query.Where(x => x.HopDongId != excludeHopDongId.Value);
            }

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs.AnyAsync(x =>
                x.PhongTroId == phongTroId &&
                x.HopDongId != excludeHopDongId &&
                x.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                !x.IsDeleted, cancellationToken);
        }

        public async Task<PhongTro?> GetPhongTroByIdAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros.FindAsync(new object[] { phongTroId }, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetOverlappingLivingMemberNamesAsync(IReadOnlyList<int> memberIds, CancellationToken cancellationToken = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .Include(x => x.HopDong)
                .Include(x => x.NguoiThue)
                .Where(x => memberIds.Contains(x.NguoiThueId) &&
                             x.NgayChuyenDi == null &&
                             !x.IsDeleted &&
                             x.HopDong.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong)
                .Select(x => x.NguoiThue.HoVaTen)
                .ToListAsync(cancellationToken);
        }

        public async Task<NguoiThue?> GetNguoiThueByIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiThues.FindAsync(new object[] { nguoiThueId }, cancellationToken);
        }

        public async Task<bool> ExistsActiveAccountForTenantAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.NguoiDungs.AnyAsync(u => u.NguoiThueId == nguoiThueId && !u.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermsByIdsAsync(IReadOnlyList<int> termIds, CancellationToken cancellationToken = default)
        {
            return await _context.DieuKhoanMaus
                .Where(x => termIds.Contains(x.DieuKhoanMauId) && !x.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasInvoicesAsync(int hopDongId, CancellationToken cancellationToken = default)
        {
            return await _context.HoaDons.AnyAsync(x => x.HopDongId == hopDongId && !x.IsDeleted, cancellationToken);
        }

        public async Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default)
        {
            return await _context.ChiTietThanhVienHopDongs
                .Where(x => x.HopDongId == hopDongId && x.NgayChuyenDi == null && !x.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<HopDongDieuKhoan>> GetHopDongDieuKhoansByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongDieuKhoans.Where(x => x.HopDongId == hopDongId).ToListAsync(cancellationToken);
        }

        public async Task<HopDongPrintRes?> GetPrintDataAsync(int id, CancellationToken cancellationToken = default)
        {
            var hopDong = await _context.HopDongs
                .Include(x => x.PhongTro)
                .ThenInclude(p => p.ChiNhanh)
                .Include(x => x.NguoiThue)
                .Include(x => x.HopDongDieuKhoans)
                .FirstOrDefaultAsync(x => x.HopDongId == id && !x.IsDeleted, cancellationToken);

            if (hopDong == null) return null;

            var dichVus = await _context.DangKyDichVus
                .Include(x => x.DichVuChiNhanh).ThenInclude(x => x.DichVu)
                .Where(x => x.PhongTroId == hopDong.PhongTroId)
                .Select(x => new HopDongDichVuPrintRes
                {
                    TenDichVu = x.DichVuChiNhanh.DichVu.TenDichVu,
                    DonGia = x.DichVuChiNhanh.GiaDichVu,
                    DonViTinh = x.DichVuChiNhanh.DichVu.DonVi
                })
                .ToListAsync(cancellationToken);

            return new HopDongPrintRes
            {
                HopDongId = hopDong.HopDongId,
                MaHopDong = hopDong.MaHopDong,
                ThoiDiemBatDau = hopDong.ThoiDiemBatDau,
                ThoiDiemKetThuc = hopDong.ThoiDiemKetThuc,
                TienCocPhong = hopDong.TienCocPhong,
                TienThuePhong = hopDong.TienThuePhong,
                NgayTao = hopDong.NgayTao,

                TenChiNhanh = hopDong.PhongTro.ChiNhanh.TenChiNhanh,
                DiaChiChiNhanh = hopDong.PhongTro.ChiNhanh.DiaChi,
                SoDienThoaiChiNhanh = hopDong.PhongTro.ChiNhanh.SoDienThoai,
                SoPhong = hopDong.PhongTro.SoPhong,

                HoVaTenNguoiThue = hopDong.NguoiThue.HoVaTen,
                SoDienThoaiNguoiThue = hopDong.NguoiThue.SoDienThoai,
                CCCDNguoiThue = hopDong.NguoiThue.CCCD,
                NgayCapCCCD = hopDong.NguoiThue.NgayCapCCCD,
                NoiCapCCCD = hopDong.NguoiThue.NoiCapCCCD,
                QueQuan = hopDong.NguoiThue.QueQuan,

                DichVus = dichVus,
                DieuKhoans = hopDong.HopDongDieuKhoans.OrderBy(x => x.ThuTu).Select(d => new HopDongDieuKhoanPrintRes
                {
                    TieuDe = d.TieuDe,
                    NoiDung = d.NoiDung,
                    ThuTu = d.ThuTu
                }).ToList()
            };
        }

        public async Task<IReadOnlyList<HopDong>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Include(x => x.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(x => x.NguoiThue)
                .Include(x => x.HopDongDieuKhoans)
                .Where(x => x.NguoiThueId == nguoiThueId && !x.IsDeleted)
                .OrderByDescending(x => x.NgayTao)
                .ToListAsync(cancellationToken);
        }

        public async Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId, CancellationToken cancellationToken = default)
        {
            var hopDong = await _context.HopDongs
                .Include(h => h.NguoiThue)
                .Include(h => h.PhongTro)
                .FirstOrDefaultAsync(h => h.HopDongId == id && h.NguoiThueId == nguoiThueId && !h.IsDeleted, cancellationToken);

            if (hopDong == null) return null;

            var members = await _context.ChiTietThanhVienHopDongs
                .Include(m => m.NguoiThue)
                .Where(m => m.HopDongId == id && !m.IsDeleted)
                .Select(m => new HopDongThanhVienKhachThueDto
                {
                    hoVaTen = m.NguoiThue.HoVaTen,
                    soDienThoai = m.NguoiThue.SoDienThoai,
                    cccd = m.NguoiThue.CCCD,
                    ngayVao = m.NgayVao.ToString("dd/MM/yyyy")
                }).ToListAsync(cancellationToken);

            var services = await _context.DangKyDichVus
                .Include(s => s.DichVuChiNhanh).ThenInclude(dcn => dcn.DichVu)
                .Where(s => s.PhongTroId == hopDong.PhongTroId)
                .Select(s => new HopDongDichVuKhachThueDto
                {
                    tenDichVu = s.DichVuChiNhanh.DichVu.TenDichVu,
                    donGia = s.DichVuChiNhanh.GiaDichVu,
                    donVi = s.DichVuChiNhanh.DichVu.DonVi,
                    soLuong = s.SoLuong
                }).ToListAsync(cancellationToken);

            return new HopDongKhachThueDetailDto
            {
                maHopDong = hopDong.MaHopDong,
                thoiDiemBatDau = hopDong.ThoiDiemBatDau.ToString("dd/MM/yyyy"),
                thoiDiemKetThuc = hopDong.ThoiDiemKetThuc?.ToString("dd/MM/yyyy") ?? "Không thời hạn",
                tienCocPhong = hopDong.TienCocPhong,
                tienThuePhong = hopDong.TienThuePhong,
                tenNguoiDaiDien = hopDong.NguoiThue?.HoVaTen,
                soDienThoaiDaiDien = hopDong.NguoiThue?.SoDienThoai,
                cccdDaiDien = hopDong.NguoiThue?.CCCD,
                members = members,
                services = services
            };
        }

        public async Task<IReadOnlyList<HopDong>> GetExpiredActiveContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Include(hd => hd.PhongTro)
                .Include(hd => hd.ChiTietThanhVienHopDongs)
                .Where(hd => hd.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong
                    && !hd.IsDeleted
                    && hd.ThoiDiemKetThuc != null
                    && hd.ThoiDiemKetThuc < nowUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesByRoomIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
        {
            return await _context.DangKyDichVus
                .Where(x => roomIds.Contains(x.PhongTroId) && x.NgayKetThuc == null)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<HopDong>> GetActiveExpiringContractsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.HopDongs
                .Include(hd => hd.NguoiThue)
                .Include(hd => hd.PhongTro)
                    .ThenInclude(p => p.ChiNhanh)
                .Where(hd => hd.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong
                    && !hd.IsDeleted
                    && hd.ThoiDiemKetThuc != null)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(HopDong hopDong, CancellationToken cancellationToken = default)
        {
            await _context.HopDongs.AddAsync(hopDong, cancellationToken);
        }

        public async Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken cancellationToken = default)
        {
            await _context.ChiTietThanhVienHopDongs.AddRangeAsync(members, cancellationToken);
        }

        public async Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken cancellationToken = default)
        {
            await _context.HopDongDieuKhoans.AddRangeAsync(terms, cancellationToken);
        }

        public async Task AddNguoiDungAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default)
        {
            await _context.NguoiDungs.AddAsync(nguoiDung, cancellationToken);
        }

        public void Update(HopDong hopDong)
        {
            _context.HopDongs.Update(hopDong);
        }

        public void UpdatePhongTro(PhongTro phongTro)
        {
            _context.PhongTros.Update(phongTro);
        }

        public void RemoveTerms(IEnumerable<HopDongDieuKhoan> terms)
        {
            _context.HopDongDieuKhoans.RemoveRange(terms);
        }
    }
}
