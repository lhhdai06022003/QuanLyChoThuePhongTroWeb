using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Ghi lại mọi lời gọi. KH1, KH3, CS1 giữ dữ liệu thô và áp đúng vị từ ghi trong comment của IViecCanLamStore
    // với mốc thời gian được truyền vào; các method còn lại trả danh sách row cấu hình sẵn.
    public class FakeViecCanLamStore : IViecCanLamStore
    {
        public sealed record Call(string Method, int? BranchId, IReadOnlyCollection<int>? Allowed, IReadOnlyList<object?> Args);

        public List<Call> Calls { get; } = new();

        // Dữ liệu thô
        public List<(int ChiNhanhId, string Ten, DateTime NgayGui)> SuCoChoTiepNhan { get; } = new();
        public List<(int ChiNhanhId, string Ten, DateTime ThoiDiemKetThuc)> HopDongSapHetHan { get; } = new();
        public List<HopDongChoChotChiSoRow> HopDongDangHoatDong { get; } = new();
        public List<KyChiSoDaChotRow> KyDaGhi { get; } = new();

        // Row cấu hình sẵn
        public List<DemTheoKyRow> AnhChoXacNhan { get; } = new();
        public List<DemHoaDonRow> HoaDonNhap { get; } = new();
        public List<DemHoaDonRow> HoaDonChoDuyet { get; } = new();
        public List<DemHoaDonRow> HoaDonDaChot { get; } = new();
        public List<DemChiNhanhRow> MinhChung { get; } = new();
        public List<DemHoaDonRow> HoaDonQuaHan { get; } = new();
        public List<DemHoaDonRow> HoaDonChuaToiHan { get; } = new();
        public List<DemChiNhanhRow> SuCoXuLyQuaLau { get; } = new();
        public List<HoaDonQuaHanRow> BangQuaHan { get; } = new();

        public bool WasCalled(string method) => Calls.Any(c => c.Method == method);
        public Call Single(string method) => Calls.Single(c => c.Method == method);

        private void Record(string method, int? branchId, IReadOnlyCollection<int>? allowed, params object?[] args)
            => Calls.Add(new Call(method, branchId, allowed, args));

        public Task<IReadOnlyList<HopDongChoChotChiSoRow>> GetHopDongDangHoatDongAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(GetHopDongDangHoatDongAsync), branchId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<HopDongChoChotChiSoRow>>(HopDongDangHoatDong.ToList());
        }

        public Task<IReadOnlyList<KyChiSoDaChotRow>> GetKyChiSoDaChotAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, int tuThang, int tuNam, CancellationToken ct = default)
        {
            Record(nameof(GetKyChiSoDaChotAsync), branchId, allowedBranchIds, tuThang, tuNam);
            var rows = KyDaGhi.Where(k => k.Nam > tuNam || (k.Nam == tuNam && k.Thang >= tuThang)).ToList();
            return Task.FromResult<IReadOnlyList<KyChiSoDaChotRow>>(rows);
        }

        public Task<IReadOnlyList<DemTheoKyRow>> DemAnhChiSoChoXacNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(DemAnhChiSoChoXacNhanAsync), branchId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<DemTheoKyRow>>(AnhChoXacNhan.ToList());
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonNhapAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(DemHoaDonNhapAsync), branchId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<DemHoaDonRow>>(HoaDonNhap.ToList());
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonChoDuyetAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(DemHoaDonChoDuyetAsync), branchId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<DemHoaDonRow>>(HoaDonChoDuyet.ToList());
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaChotChuaGuiAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(DemHoaDonDaChotChuaGuiAsync), branchId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<DemHoaDonRow>>(HoaDonDaChot.ToList());
        }

        public Task<IReadOnlyList<DemChiNhanhRow>> DemMinhChungChoDoiChieuAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(DemMinhChungChoDoiChieuAsync), branchId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<DemChiNhanhRow>>(MinhChung.ToList());
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default)
        {
            Record(nameof(DemHoaDonQuaHanAsync), branchId, allowedBranchIds, nowUtc);
            return Task.FromResult<IReadOnlyList<DemHoaDonRow>>(HoaDonQuaHan.ToList());
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaGuiChuaToiHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default)
        {
            Record(nameof(DemHoaDonDaGuiChuaToiHanAsync), branchId, allowedBranchIds, nowUtc);
            return Task.FromResult<IReadOnlyList<DemHoaDonRow>>(HoaDonChuaToiHan.ToList());
        }

        // KH1: sự cố ChoTiepNhan; SoLuongGap = số có NgayGui <= mocGapUtc.
        public Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoChoTiepNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocGapUtc, CancellationToken ct = default)
        {
            Record(nameof(DemSuCoChoTiepNhanAsync), branchId, allowedBranchIds, mocGapUtc);
            var rows = SuCoChoTiepNhan
                .GroupBy(s => new { s.ChiNhanhId, s.Ten })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.Ten,
                    SoLuong = g.Count(),
                    SoLuongGap = g.Count(x => x.NgayGui <= mocGapUtc)
                }).ToList();
            return Task.FromResult<IReadOnlyList<DemChiNhanhRow>>(rows);
        }

        public Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoXuLyQuaLauAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocUtc, CancellationToken ct = default)
        {
            Record(nameof(DemSuCoXuLyQuaLauAsync), branchId, allowedBranchIds, mocUtc);
            return Task.FromResult<IReadOnlyList<DemChiNhanhRow>>(SuCoXuLyQuaLau.ToList());
        }

        // KH3: tuUtc <= ThoiDiemKetThuc < denTruocUtc; SoLuongGap = số có ThoiDiemKetThuc < gapTruocUtc.
        public Task<IReadOnlyList<DemChiNhanhRow>> DemHopDongSapHetHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime tuUtc, DateTime denTruocUtc, DateTime gapTruocUtc, CancellationToken ct = default)
        {
            Record(nameof(DemHopDongSapHetHanAsync), branchId, allowedBranchIds, tuUtc, denTruocUtc, gapTruocUtc);
            var rows = HopDongSapHetHan
                .Where(h => h.ThoiDiemKetThuc >= tuUtc && h.ThoiDiemKetThuc < denTruocUtc)
                .GroupBy(h => new { h.ChiNhanhId, h.Ten })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.Ten,
                    SoLuong = g.Count(),
                    SoLuongGap = g.Count(x => x.ThoiDiemKetThuc < gapTruocUtc)
                }).ToList();
            return Task.FromResult<IReadOnlyList<DemChiNhanhRow>>(rows);
        }

        public Task<IReadOnlyList<HoaDonQuaHanRow>> GetHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, int gioiHan, CancellationToken ct = default)
        {
            Record(nameof(GetHoaDonQuaHanAsync), branchId, allowedBranchIds, nowUtc, gioiHan);
            return Task.FromResult<IReadOnlyList<HoaDonQuaHanRow>>(BangQuaHan.ToList());
        }
    }
}
