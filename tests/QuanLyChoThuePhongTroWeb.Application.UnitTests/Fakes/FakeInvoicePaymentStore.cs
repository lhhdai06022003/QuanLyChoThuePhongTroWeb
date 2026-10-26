using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Store thanh toán trong bộ nhớ. Entity được sửa trực tiếp nên rollback không hoàn tác dữ liệu;
    // test kiểm commit/rollback qua RecordingUnitOfWork. Id được gán khi SaveChanges (AssignIds).
    public class FakeInvoicePaymentStore : IInvoicePaymentStore
    {
        public sealed record InvoiceMeta(
            int ChiNhanhId, int NguoiThueId, string TenPhong = "P101", string TenChiNhanh = "CN1", string TenKhachThue = "Nguyễn Văn A",
            string DiaChiChiNhanh = "1 Đường A", string? EmailKhachThue = "khach@test.vn", int Thang = 9, int Nam = 2026);

        private int _nextId = 1000;

        public Dictionary<int, HoaDon> Invoices { get; } = new();
        public Dictionary<int, InvoiceMeta> Meta { get; } = new();
        public List<YeuCauThanhToanHoaDon> Requests { get; } = new();
        public List<LichSuThanhToan> Ledger { get; } = new();
        public List<ThongBao> Notifications { get; } = new();
        public List<int> AdminUserIds { get; } = new();
        public Dictionary<int, List<int>> StaffUserIdsByBranch { get; } = new();
        public Dictionary<int, int> TenantUserIdByHopDong { get; } = new();
        public Dictionary<int, string> UserNames { get; } = new();

        public int LockCalls { get; private set; }
        public Func<bool>? InTransaction { get; set; }
        public bool LockedOutsideTransaction { get; private set; }
        // Mô phỏng thay đổi đồng thời ngay lúc lấy được khóa.
        public Action<HoaDon>? OnLock { get; set; }

        public IEnumerable<MinhChungThanhToanHoaDon> Proofs => Requests.SelectMany(r => r.MinhChungThanhToanHoaDons);

        public void AssignIds()
        {
            foreach (var request in Requests)
            {
                if (request.YeuCauThanhToanHoaDonId == 0)
                {
                    request.YeuCauThanhToanHoaDonId = ++_nextId;
                }

                foreach (var proof in request.MinhChungThanhToanHoaDons)
                {
                    proof.YeuCauThanhToanHoaDon = request;
                    proof.YeuCauThanhToanHoaDonId = request.YeuCauThanhToanHoaDonId;
                    if (proof.MinhChungThanhToanHoaDonId == 0)
                    {
                        proof.MinhChungThanhToanHoaDonId = ++_nextId;
                    }
                }

                foreach (var history in request.LichSuTrangThaiYeuCauThanhToanHoaDons)
                {
                    history.YeuCauThanhToanHoaDonId = request.YeuCauThanhToanHoaDonId;
                    if (history.LichSuTrangThaiYeuCauThanhToanHoaDonId == 0)
                    {
                        history.LichSuTrangThaiYeuCauThanhToanHoaDonId = ++_nextId;
                    }
                }
            }

            foreach (var ledger in Ledger.Where(l => l.LichSuThanhToanId == 0))
            {
                ledger.LichSuThanhToanId = ++_nextId;
                var proof = Proofs.FirstOrDefault(p => p.MinhChungThanhToanHoaDonId == ledger.MinhChungThanhToanHoaDonId);
                if (proof != null)
                {
                    proof.LichSuThanhToan = ledger;
                }
            }
        }

        public Task<HoaDon?> LockHoaDonAsync(int hoaDonId, CancellationToken ct = default)
        {
            LockCalls++;
            if (InTransaction != null && !InTransaction())
            {
                LockedOutsideTransaction = true;
            }

            Invoices.TryGetValue(hoaDonId, out var hoaDon);
            if (hoaDon != null)
            {
                OnLock?.Invoke(hoaDon);
            }

            return Task.FromResult(hoaDon);
        }

        public Task<IReadOnlyList<YeuCauThanhToanHoaDon>> GetExpiredWaitingRequestsAsync(int hoaDonId, DateTime nowUtc, CancellationToken ct = default)
        {
            var predicate = YeuCauThanhToanHoaDon.QuaHanChuaGhiNhan(nowUtc).Compile();
            return Task.FromResult<IReadOnlyList<YeuCauThanhToanHoaDon>>(Requests.Where(r => r.HoaDonId == hoaDonId && predicate(r)).ToList());
        }

        public Task<YeuCauThanhToanHoaDon?> GetActiveRequestAsync(int hoaDonId, DateTime nowUtc, CancellationToken ct = default)
        {
            var predicate = YeuCauThanhToanHoaDon.DangHoatDong(nowUtc).Compile();
            return Task.FromResult(Requests.Where(r => r.HoaDonId == hoaDonId && predicate(r)).OrderByDescending(r => r.NgayTao).FirstOrDefault());
        }

        public Task<YeuCauThanhToanHoaDon?> GetRequestAsync(int yeuCauId, CancellationToken ct = default)
        {
            return Task.FromResult(Requests.FirstOrDefault(r => r.YeuCauThanhToanHoaDonId == yeuCauId && !r.IsDeleted));
        }

        public Task<MinhChungThanhToanHoaDon?> GetProofAsync(int minhChungId, CancellationToken ct = default)
        {
            foreach (var request in Requests.Where(r => !r.IsDeleted))
            {
                var proof = request.MinhChungThanhToanHoaDons.FirstOrDefault(m => m.MinhChungThanhToanHoaDonId == minhChungId && !m.IsDeleted);
                if (proof != null)
                {
                    proof.YeuCauThanhToanHoaDon = request;
                    return Task.FromResult<MinhChungThanhToanHoaDon?>(proof);
                }
            }

            return Task.FromResult<MinhChungThanhToanHoaDon?>(null);
        }

        public Task<LichSuThanhToan?> GetLedgerEntryAsync(int lichSuThanhToanId, CancellationToken ct = default)
        {
            return Task.FromResult(Ledger.FirstOrDefault(l => l.LichSuThanhToanId == lichSuThanhToanId));
        }

        public Task<decimal> GetTongTienDaThanhToanHoaDonAsync(int hoaDonId, CancellationToken ct = default)
        {
            return Task.FromResult(Ledger.Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted).Sum(l => l.SoTienThanhToan));
        }

        public Task<bool> ExistsMaGiaoDichAsync(string maGiaoDich, CancellationToken ct = default)
        {
            return Task.FromResult(Ledger.Any(l => !l.IsDeleted && string.Equals(l.MaGiaoDich, maGiaoDich.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(int hoaDonId, string maGiaoDich, CancellationToken ct = default)
        {
            return Task.FromResult(Ledger.Any(l => l.HoaDonId == hoaDonId && string.Equals(l.MaGiaoDich, maGiaoDich.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> ExistsMaGiaoDichIncludingDeletedAsync(string maGiaoDich, CancellationToken ct = default)
        {
            return Task.FromResult(Ledger.Any(l => string.Equals(l.MaGiaoDich, maGiaoDich.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        public void AddLedgerEntry(LichSuThanhToan lichSu) => Ledger.Add(lichSu);
        public void AddNotification(ThongBao thongBao) => Notifications.Add(thongBao);

        public Task<IReadOnlyList<int>> GetAdminUserIdsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<int>>(AdminUserIds.ToList());
        }

        public Task<IReadOnlyList<int>> GetReviewerUserIdsAsync(int chiNhanhId, CancellationToken ct = default)
        {
            var staff = StaffUserIdsByBranch.TryGetValue(chiNhanhId, out var ids) ? ids : new List<int>();
            return Task.FromResult<IReadOnlyList<int>>(AdminUserIds.Concat(staff).Distinct().ToList());
        }

        public Task<int?> GetTenantUserIdAsync(int hopDongId, CancellationToken ct = default)
        {
            return Task.FromResult(TenantUserIdByHopDong.TryGetValue(hopDongId, out var id) ? (int?)id : null);
        }

        public Task<InvoicePaymentTarget?> GetTargetByHoaDonAsync(int hoaDonId, CancellationToken ct = default)
        {
            return Task.FromResult(BuildTarget(hoaDonId, null));
        }

        public Task<InvoicePaymentTarget?> GetTargetByMinhChungAsync(int minhChungId, CancellationToken ct = default)
        {
            var request = Requests.FirstOrDefault(r => !r.IsDeleted && r.MinhChungThanhToanHoaDons.Any(m => m.MinhChungThanhToanHoaDonId == minhChungId && !m.IsDeleted));
            return Task.FromResult(request == null ? null : BuildTarget(request.HoaDonId, request));
        }

        public Task<InvoicePaymentTarget?> GetTargetByLedgerEntryAsync(int lichSuThanhToanId, CancellationToken ct = default)
        {
            var ledger = Ledger.FirstOrDefault(l => l.LichSuThanhToanId == lichSuThanhToanId);
            return Task.FromResult(ledger == null ? null : BuildTarget(ledger.HoaDonId, null));
        }

        public Task<InvoicePaymentTarget?> GetTargetByMaYeuCauAsync(string maYeuCau, CancellationToken ct = default)
        {
            var normalized = new string(maYeuCau.Where(c => !char.IsWhiteSpace(c)).ToArray());
            var request = Requests.FirstOrDefault(r => !r.IsDeleted && string.Equals(r.MaYeuCau, normalized, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(request == null ? null : BuildTarget(request.HoaDonId, request));
        }

        private InvoicePaymentTarget? BuildTarget(int hoaDonId, YeuCauThanhToanHoaDon? request)
        {
            if (!Invoices.TryGetValue(hoaDonId, out var hd) || !Meta.TryGetValue(hoaDonId, out var meta))
            {
                return null;
            }

            return new InvoicePaymentTarget
            {
                HoaDonId = hd.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                HopDongId = hd.HopDongId,
                NguoiThueId = meta.NguoiThueId,
                ChiNhanhId = meta.ChiNhanhId,
                IsDeleted = hd.IsDeleted,
                TrangThaiPhatHanh = hd.TrangThaiPhatHanh,
                NgayGui = hd.NgayGui,
                YeuCauId = request?.YeuCauThanhToanHoaDonId,
                MaYeuCau = request?.MaYeuCau
            };
        }

        public Task<PaymentInvoiceSnapshot?> GetInvoiceSnapshotAsync(int hoaDonId, CancellationToken ct = default)
        {
            if (!Invoices.TryGetValue(hoaDonId, out var hd) || !Meta.TryGetValue(hoaDonId, out var meta))
            {
                return Task.FromResult<PaymentInvoiceSnapshot?>(null);
            }

            return Task.FromResult<PaymentInvoiceSnapshot?>(new PaymentInvoiceSnapshot
            {
                HoaDonId = hd.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                TenPhong = meta.TenPhong,
                TenChiNhanh = meta.TenChiNhanh,
                TenKhachThue = meta.TenKhachThue,
                TongTien = hd.TongTien,
                DaThu = Ledger.Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted).Sum(l => l.SoTienThanhToan),
                ChoPhepThanhToanMotPhan = hd.ChoPhepThanhToanMotPhan,
                SoTienThanhToanToiThieu = hd.SoTienThanhToanToiThieu,
                TrangThaiHoaDon = hd.TrangThaiHoaDon,
                TrangThaiPhatHanh = hd.TrangThaiPhatHanh,
                IsDeleted = hd.IsDeleted,
                Thang = meta.Thang,
                Nam = meta.Nam,
                HanThanhToan = hd.HanThanhToan
            });
        }

        public Task<IReadOnlyList<PaymentLedgerRowSnapshot>> GetActiveLedgerRowsAsync(int hoaDonId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PaymentLedgerRowSnapshot>>(Ledger
                .Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted)
                .OrderBy(l => l.NgayThanhToan).ThenBy(l => l.LichSuThanhToanId)
                .Select(l => new PaymentLedgerRowSnapshot
                {
                    LichSuThanhToanId = l.LichSuThanhToanId,
                    SoTienThanhToan = l.SoTienThanhToan,
                    NgayThanhToanUtc = l.NgayThanhToan,
                    MaGiaoDich = l.MaGiaoDich,
                    PhuongThucThanhToan = l.PhuongThucThanhToan
                })
                .ToList());
        }

        public Task<IReadOnlyList<PaymentRequestSnapshot>> GetRequestSnapshotsAsync(int hoaDonId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PaymentRequestSnapshot>>(Requests
                .Where(r => r.HoaDonId == hoaDonId && !r.IsDeleted)
                .OrderByDescending(r => r.NgayTao)
                .ThenByDescending(r => r.YeuCauThanhToanHoaDonId)
                .Select(ToSnapshot)
                .ToList());
        }

        public Task<PaymentRequestSnapshot?> GetRequestSnapshotAsync(int yeuCauId, CancellationToken ct = default)
        {
            var request = Requests.FirstOrDefault(r => r.YeuCauThanhToanHoaDonId == yeuCauId && !r.IsDeleted);
            return Task.FromResult(request == null ? null : ToSnapshot(request));
        }

        private PaymentRequestSnapshot ToSnapshot(YeuCauThanhToanHoaDon r) => new()
        {
            YeuCauId = r.YeuCauThanhToanHoaDonId,
            HoaDonId = r.HoaDonId,
            MaYeuCau = r.MaYeuCau,
            SoTien = r.SoTien,
            NoiDungChuyenKhoan = r.NoiDungChuyenKhoan,
            HanThanhToan = r.HanThanhToan,
            NgayTao = r.NgayTao,
            TrangThai = r.TrangThai,
            MinhChungs = r.MinhChungThanhToanHoaDons.Where(m => !m.IsDeleted).Select(m => new PaymentProofSnapshot
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
                GhiNhanBiHuy = Ledger.Any(l => l.MinhChungThanhToanHoaDonId == m.MinhChungThanhToanHoaDonId && l.IsDeleted)
            }).ToList(),
            LichSu = r.LichSuTrangThaiYeuCauThanhToanHoaDons
                .OrderBy(l => l.NgayThucHien).ThenBy(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
                .Select(l => new PaymentHistorySnapshot
                {
                    Id = l.LichSuTrangThaiYeuCauThanhToanHoaDonId,
                    TrangThaiCu = l.TrangThaiCu,
                    TrangThaiMoi = l.TrangThaiMoi,
                    NguoiThucHien = l.NguoiThucHienId.HasValue && UserNames.TryGetValue(l.NguoiThucHienId.Value, out var name) ? name : null,
                    NgayThucHien = l.NgayThucHien,
                    LyDo = l.LyDo
                }).ToList()
        };

        public Task<ReviewQueuePage> GetReviewQueueAsync(ReviewQueueQuery query, CancellationToken ct = default)
        {
            var rows = Requests.Where(r => !r.IsDeleted)
                .SelectMany(r => r.MinhChungThanhToanHoaDons.Where(m => !m.IsDeleted).Select(m => (Request: r, Proof: m)))
                .Where(x => query.DaXuLy
                    ? x.Proof.TrangThaiDoiChieu != TrangThaiMinhChungThanhToan.ChoXacNhan
                    : x.Proof.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.ChoXacNhan)
                .Where(x => query.ChiNhanhId <= 0 || Meta[x.Request.HoaDonId].ChiNhanhId == query.ChiNhanhId)
                .Where(x => query.AllowedBranchIds == null || query.AllowedBranchIds.Contains(Meta[x.Request.HoaDonId].ChiNhanhId))
                .Select(x => new ReviewQueueRowSnapshot
                {
                    MinhChungId = x.Proof.MinhChungThanhToanHoaDonId,
                    YeuCauId = x.Request.YeuCauThanhToanHoaDonId,
                    MaYeuCau = x.Request.MaYeuCau,
                    HoaDonId = x.Request.HoaDonId,
                    MaHoaDon = Invoices[x.Request.HoaDonId].MaHoaDon,
                    SoTienLuot = x.Request.SoTien,
                    NgayNop = x.Proof.NgayTao,
                    TrangThai = x.Proof.TrangThaiDoiChieu,
                    ChoAdmin = x.Proof.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.ChoXacNhan &&
                        x.Request.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu &&
                        PaymentNoteTags.IsWaitingAdmin(x.Request)
                })
                .Where(r => !query.ChiChoAdmin || r.ChoAdmin)
                .ToList();

            return Task.FromResult(new ReviewQueuePage
            {
                Total = rows.Count,
                Rows = rows.Skip(query.Start).Take(query.Length).ToList()
            });
        }

        public Task<string?> FindSystemSePayEntryRecordedAfterAsync(int hoaDonId, DateTime sinceUtc, CancellationToken ct = default)
        {
            return Task.FromResult(Ledger
                .Where(l => l.HoaDonId == hoaDonId && !l.IsDeleted &&
                            (l.MaGiaoDich.StartsWith(InvoicePaymentPolicy.SePayMaGiaoDichPrefix, StringComparison.Ordinal) ||
                             l.MaGiaoDich.StartsWith(InvoicePaymentPolicy.VnPayMaGiaoDichPrefix, StringComparison.Ordinal)) &&
                            l.NguoiXacNhanId == null && l.NgayXacNhan != null && l.NgayXacNhan >= sinceUtc)
                .OrderBy(l => l.NgayXacNhan).ThenBy(l => l.LichSuThanhToanId)
                .Select(l => l.MaGiaoDich)
                .FirstOrDefault());
        }

        public Task<PaymentReceiptSnapshot?> GetReceiptSnapshotAsync(int lichSuThanhToanId, CancellationToken ct = default)
        {
            var row = Ledger.FirstOrDefault(l => l.LichSuThanhToanId == lichSuThanhToanId);
            if (row == null || !Invoices.TryGetValue(row.HoaDonId, out var hd) || !Meta.TryGetValue(row.HoaDonId, out var meta))
            {
                return Task.FromResult<PaymentReceiptSnapshot?>(null);
            }

            var upToThisEntry = Ledger
                .Where(l => l.HoaDonId == row.HoaDonId && !l.IsDeleted &&
                            (l.NgayThanhToan < row.NgayThanhToan ||
                             (l.NgayThanhToan == row.NgayThanhToan && l.LichSuThanhToanId <= row.LichSuThanhToanId)))
                .ToList();

            return Task.FromResult<PaymentReceiptSnapshot?>(new PaymentReceiptSnapshot
            {
                LichSuThanhToanId = row.LichSuThanhToanId,
                HoaDonId = row.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                ChiNhanhId = meta.ChiNhanhId,
                TenChiNhanh = meta.TenChiNhanh,
                DiaChiChiNhanh = meta.DiaChiChiNhanh,
                TenPhong = meta.TenPhong,
                Thang = meta.Thang,
                Nam = meta.Nam,
                TenKhachThue = meta.TenKhachThue,
                EmailKhachThue = meta.EmailKhachThue,
                TongTien = hd.TongTien,
                SoTienThanhToan = row.SoTienThanhToan,
                NgayThanhToanUtc = row.NgayThanhToan,
                MaGiaoDich = row.MaGiaoDich,
                PhuongThucThanhToan = row.PhuongThucThanhToan,
                GhiChu = row.GhiChu,
                IsDeleted = row.IsDeleted,
                DotThu = upToThisEntry.Count,
                LuyKeDaThanhToan = upToThisEntry.Sum(l => l.SoTienThanhToan)
            });
        }

        public Task<TransferAlertContext?> GetTransferAlertContextAsync(int hoaDonId, CancellationToken ct = default)
        {
            if (!Invoices.TryGetValue(hoaDonId, out var hd) || !Meta.TryGetValue(hoaDonId, out var meta))
            {
                return Task.FromResult<TransferAlertContext?>(null);
            }

            HoaDon? replacement = null;
            if (hd.IsDeleted || hd.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                replacement = Invoices.Values
                    .Where(h => h.HopDongId == hd.HopDongId && h.Thang == hd.Thang && h.Nam == hd.Nam && h.HoaDonId != hd.HoaDonId &&
                                !h.IsDeleted && h.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaHuy)
                    .OrderByDescending(h => h.HoaDonId)
                    .FirstOrDefault();
            }

            return Task.FromResult<TransferAlertContext?>(new TransferAlertContext
            {
                HoaDonId = hd.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                ChiNhanhId = meta.ChiNhanhId,
                HoaDonThayTheId = replacement?.HoaDonId,
                MaHoaDonThayThe = replacement?.MaHoaDon
            });
        }
    }
}
