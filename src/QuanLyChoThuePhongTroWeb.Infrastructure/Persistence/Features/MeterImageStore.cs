using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class MeterImageStore : IMeterImageStore
    {
        private readonly ApplicationDbContext _context;

        public MeterImageStore(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<int?> GetBranchIdByRoomAsync(int phongTroId, CancellationToken cancellationToken = default)
        {
            return await _context.PhongTros
                .Where(p => p.PhongTroId == phongTroId && !p.IsDeleted)
                .Select(p => (int?)p.ChiNhanhId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> HasActiveContractForTenantInPeriodAsync(int phongTroId, int tenantUserId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var user = await _context.NguoiDungs
                .Include(u => u.NguoiThue)
                .Where(u => u.NguoiDungId == tenantUserId &&
                            !u.IsDeleted &&
                            u.IsActive &&
                            u.Role == Role.KhachThue &&
                            u.NguoiThueId.HasValue &&
                            u.NguoiThue != null &&
                            !u.NguoiThue.IsDeleted)
                .Select(u => new { u.NguoiThueId })
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null || !user.NguoiThueId.HasValue)
            {
                return false;
            }

            var nguoiThueId = user.NguoiThueId.Value;
            var (startUtc, endExclusiveUtc) = QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services.MeterPeriodPolicy.MonthRangeUtc(
                new QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services.MeterPeriod(thang, nam));

            return await _context.HopDongs
                .Where(h => h.PhongTroId == phongTroId &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau < endExclusiveUtc &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= startUtc))
                .AnyAsync(h => h.NguoiThueId == nguoiThueId ||
                               h.ChiTietThanhVienHopDongs.Any(tv => !tv.IsDeleted && tv.NguoiThueId == nguoiThueId),
                          cancellationToken);
        }

        public async Task<bool> HasContractInMonthAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var (startUtc, endExclusiveUtc) = QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services.MeterPeriodPolicy.MonthRangeUtc(
                new QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services.MeterPeriod(thang, nam));

            return await _context.HopDongs
                .Where(h => h.PhongTroId == phongTroId &&
                            !h.IsDeleted &&
                            h.TrangThaiHopDong != TrangThaiHopDong.DaHuy &&
                            h.ThoiDiemBatDau < endExclusiveUtc &&
                            (h.ThoiDiemKetThuc == null || h.ThoiDiemKetThuc.Value >= startUtc))
                .AnyAsync(cancellationToken);
        }

        public async Task<MeterImageAccessContext?> GetImageAccessContextAsync(int imageId, CancellationToken cancellationToken = default)
        {
            return await _context.AnhChiSoDongHos
                .AsNoTracking()
                .Where(a => a.AnhChiSoDongHoId == imageId)
                .Select(a => new MeterImageAccessContext
                {
                    AnhChiSoDongHoId = a.AnhChiSoDongHoId,
                    DichVuDienNuocCuaPhongId = a.DichVuDienNuocCuaPhongId,
                    PhongTroId = a.DichVuDienNuocCuaPhong.PhongTroId,
                    ChiNhanhId = a.DichVuDienNuocCuaPhong.PhongTro.ChiNhanhId,
                    TrangThaiXuLy = a.TrangThaiXuLy,
                    LoaiDongHo = a.LoaiDongHo,
                    DuocChonLamChiSoChinhThuc = a.DuocChonLamChiSoChinhThuc,
                    IsDeleted = a.IsDeleted
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<int, string>> GetRoomNumbersAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
        {
            if (roomIds.Count == 0) return new Dictionary<int, string>();

            var ids = roomIds.Distinct().ToList();
            return await _context.PhongTros
                .AsNoTracking()
                .Where(p => ids.Contains(p.PhongTroId))
                .ToDictionaryAsync(p => p.PhongTroId, p => p.SoPhong, cancellationToken);
        }

        public async Task LockRoomsAsync(IEnumerable<int> roomIds, CancellationToken cancellationToken = default)
        {
            var ids = roomIds.Distinct().OrderBy(id => id).ToArray();
            if (ids.Length == 0) return;

            var lockedRooms = await _context.PhongTros
                .FromSqlInterpolated($"SELECT * FROM phong_tro WHERE \"PhongTroId\" = ANY({ids}) AND \"IsDeleted\" = false ORDER BY \"PhongTroId\" FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (lockedRooms.Count != ids.Length)
            {
                throw new InvalidOperationException("Phòng không tồn tại hoặc đã bị xóa.");
            }
        }

        public async Task<DichVuDienNuocCuaPhong?> GetPeriodRecordAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .Include(d => d.AnhChiSoDongHos)
                .FirstOrDefaultAsync(d => d.PhongTroId == phongTroId && d.Thang == thang && d.Nam == nam && !d.IsDeleted, cancellationToken);
        }

        public Task<DichVuDienNuocCuaPhong?> GetPeriodRecordWithLockAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return GetPeriodForUpdateAsync(phongTroId, thang, nam, cancellationToken);
        }

        public async Task<DichVuDienNuocCuaPhong?> GetPeriodForUpdateAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .FromSqlInterpolated($"SELECT * FROM dich_vu_dien_nuoc_cua_phong WHERE \"PhongTroId\" = {phongTroId} AND \"Thang\" = {thang} AND \"Nam\" = {nam} AND \"IsDeleted\" = false FOR UPDATE")
                .Include(d => d.AnhChiSoDongHos)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<DichVuDienNuocCuaPhong?> GetPeriodRecordByIdAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .Include(d => d.AnhChiSoDongHos)
                .FirstOrDefaultAsync(d => d.DichVuDienNuocCuaPhongId == periodRecordId && !d.IsDeleted, cancellationToken);
        }

        public Task<DichVuDienNuocCuaPhong?> GetPeriodRecordByIdWithLockAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            return GetPeriodByIdForUpdateAsync(periodRecordId, cancellationToken);
        }

        public async Task<DichVuDienNuocCuaPhong?> GetPeriodByIdForUpdateAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            // V10: Detach thực thể nếu đã được ChangeTracker nạp trước đó trong cùng DbContext scope,
            // nhằm đảm bảo câu lệnh SQL FOR UPDATE đọc được bản ghi tươi mới nhất từ PostgreSQL.
            // Bất biến bắt buộc: luôn khóa kỳ trước ảnh để tránh xung đột tracking quan hệ phụ thuộc.
            var existing = _context.DichVuDienNuocCuaPhongs.Local.FirstOrDefault(p => p.DichVuDienNuocCuaPhongId == periodRecordId);
            if (existing != null)
            {
                _context.Entry(existing).State = EntityState.Detached;
            }

            return await _context.DichVuDienNuocCuaPhongs
                .FromSqlInterpolated($"SELECT * FROM dich_vu_dien_nuoc_cua_phong WHERE \"DichVuDienNuocCuaPhongId\" = {periodRecordId} AND \"IsDeleted\" = false FOR UPDATE")
                .Include(d => d.AnhChiSoDongHos)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<AnhChiSoDongHo?> GetImageByIdAsync(int imageId, CancellationToken cancellationToken = default)
        {
            return await _context.AnhChiSoDongHos
                .Include(a => a.DichVuDienNuocCuaPhong)
                .FirstOrDefaultAsync(a => a.AnhChiSoDongHoId == imageId && !a.IsDeleted, cancellationToken);
        }

        public Task<AnhChiSoDongHo?> GetImageByIdWithLockAsync(int imageId, CancellationToken cancellationToken = default)
        {
            return GetImageForUpdateAsync(imageId, cancellationToken);
        }

        public async Task<AnhChiSoDongHo?> GetImageForUpdateAsync(int imageId, CancellationToken cancellationToken = default)
        {
            // V10: Detach thực thể nếu đã được ChangeTracker nạp trước đó trong cùng DbContext scope,
            // nhằm đảm bảo câu lệnh SQL FOR UPDATE đọc được bản ghi tươi mới nhất từ PostgreSQL.
            // Bất biến bắt buộc: gọi sau khi đã khóa kỳ theo đúng thứ tự phòng -> kỳ -> ảnh.
            var existing = _context.AnhChiSoDongHos.Local.FirstOrDefault(a => a.AnhChiSoDongHoId == imageId);
            if (existing != null)
            {
                _context.Entry(existing).State = EntityState.Detached;
            }

            return await _context.AnhChiSoDongHos
                .FromSqlInterpolated($"SELECT * FROM anh_chi_so_dong_ho WHERE \"AnhChiSoDongHoId\" = {imageId} AND \"IsDeleted\" = false FOR UPDATE")
                .Include(a => a.DichVuDienNuocCuaPhong)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesByPeriodAndTypeAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
        {
            return await _context.AnhChiSoDongHos
                .Where(a => a.DichVuDienNuocCuaPhongId == periodRecordId && a.LoaiDongHo == loaiDongHo && !a.IsDeleted)
                .OrderByDescending(a => a.NgayGui)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesForUpdateAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
        {
            var loai = (int)loaiDongHo;
            return await _context.AnhChiSoDongHos
                .FromSqlInterpolated($"SELECT * FROM anh_chi_so_dong_ho WHERE \"DichVuDienNuocCuaPhongId\" = {periodRecordId} AND \"LoaiDongHo\" = {loai} AND \"IsDeleted\" = false ORDER BY \"AnhChiSoDongHoId\" FOR UPDATE")
                .ToListAsync(cancellationToken);
        }

        public async Task<AnhChiSoDongHo?> GetOfficialImageAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
        {
            return await _context.AnhChiSoDongHos
                .FirstOrDefaultAsync(a => a.DichVuDienNuocCuaPhongId == periodRecordId && a.LoaiDongHo == loaiDongHo && a.DuocChonLamChiSoChinhThuc && !a.IsDeleted, cancellationToken);
        }

        public async Task<bool> HasSubsequentPeriodAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            // Kỳ sau chỉ khóa kỳ này khi đã chốt (DaDuyet) hoặc đã có hóa đơn phát hành.
            // Kỳ sau còn là bản nháp thì được phép sửa kỳ này, chỉ số cũ kỳ sau sẽ được cập nhật theo.
            return await _context.DichVuDienNuocCuaPhongs
                .AnyAsync(d => d.PhongTroId == phongTroId && !d.IsDeleted &&
                               (d.Nam > nam || (d.Nam == nam && d.Thang > thang)) &&
                               (d.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet ||
                                _context.HoaDons.IgnoreQueryFilters().Any(h => h.DichVuDienNuocCuaPhongId == d.DichVuDienNuocCuaPhongId &&
                                                                               h.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap &&
                                                                               h.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy)),
                               cancellationToken);
        }

        public async Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetSubsequentPeriodsForUpdateAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            return await _context.DichVuDienNuocCuaPhongs
                .FromSqlInterpolated($"SELECT * FROM dich_vu_dien_nuoc_cua_phong WHERE \"PhongTroId\" = {phongTroId} AND \"IsDeleted\" = false AND (\"Nam\" > {nam} OR (\"Nam\" = {nam} AND \"Thang\" > {thang})) ORDER BY \"Nam\", \"Thang\" FOR UPDATE")
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasLockedInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default)
        {
            // Bản đã hủy không còn khóa kỳ: nhân viên phải sửa được chỉ số rồi tạo bản thay thế (-R1, -R2).
            return await _context.HoaDons
                .IgnoreQueryFilters()
                .AnyAsync(h => h.DichVuDienNuocCuaPhongId == periodRecordId &&
                               h.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap &&
                               h.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy, cancellationToken);
        }

        public Task<bool> HasNonDraftInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default)
            => HasLockedInvoiceAsync(periodRecordId, cancellationToken);

        public async Task<(decimal ChiSoDienMoi, decimal ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
        {
            var record = await _context.DichVuDienNuocCuaPhongs
                .Where(x => x.PhongTroId == phongTroId && !x.IsDeleted &&
                            (x.Nam < nam || (x.Nam == nam && x.Thang < thang)))
                .OrderByDescending(x => x.Nam)
                .ThenByDescending(x => x.Thang)
                .FirstOrDefaultAsync(cancellationToken);

            return record != null ? (record.ChiSoDienMoi, record.ChiSoNuocMoi) : (0m, 0m);
        }

        public async Task<decimal> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            var keyword = serviceKeyword.ToLower().Trim();
            var loai = keyword.Contains("điện") || keyword.Contains("dien") ? LoaiDichVu.Dien :
                       keyword.Contains("nước") || keyword.Contains("nuoc") ? LoaiDichVu.Nuoc : (LoaiDichVu?)null;

            var query = from bg in _context.DichVuChiNhanhs
                        join dv in _context.DichVus on bg.DichVuId equals dv.DichVuId
                        where bg.ChiNhanhId == chiNhanhId && !bg.IsDeleted && !dv.IsDeleted
                        where (loai.HasValue && dv.LoaiDichVu == loai.Value) || EF.Functions.ILike(dv.TenDichVu, $"%{keyword}%")
                        select (decimal?)bg.GiaDichVu;

            return await query.FirstOrDefaultAsync(cancellationToken) ?? 0m;
        }

        public async Task AddPeriodRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default)
        {
            await _context.DichVuDienNuocCuaPhongs.AddAsync(record, cancellationToken);
        }

        public void UpdatePeriodRecord(DichVuDienNuocCuaPhong record)
        {
            _context.DichVuDienNuocCuaPhongs.Update(record);
        }

        public async Task AddImageAsync(AnhChiSoDongHo image, CancellationToken cancellationToken = default)
        {
            await _context.AnhChiSoDongHos.AddAsync(image, cancellationToken);
        }

        public void UpdateImage(AnhChiSoDongHo image)
        {
            _context.AnhChiSoDongHos.Update(image);
        }
    }
}
