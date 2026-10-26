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

        public async Task<bool> ExistsMaGiaoDichAsync(string maGiaoDich, CancellationToken ct = default)
        {
            var upper = maGiaoDich.Trim().ToUpperInvariant();
            return await _context.LichSuThanhToans
                .AnyAsync(l => !l.IsDeleted && l.MaGiaoDich.ToUpper() == upper, ct);
        }

        public async Task<bool> ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(int hoaDonId, string maGiaoDich, CancellationToken ct = default)
        {
            var upper = maGiaoDich.Trim().ToUpperInvariant();
            return await _context.LichSuThanhToans
                .AnyAsync(l => l.HoaDonId == hoaDonId && l.MaGiaoDich.ToUpper() == upper, ct);
        }

        public async Task<bool> ExistsMaGiaoDichIncludingDeletedAsync(string maGiaoDich, CancellationToken ct = default)
        {
            var upper = maGiaoDich.Trim().ToUpperInvariant();
            return await _context.LichSuThanhToans
                .AnyAsync(l => l.MaGiaoDich.ToUpper() == upper, ct);
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
                    IsDeleted = h.IsDeleted,
                    Thang = h.Thang,
                    Nam = h.Nam,
                    HanThanhToan = h.HanThanhToan
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<PaymentLedgerRowSnapshot>> GetActiveLedgerRowsAsync(int hoaDonId, CancellationToken ct = default)
        {
            return await _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted)
                .OrderBy(l => l.NgayThanhToan)
                .ThenBy(l => l.LichSuThanhToanId)
                .Select(l => new PaymentLedgerRowSnapshot
                {
                    LichSuThanhToanId = l.LichSuThanhToanId,
                    SoTienThanhToan = l.SoTienThanhToan,
                    NgayThanhToanUtc = l.NgayThanhToan,
                    MaGiaoDich = l.MaGiaoDich,
                    PhuongThucThanhToan = l.PhuongThucThanhToan
                })
                .ToListAsync(ct);
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

        public async Task<string?> FindSystemSePayEntryRecordedAfterAsync(int hoaDonId, DateTime sinceUtc, CancellationToken ct = default)
        {
            var sePayPrefix = InvoicePaymentPolicy.SePayMaGiaoDichPrefix;
            var vnPayPrefix = InvoicePaymentPolicy.VnPayMaGiaoDichPrefix;
            return await _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => l.HoaDonId == hoaDonId &&
                            !l.IsDeleted &&
                            (l.MaGiaoDich.StartsWith(sePayPrefix) || l.MaGiaoDich.StartsWith(vnPayPrefix)) &&
                            l.NguoiXacNhanId == null &&
                            l.NgayXacNhan != null &&
                            l.NgayXacNhan >= sinceUtc)
                .OrderBy(l => l.NgayXacNhan)
                .ThenBy(l => l.LichSuThanhToanId)
                .Select(l => l.MaGiaoDich)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<PaymentReceiptSnapshot?> GetReceiptSnapshotAsync(int lichSuThanhToanId, CancellationToken ct = default)
        {
            var row = await _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => l.LichSuThanhToanId == lichSuThanhToanId)
                .Select(l => new
                {
                    l.LichSuThanhToanId,
                    l.HoaDonId,
                    l.HoaDon.MaHoaDon,
                    l.HoaDon.Thang,
                    l.HoaDon.Nam,
                    l.HoaDon.TongTien,
                    ChiNhanhId = l.HoaDon.HopDong.PhongTro.ChiNhanhId,
                    TenChiNhanh = l.HoaDon.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    DiaChiChiNhanh = l.HoaDon.HopDong.PhongTro.ChiNhanh.DiaChi,
                    TenPhong = l.HoaDon.HopDong.PhongTro.SoPhong,
                    TenKhachThue = l.HoaDon.HopDong.NguoiThue.HoVaTen,
                    EmailKhachThue = l.HoaDon.HopDong.NguoiThue.Email,
                    l.SoTienThanhToan,
                    l.NgayThanhToan,
                    l.MaGiaoDich,
                    l.PhuongThucThanhToan,
                    l.GhiChu,
                    l.IsDeleted
                })
                .FirstOrDefaultAsync(ct);

            if (row == null)
            {
                return null;
            }

            // Thứ tự lần trả và lũy kế chỉ tính dòng chưa xóa mềm, theo (NgayThanhToan, Id) không sau dòng này.
            var upToThisEntry = _context.LichSuThanhToans
                .AsNoTracking()
                .Where(l => l.HoaDonId == row.HoaDonId &&
                            !l.IsDeleted &&
                            (l.NgayThanhToan < row.NgayThanhToan ||
                             (l.NgayThanhToan == row.NgayThanhToan && l.LichSuThanhToanId <= row.LichSuThanhToanId)));
            var dotThu = await upToThisEntry.CountAsync(ct);
            var luyKe = await upToThisEntry.SumAsync(l => l.SoTienThanhToan, ct);

            return new PaymentReceiptSnapshot
            {
                LichSuThanhToanId = row.LichSuThanhToanId,
                HoaDonId = row.HoaDonId,
                MaHoaDon = row.MaHoaDon ?? string.Empty,
                ChiNhanhId = row.ChiNhanhId,
                TenChiNhanh = row.TenChiNhanh ?? string.Empty,
                DiaChiChiNhanh = row.DiaChiChiNhanh ?? string.Empty,
                TenPhong = row.TenPhong ?? string.Empty,
                Thang = row.Thang,
                Nam = row.Nam,
                TenKhachThue = row.TenKhachThue ?? string.Empty,
                EmailKhachThue = row.EmailKhachThue,
                TongTien = row.TongTien,
                SoTienThanhToan = row.SoTienThanhToan,
                NgayThanhToanUtc = row.NgayThanhToan,
                MaGiaoDich = row.MaGiaoDich ?? string.Empty,
                PhuongThucThanhToan = row.PhuongThucThanhToan,
                GhiChu = row.GhiChu ?? string.Empty,
                IsDeleted = row.IsDeleted,
                DotThu = dotThu,
                LuyKeDaThanhToan = luyKe
            };
        }

        public async Task<TransferAlertContext?> GetTransferAlertContextAsync(int hoaDonId, CancellationToken ct = default)
        {
            var invoice = await _context.HoaDons
                .AsNoTracking()
                .Where(h => h.HoaDonId == hoaDonId)
                .Select(h => new
                {
                    h.HoaDonId,
                    h.MaHoaDon,
                    h.HopDongId,
                    h.Thang,
                    h.Nam,
                    h.IsDeleted,
                    h.TrangThaiPhatHanh,
                    ChiNhanhId = h.HopDong.PhongTro.ChiNhanhId
                })
                .FirstOrDefaultAsync(ct);

            if (invoice == null)
            {
                return null;
            }

            // Hóa đơn đã hủy được lập lại bản "-R{n}" cùng hợp đồng và kỳ: báo mã bản thay thế để nhân viên chuyển khoản sang đúng hóa đơn.
            int? replacementId = null;
            string? replacementCode = null;
            if (invoice.IsDeleted || invoice.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                var replacement = await _context.HoaDons
                    .AsNoTracking()
                    .Where(h => h.HopDongId == invoice.HopDongId &&
                                h.Thang == invoice.Thang &&
                                h.Nam == invoice.Nam &&
                                h.HoaDonId != invoice.HoaDonId &&
                                !h.IsDeleted &&
                                h.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy)
                    .OrderByDescending(h => h.HoaDonId)
                    .Select(h => new { h.HoaDonId, h.MaHoaDon })
                    .FirstOrDefaultAsync(ct);
                replacementId = replacement?.HoaDonId;
                replacementCode = replacement?.MaHoaDon;
            }

            return new TransferAlertContext
            {
                HoaDonId = invoice.HoaDonId,
                MaHoaDon = invoice.MaHoaDon ?? string.Empty,
                ChiNhanhId = invoice.ChiNhanhId,
                HoaDonThayTheId = replacementId,
                MaHoaDonThayThe = replacementCode
            };
        }
    }
}
