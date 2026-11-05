using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Bộ nhớ thay cho IHopDongLifecycleStore. Entity là cùng một đối tượng nên thay đổi service ghi vào đều quan sát được.
    public class FakeHopDongLifecycleStore : IHopDongLifecycleStore
    {
        private int _nextHopDongId = 1000;

        public List<HopDong> HopDongs { get; } = new();
        public List<HoaDon> HoaDons { get; } = new();
        public List<LichSuThanhToan> Ledgers { get; } = new();
        public List<YeuCauThanhToanHoaDon> Requests { get; } = new();
        public List<ChiTietThanhVienHopDong> Members { get; } = new();
        public List<DangKyDichVu> Services { get; } = new();
        public List<HopDongDieuKhoan> Terms { get; } = new();
        public List<DieuKhoanMau> TermTemplates { get; } = new();
        public List<PhongTro> PhongTros { get; } = new();
        public List<NguoiThue> NguoiThues { get; } = new();
        public List<(int ChiNhanhId, LichSuThueRow Row)> RentalRows { get; } = new();
        public List<(int ChiNhanhId, NoCuRow Row)> OldDebtRows { get; } = new();
        public DateTime NowUtc { get; set; } = new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);

        public List<int> LockHopDongCalls { get; } = new();
        public List<string> Order { get; } = new();

        public Task<HopDong?> GetHopDongAsync(int hopDongId, CancellationToken ct = default)
            => Task.FromResult(HopDongs.FirstOrDefault(h => h.HopDongId == hopDongId && !h.IsDeleted));

        public Task<HopDong?> LockHopDongAsync(int hopDongId, CancellationToken ct = default)
        {
            LockHopDongCalls.Add(hopDongId);
            Order.Add("lock-hopdong");
            return Task.FromResult(HopDongs.FirstOrDefault(h => h.HopDongId == hopDongId && !h.IsDeleted));
        }

        public Task<IReadOnlyList<HoaDon>> LockInvoicesOfContractsAsync(IReadOnlyCollection<int> hopDongIds, CancellationToken ct = default)
        {
            Order.Add("lock-hoadon");
            return GetInvoicesOfContractsAsync(hopDongIds, ct);
        }

        public Task<IReadOnlyList<HoaDon>> GetInvoicesOfContractsAsync(IReadOnlyCollection<int> hopDongIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<HoaDon>>(HoaDons.Where(h => hopDongIds.Contains(h.HopDongId) && !h.IsDeleted).OrderBy(h => h.HoaDonId).ToList());

        public Task<IReadOnlyDictionary<int, decimal>> GetPaidTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, decimal>>(Ledgers
                .Where(l => hoaDonIds.Contains(l.HoaDonId) && !l.IsDeleted)
                .GroupBy(l => l.HoaDonId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SoTienThanhToan)));

        public Task<IReadOnlyDictionary<int, decimal>> GetOffsetTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<int, decimal>>(Ledgers
                .Where(l => hoaDonIds.Contains(l.HoaDonId) && !l.IsDeleted && l.PhuongThucThanhToan == PhuongThucThanhToan.CanTruTienCoc)
                .GroupBy(l => l.HoaDonId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SoTienThanhToan)));

        public Task<IReadOnlyList<YeuCauThanhToanHoaDon>> GetActivePaymentRequestsAsync(IReadOnlyCollection<int> hoaDonIds, DateTime nowUtc, CancellationToken ct = default)
        {
            var dangHoatDong = YeuCauThanhToanHoaDon.DangHoatDong(nowUtc).Compile();
            return Task.FromResult<IReadOnlyList<YeuCauThanhToanHoaDon>>(Requests.Where(y => hoaDonIds.Contains(y.HoaDonId) && dangHoatDong(y)).ToList());
        }

        public void AddLedgerEntry(LichSuThanhToan entry) => Ledgers.Add(entry);

        public Task<bool> HasInvoiceFromPeriodAsync(int hopDongId, int thang, int nam, CancellationToken ct = default)
            => Task.FromResult(HoaDons.Any(h => h.HopDongId == hopDongId && !h.IsDeleted && (h.Nam > nam || (h.Nam == nam && h.Thang >= thang))));

        public Task<PhongTro?> GetPhongTroForUpdateAsync(int phongTroId, CancellationToken ct = default)
            => Task.FromResult(PhongTros.FirstOrDefault(p => p.PhongTroId == phongTroId));

        public Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken ct = default)
            => Task.FromResult(HopDongs.Any(h => h.PhongTroId == phongTroId && h.HopDongId != excludeHopDongId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted));

        public Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int excludeHopDongId, CancellationToken ct = default)
            => Task.FromResult(HopDongs.Any(h => h.NguoiThueId == nguoiThueId && h.HopDongId != excludeHopDongId && h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong && !h.IsDeleted));

        public Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersAsync(int hopDongId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ChiTietThanhVienHopDong>>(Members.Where(m => m.HopDongId == hopDongId && m.NgayChuyenDi == null && !m.IsDeleted).ToList());

        public Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetMembersAsync(int hopDongId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ChiTietThanhVienHopDong>>(Members.Where(m => m.HopDongId == hopDongId && !m.IsDeleted).ToList());

        public Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesOfRoomAsync(int phongTroId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DangKyDichVu>>(Services.Where(s => s.PhongTroId == phongTroId && s.NgayKetThuc == null).ToList());

        public Task<IReadOnlyList<HopDongDieuKhoan>> GetTermsAsync(int hopDongId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<HopDongDieuKhoan>>(Terms.Where(t => t.HopDongId == hopDongId).OrderBy(t => t.ThuTu).ToList());

        public Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermTemplatesByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DieuKhoanMau>>(TermTemplates.Where(t => ids.Contains(t.DieuKhoanMauId) && !t.IsDeleted).ToList());

        public Task<int> CountActiveRenewalsAsync(string maGoc, CancellationToken ct = default)
            => Task.FromResult(HopDongs.Count(h => !h.IsDeleted && HopDongCodeChain.IsRenewal(h.MaHopDong) && HopDongCodeChain.BelongsTo(h.MaHopDong, maGoc)));

        public Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(HopDongs.Any(h => h.MaHopDong == code));

        public Task<IReadOnlyList<HopDong>> GetChainAsync(string maGoc, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<HopDong>>(HopDongs.Where(h => !h.IsDeleted && (h.MaHopDong == maGoc || h.MaHopDong.StartsWith(maGoc + "-GH"))).ToList());

        public Task AddHopDongAsync(HopDong hopDong, CancellationToken ct = default)
        {
            if (hopDong.HopDongId == 0)
            {
                hopDong.HopDongId = ++_nextHopDongId;
            }

            HopDongs.Add(hopDong);
            return Task.CompletedTask;
        }

        public Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken ct = default)
        {
            Members.AddRange(members);
            return Task.CompletedTask;
        }

        public Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken ct = default)
        {
            Terms.AddRange(terms);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LichSuThueRow>> GetRentalHistoryAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LichSuThueRow>>(RentalRows
                .Where(r => allowedBranchIds == null || allowedBranchIds.Contains(r.ChiNhanhId))
                .Select(r => r.Row).ToList());

        public Task<IReadOnlyList<NoCuRow>> GetOldDebtsAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NoCuRow>>(OldDebtRows
                .Where(r => allowedBranchIds == null || allowedBranchIds.Contains(r.ChiNhanhId))
                .Select(r => r.Row).ToList());

        public Task<NguoiThue?> GetNguoiThueAsync(int nguoiThueId, CancellationToken ct = default)
            => Task.FromResult(NguoiThues.FirstOrDefault(n => n.NguoiThueId == nguoiThueId && !n.IsDeleted));
    }
}
