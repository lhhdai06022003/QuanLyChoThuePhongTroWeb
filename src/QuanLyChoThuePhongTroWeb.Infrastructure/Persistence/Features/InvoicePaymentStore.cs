using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class InvoicePaymentStore : IInvoicePaymentStore
    {
        private readonly ApplicationDbContext _context;

        public InvoicePaymentStore(ApplicationDbContext context)
        {
            _context = context;
        }

        // ----- Ghi dưới khóa -----

        public async Task<HoaDon?> LockHoaDonAsync(int hoaDonId, CancellationToken ct = default)
        {
            _context.ChangeTracker.Clear();

            return await _context.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {hoaDonId} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<YeuCauThanhToanHoaDon>> GetExpiredWaitingRequestsAsync(int hoaDonId, DateTime nowUtc, CancellationToken ct = default)
        {
            return await _context.YeuCauThanhToanHoaDons
                .Where(y => y.HoaDonId == hoaDonId)
                .Where(YeuCauThanhToanHoaDon.QuaHanChuaGhiNhan(nowUtc))
                .ToListAsync(ct);
        }

        public async Task<YeuCauThanhToanHoaDon?> GetActiveRequestAsync(int hoaDonId, DateTime nowUtc, CancellationToken ct = default)
        {
            return await _context.YeuCauThanhToanHoaDons
                .Where(y => y.HoaDonId == hoaDonId)
                .Where(YeuCauThanhToanHoaDon.DangHoatDong(nowUtc))
                .OrderByDescending(y => y.NgayTao)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<YeuCauThanhToanHoaDon?> GetRequestAsync(int yeuCauId, CancellationToken ct = default)
        {
            return await _context.YeuCauThanhToanHoaDons
                .Include(y => y.MinhChungThanhToanHoaDons)
                .Include(y => y.LichSuTrangThaiYeuCauThanhToanHoaDons)
                .FirstOrDefaultAsync(y => y.YeuCauThanhToanHoaDonId == yeuCauId && !y.IsDeleted, ct);
        }

        public async Task<MinhChungThanhToanHoaDon?> GetProofAsync(int minhChungId, CancellationToken ct = default)
        {
            return await _context.MinhChungThanhToanHoaDons
                .Include(m => m.YeuCauThanhToanHoaDon)
                    .ThenInclude(y => y.LichSuTrangThaiYeuCauThanhToanHoaDons)
                .FirstOrDefaultAsync(m => m.MinhChungThanhToanHoaDonId == minhChungId && !m.IsDeleted && !m.YeuCauThanhToanHoaDon.IsDeleted, ct);
        }

        public async Task<LichSuThanhToan?> GetLedgerEntryAsync(int lichSuThanhToanId, CancellationToken ct = default)
        {
            return await _context.LichSuThanhToans
                .FirstOrDefaultAsync(l => l.LichSuThanhToanId == lichSuThanhToanId, ct);
        }

        public async Task<decimal> GetTongTienDaThanhToanHoaDonAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.LichSuThanhToans
                .Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted)
                .SumAsync(l => l.SoTienThanhToan, ct);
        }

        public async Task<bool> ExistsMaYeuCauAsync(string maYeuCau, CancellationToken ct = default)
        {
            return await _context.YeuCauThanhToanHoaDons.AnyAsync(y => y.MaYeuCau == maYeuCau, ct);
        }

        public async Task<bool> ExistsMaGiaoDichAsync(string maGiaoDich, CancellationToken ct = default)
        {
            var upper = maGiaoDich.Trim().ToUpperInvariant();
            return await _context.LichSuThanhToans
                .AnyAsync(l => !l.IsDeleted && l.MaGiaoDich.ToUpper() == upper, ct);
        }

        public async Task<bool> ExistsPendingProofMaGiaoDichAsync(string maGiaoDich, int? exceptMinhChungId, CancellationToken ct = default)
        {
            var upper = maGiaoDich.Trim().ToUpperInvariant();
            return await _context.MinhChungThanhToanHoaDons
                .AnyAsync(m => !m.IsDeleted &&
                               m.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.ChoXacNhan &&
                               m.MaGiaoDichNganHang != null &&
                               m.MaGiaoDichNganHang.ToUpper() == upper &&
                               (exceptMinhChungId == null || m.MinhChungThanhToanHoaDonId != exceptMinhChungId), ct);
        }

        public void AddRequest(YeuCauThanhToanHoaDon yeuCau)
        {
            _context.YeuCauThanhToanHoaDons.Add(yeuCau);
        }

        public void AddLedgerEntry(LichSuThanhToan lichSu)
        {
            _context.LichSuThanhToans.Add(lichSu);
        }

        public void AddNotification(ThongBao thongBao)
        {
            _context.ThongBaos.Add(thongBao);
        }

        // ----- Người nhận thông báo -----

        public async Task<IReadOnlyList<int>> GetAdminUserIdsAsync(CancellationToken ct = default)
        {
            return await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.Role == Role.Admin && u.IsActive && !u.IsDeleted)
                .Select(u => u.NguoiDungId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<int>> GetReviewerUserIdsAsync(int chiNhanhId, CancellationToken ct = default)
        {
            return await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.IsActive && !u.IsDeleted &&
                            (u.Role == Role.Admin ||
                             (u.Role == Role.NhanVien &&
                              u.NhanVienChiNhanhs.Any(nv => nv.ChiNhanhId == chiNhanhId && nv.IsActive && nv.NgayThuHoi == null))))
                .Select(u => u.NguoiDungId)
                .ToListAsync(ct);
        }

        public async Task<int?> GetTenantUserIdAsync(int hopDongId, CancellationToken ct = default)
        {
            var nguoiThueId = await _context.HopDongs
                .AsNoTracking()
                .Where(h => h.HopDongId == hopDongId)
                .Select(h => (int?)h.NguoiThueId)
                .FirstOrDefaultAsync(ct);

            if (!nguoiThueId.HasValue)
            {
                return null;
            }

            return await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.NguoiThueId == nguoiThueId && !u.IsDeleted && u.IsActive)
                .OrderBy(u => u.NguoiDungId)
                .Select(u => (int?)u.NguoiDungId)
                .FirstOrDefaultAsync(ct);
        }

        // ----- Đọc trước khóa và read model -----

        private static readonly Expression<Func<HoaDon, InvoicePaymentTarget>> TargetProjection = h => new InvoicePaymentTarget
        {
            HoaDonId = h.HoaDonId,
            MaHoaDon = h.MaHoaDon,
            HopDongId = h.HopDongId,
            NguoiThueId = h.HopDong.NguoiThueId,
            ChiNhanhId = h.HopDong.PhongTro.ChiNhanhId,
            IsDeleted = h.IsDeleted,
            TrangThaiPhatHanh = h.TrangThaiPhatHanh,
            NgayGui = h.NgayGui
        };

        public async Task<InvoicePaymentTarget?> GetTargetByHoaDonAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .AsNoTracking()
                .Where(h => h.HoaDonId == hoaDonId)
                .Select(TargetProjection)
                .FirstOrDefaultAsync(ct);
        }

        public Task<InvoicePaymentTarget?> GetTargetByYeuCauAsync(int yeuCauId, CancellationToken ct = default)
        {
            return GetTargetByRequestAsync(y => y.YeuCauThanhToanHoaDonId == yeuCauId, ct);
        }

        public Task<InvoicePaymentTarget?> GetTargetByMinhChungAsync(int minhChungId, CancellationToken ct = default)
        {
            return GetTargetByRequestAsync(
                y => y.MinhChungThanhToanHoaDons.Any(m => m.MinhChungThanhToanHoaDonId == minhChungId && !m.IsDeleted), ct);
        }

        public Task<InvoicePaymentTarget?> GetTargetByMaYeuCauAsync(string maYeuCau, CancellationToken ct = default)
        {
            var normalized = new string((maYeuCau ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
            return GetTargetByRequestAsync(y => y.MaYeuCau.ToUpper() == normalized, ct);
        }

        public async Task<InvoicePaymentTarget?> GetTargetByLedgerEntryAsync(int lichSuThanhToanId, CancellationToken ct = default)
        {
            var hoaDonId = await _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => l.LichSuThanhToanId == lichSuThanhToanId)
                .Select(l => (int?)l.HoaDonId)
                .FirstOrDefaultAsync(ct);

            return hoaDonId.HasValue ? await GetTargetByHoaDonAsync(hoaDonId.Value, ct) : null;
        }

        private async Task<InvoicePaymentTarget?> GetTargetByRequestAsync(Expression<Func<YeuCauThanhToanHoaDon, bool>> predicate, CancellationToken ct)
        {
            var request = await _context.YeuCauThanhToanHoaDons
                .AsNoTracking()
                .Where(y => !y.IsDeleted)
                .Where(predicate)
                .Select(y => new { y.YeuCauThanhToanHoaDonId, y.MaYeuCau, y.HoaDonId })
                .FirstOrDefaultAsync(ct);

            if (request == null)
            {
                return null;
            }

            var target = await GetTargetByHoaDonAsync(request.HoaDonId, ct);
            if (target == null)
            {
                return null;
            }

            return new InvoicePaymentTarget
            {
                HoaDonId = target.HoaDonId,
                MaHoaDon = target.MaHoaDon,
                HopDongId = target.HopDongId,
                NguoiThueId = target.NguoiThueId,
                ChiNhanhId = target.ChiNhanhId,
                IsDeleted = target.IsDeleted,
                TrangThaiPhatHanh = target.TrangThaiPhatHanh,
                NgayGui = target.NgayGui,
                YeuCauId = request.YeuCauThanhToanHoaDonId,
                MaYeuCau = request.MaYeuCau
            };
        }

        public async Task<PaymentInvoiceSnapshot?> GetInvoiceSnapshotAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.HoaDons
                .AsNoTracking()
                .Where(h => h.HoaDonId == hoaDonId)
                .Select(h => new PaymentInvoiceSnapshot
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    TenPhong = h.HopDong.PhongTro.SoPhong,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    TenKhachThue = h.HopDong.NguoiThue.HoVaTen,
                    TongTien = h.TongTien,
                    DaThu = h.LichSuThanhToans.Where(l => !l.IsDeleted).Sum(l => l.SoTienThanhToan),
                    ChoPhepThanhToanMotPhan = h.ChoPhepThanhToanMotPhan,
                    SoTienThanhToanToiThieu = h.SoTienThanhToanToiThieu,
                    TrangThaiHoaDon = h.TrangThaiHoaDon,
                    TrangThaiPhatHanh = h.TrangThaiPhatHanh,
                    IsDeleted = h.IsDeleted
                })
                .FirstOrDefaultAsync(ct);
        }

        public Task<IReadOnlyList<PaymentRequestSnapshot>> GetRequestSnapshotsAsync(int hoaDonId, CancellationToken ct = default)
        {
            return LoadRequestSnapshotsAsync(y => y.HoaDonId == hoaDonId, ct);
        }

        public async Task<PaymentRequestSnapshot?> GetRequestSnapshotAsync(int yeuCauId, CancellationToken ct = default)
        {
            var list = await LoadRequestSnapshotsAsync(y => y.YeuCauThanhToanHoaDonId == yeuCauId, ct);
            return list.FirstOrDefault();
        }

        private async Task<IReadOnlyList<PaymentRequestSnapshot>> LoadRequestSnapshotsAsync(
            Expression<Func<YeuCauThanhToanHoaDon, bool>> predicate,
            CancellationToken ct)
        {
            var raw = await _context.YeuCauThanhToanHoaDons
                .AsNoTracking()
                .Where(y => !y.IsDeleted)
                .Where(predicate)
                .OrderByDescending(y => y.NgayTao)
                .ThenByDescending(y => y.YeuCauThanhToanHoaDonId)
                .Select(y => new
                {
                    y.YeuCauThanhToanHoaDonId,
                    y.HoaDonId,
                    y.MaYeuCau,
                    y.SoTien,
                    y.NoiDungChuyenKhoan,
                    y.HanThanhToan,
                    y.NgayTao,
                    y.TrangThai,
                    MinhChungs = y.MinhChungThanhToanHoaDons
                        .Where(m => !m.IsDeleted)
                        .Select(m => new PaymentProofSnapshot
                        {
                            MinhChungId = m.MinhChungThanhToanHoaDonId,
                            HinhAnhUrl = m.HinhAnhUrl,
                            PublicId = m.PublicId,
                            NgayTao = m.NgayTao,
                            NgayChuyenKhaiBao = m.NgayChuyenKhaiBao,
                            SoTienKhaiBao = m.SoTienKhaiBao,
                            MaGiaoDichNganHang = m.MaGiaoDichNganHang,
                            TrangThai = m.TrangThaiDoiChieu,
                            LyDoTuChoi = m.LyDoTuChoi,
                            NgayDoiChieu = m.NgayDoiChieu,
                            GhiNhanBiHuy = m.LichSuThanhToan != null && m.LichSuThanhToan.IsDeleted
                        })
                        .ToList(),
                    LichSu = y.LichSuTrangThaiYeuCauThanhToanHoaDons
                        .OrderBy(l => l.NgayThucHien)
                        .ThenBy(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
                        .Select(l => new
                        {
                            l.LichSuTrangThaiYeuCauThanhToanHoaDonId,
                            l.TrangThaiCu,
                            l.TrangThaiMoi,
                            l.NguoiThucHienId,
                            l.NgayThucHien,
                            l.LyDo
                        })
                        .ToList()
                })
                .ToListAsync(ct);

            var userIds = raw
                .SelectMany(y => y.LichSu)
                .Where(l => l.NguoiThucHienId.HasValue)
                .Select(l => l.NguoiThucHienId!.Value)
                .Distinct()
                .ToList();

            var names = userIds.Count == 0
                ? new Dictionary<int, string>()
                : await _context.NguoiDungs
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.NguoiDungId))
                    .ToDictionaryAsync(u => u.NguoiDungId, u => u.TenDangNhap, ct);

            return raw.Select(y => new PaymentRequestSnapshot
            {
                YeuCauId = y.YeuCauThanhToanHoaDonId,
                HoaDonId = y.HoaDonId,
                MaYeuCau = y.MaYeuCau,
                SoTien = y.SoTien,
                NoiDungChuyenKhoan = y.NoiDungChuyenKhoan,
                HanThanhToan = y.HanThanhToan,
                NgayTao = y.NgayTao,
                TrangThai = y.TrangThai,
                MinhChungs = y.MinhChungs,
                LichSu = y.LichSu.Select(l => new PaymentHistorySnapshot
                {
                    Id = l.LichSuTrangThaiYeuCauThanhToanHoaDonId,
                    TrangThaiCu = l.TrangThaiCu,
                    TrangThaiMoi = l.TrangThaiMoi,
                    NguoiThucHien = l.NguoiThucHienId.HasValue && names.TryGetValue(l.NguoiThucHienId.Value, out var name) ? name : null,
                    NgayThucHien = l.NgayThucHien,
                    LyDo = l.LyDo
                }).ToList()
            }).ToList();
        }

        public async Task<ReviewQueuePage> GetReviewQueueAsync(ReviewQueueQuery query, CancellationToken ct = default)
        {
            var proofs = _context.MinhChungThanhToanHoaDons
                .AsNoTracking()
                .Where(m => !m.IsDeleted && !m.YeuCauThanhToanHoaDon.IsDeleted);

            proofs = query.DaXuLy
                ? proofs.Where(m => m.TrangThaiDoiChieu != TrangThaiMinhChungThanhToan.ChoXacNhan)
                : proofs.Where(m => m.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.ChoXacNhan);

            if (query.ChiNhanhId > 0)
            {
                proofs = proofs.Where(m => m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.ChiNhanhId == query.ChiNhanhId);
            }

            if (query.AllowedBranchIds != null)
            {
                var allowed = query.AllowedBranchIds;
                proofs = proofs.Where(m => allowed.Contains(m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.ChiNhanhId));
            }

            if (!string.IsNullOrWhiteSpace(query.SearchValue))
            {
                var s = query.SearchValue.Trim().ToLower();
                proofs = proofs.Where(m =>
                    m.YeuCauThanhToanHoaDon.MaYeuCau.ToLower().Contains(s) ||
                    m.YeuCauThanhToanHoaDon.HoaDon.MaHoaDon.ToLower().Contains(s) ||
                    m.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.SoPhong.ToLower().Contains(s) ||
                    m.YeuCauThanhToanHoaDon.HoaDon.HopDong.NguoiThue.HoVaTen.ToLower().Contains(s) ||
                    (m.MaGiaoDichNganHang != null && m.MaGiaoDichNganHang.ToLower().Contains(s)));
            }

            var choAdminPrefix = PaymentNoteTags.ChoAdminPrefix;
            var rows = proofs.Select(m => new
            {
                Proof = m,
                ChoAdmin = m.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.ChoXacNhan &&
                    m.YeuCauThanhToanHoaDon.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu &&
                    m.YeuCauThanhToanHoaDon.LichSuTrangThaiYeuCauThanhToanHoaDons
                        .OrderByDescending(l => l.NgayThucHien)
                        .ThenByDescending(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
                        .Select(l => l.LyDo)
                        .FirstOrDefault()!
                        .StartsWith(choAdminPrefix)
            });

            if (query.ChiChoAdmin)
            {
                rows = rows.Where(x => x.ChoAdmin);
            }

            var total = await rows.CountAsync(ct);

            rows = query.DaXuLy
                ? rows.OrderByDescending(x => x.Proof.NgayDoiChieu).ThenByDescending(x => x.Proof.MinhChungThanhToanHoaDonId)
                : rows.OrderBy(x => x.Proof.NgayTao).ThenBy(x => x.Proof.MinhChungThanhToanHoaDonId);

            var page = await rows
                .Skip(query.Start)
                .Take(query.Length)
                .Select(x => new ReviewQueueRowSnapshot
                {
                    MinhChungId = x.Proof.MinhChungThanhToanHoaDonId,
                    YeuCauId = x.Proof.YeuCauThanhToanHoaDonId,
                    MaYeuCau = x.Proof.YeuCauThanhToanHoaDon.MaYeuCau,
                    HoaDonId = x.Proof.YeuCauThanhToanHoaDon.HoaDonId,
                    MaHoaDon = x.Proof.YeuCauThanhToanHoaDon.HoaDon.MaHoaDon,
                    TenPhong = x.Proof.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.SoPhong,
                    TenChiNhanh = x.Proof.YeuCauThanhToanHoaDon.HoaDon.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    TenKhachThue = x.Proof.YeuCauThanhToanHoaDon.HoaDon.HopDong.NguoiThue.HoVaTen,
                    SoTienLuot = x.Proof.YeuCauThanhToanHoaDon.SoTien,
                    MaGiaoDichKhaiBao = x.Proof.MaGiaoDichNganHang,
                    NgayChuyenKhaiBao = x.Proof.NgayChuyenKhaiBao,
                    NgayNop = x.Proof.NgayTao,
                    TrangThai = x.Proof.TrangThaiDoiChieu,
                    ChoAdmin = x.ChoAdmin
                })
                .ToListAsync(ct);

            return new ReviewQueuePage { Total = total, Rows = page };
        }
    }
}
