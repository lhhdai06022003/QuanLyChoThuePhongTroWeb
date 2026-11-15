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
        public List<(int ChiNhanhId, string Ten, DateTime ThoiDiemKetThuc)> HopDongQuaHan { get; } = new();
        public List<DemHoaDonRow> HopDongDaTraPhongConNo { get; } = new();
        public List<HoaDonQuaHanRow> BangQuaHan { get; } = new();
        public List<KyDaChotChuaCoHoaDonRow> KyDaChotChuaCoHoaDon { get; } = new();

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

        // HD1-HD3 trong một lần gọi; mục có cờ gom* = false trả danh sách rỗng như store thật.
        public Task<DemHoaDonChuaGuiRows> DemHoaDonChuaGuiAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, bool gomNhap, bool gomChoDuyet, bool gomDaChotChuaGui, CancellationToken ct = default)
        {
            Record(nameof(DemHoaDonChuaGuiAsync), branchId, allowedBranchIds, gomNhap, gomChoDuyet, gomDaChotChuaGui);
            return Task.FromResult(new DemHoaDonChuaGuiRows
            {
                Nhap = gomNhap ? HoaDonNhap.ToList() : new List<DemHoaDonRow>(),
                ChoDuyet = gomChoDuyet ? HoaDonChoDuyet.ToList() : new List<DemHoaDonRow>(),
                DaChotChuaGui = gomDaChotChuaGui ? HoaDonDaChot.ToList() : new List<DemHoaDonRow>()
            });
        }

        // HD5: áp khoảng kỳ và giới hạn như store thật (mới nhất trước); phạm vi chi nhánh do test tự cấu hình qua row.
        public Task<IReadOnlyList<KyDaChotChuaCoHoaDonRow>> GetKyDaChotChuaCoHoaDonAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, int tuThang, int tuNam, int denThang, int denNam, int gioiHan, CancellationToken ct = default)
        {
            Record(nameof(GetKyDaChotChuaCoHoaDonAsync), branchId, allowedBranchIds, tuThang, tuNam, denThang, denNam, gioiHan);
            var rows = KyDaChotChuaCoHoaDon
                .Where(k => (k.Nam > tuNam || (k.Nam == tuNam && k.Thang >= tuThang))
                            && (k.Nam < denNam || (k.Nam == denNam && k.Thang <= denThang)))
                .OrderByDescending(k => k.Nam).ThenByDescending(k => k.Thang).ThenBy(k => k.PhongTroId)
                .Take(gioiHan)
                .ToList();
            return Task.FromResult<IReadOnlyList<KyDaChotChuaCoHoaDonRow>>(rows);
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

        // TT2 và TT3 trong một lần gọi.
        public Task<DemHoaDonDaGuiChuaThuDuRows> DemHoaDonDaGuiChuaThuDuAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default)
        {
            Record(nameof(DemHoaDonDaGuiChuaThuDuAsync), branchId, allowedBranchIds, nowUtc);
            return Task.FromResult(new DemHoaDonDaGuiChuaThuDuRows { QuaHan = HoaDonQuaHan.ToList(), ChuaToiHan = HoaDonChuaToiHan.ToList() });
        }

        // KH1: sự cố ChoTiepNhan, SoLuongGap = số có NgayGui <= mocGapUtc. KH2: row cấu hình sẵn.
        public Task<DemSuCoCanXuLyRows> DemSuCoCanXuLyAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocGapUtc, DateTime mocXuLyQuaLauUtc, CancellationToken ct = default)
        {
            Record(nameof(DemSuCoCanXuLyAsync), branchId, allowedBranchIds, mocGapUtc, mocXuLyQuaLauUtc);
            var choTiepNhan = SuCoChoTiepNhan
                .GroupBy(s => new { s.ChiNhanhId, s.Ten })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.Ten,
                    SoLuong = g.Count(),
                    SoLuongGap = g.Count(x => x.NgayGui <= mocGapUtc)
                }).ToList();
            return Task.FromResult(new DemSuCoCanXuLyRows { ChoTiepNhan = choTiepNhan, XuLyQuaLau = SuCoXuLyQuaLau.ToList() });
        }

        // KH3: homNayVnBatDauUtc <= ThoiDiemKetThuc < denTruocUtc, SoLuongGap = số có ThoiDiemKetThuc < gapTruocUtc.
        // KH4: ThoiDiemKetThuc < homNayVnBatDauUtc.
        public Task<DemHopDongHetHanRows> DemHopDongHetHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime homNayVnBatDauUtc, DateTime denTruocUtc, DateTime gapTruocUtc, CancellationToken ct = default)
        {
            Record(nameof(DemHopDongHetHanAsync), branchId, allowedBranchIds, homNayVnBatDauUtc, denTruocUtc, gapTruocUtc);
            var sapHetHan = HopDongSapHetHan
                .Where(h => h.ThoiDiemKetThuc >= homNayVnBatDauUtc && h.ThoiDiemKetThuc < denTruocUtc)
                .GroupBy(h => new { h.ChiNhanhId, h.Ten })
                .Select(g => new DemChiNhanhRow
                {
                    ChiNhanhId = g.Key.ChiNhanhId,
                    TenChiNhanh = g.Key.Ten,
                    SoLuong = g.Count(),
                    SoLuongGap = g.Count(x => x.ThoiDiemKetThuc < gapTruocUtc)
                }).ToList();
            var quaHan = HopDongQuaHan
                .Where(h => h.ThoiDiemKetThuc < homNayVnBatDauUtc)
                .GroupBy(h => new { h.ChiNhanhId, h.Ten })
                .Select(g => new DemChiNhanhRow { ChiNhanhId = g.Key.ChiNhanhId, TenChiNhanh = g.Key.Ten, SoLuong = g.Count() })
                .ToList();
            return Task.FromResult(new DemHopDongHetHanRows { SapHetHan = sapHetHan, QuaHan = quaHan });
        }

        public Task<IReadOnlyList<DemHoaDonRow>> DemHopDongDaTraPhongConNoAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(DemHopDongDaTraPhongConNoAsync), branchId, allowedBranchIds);
            return Task.FromResult<IReadOnlyList<DemHoaDonRow>>(HopDongDaTraPhongConNo.ToList());
        }

        public Task<IReadOnlyList<HoaDonQuaHanRow>> GetHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, int gioiHan, CancellationToken ct = default)
        {
            Record(nameof(GetHoaDonQuaHanAsync), branchId, allowedBranchIds, nowUtc, gioiHan);
            return Task.FromResult<IReadOnlyList<HoaDonQuaHanRow>>(BangQuaHan.ToList());
        }
    }
}
