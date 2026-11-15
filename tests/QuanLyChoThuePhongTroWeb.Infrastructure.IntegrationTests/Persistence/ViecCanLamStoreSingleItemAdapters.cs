using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Từ bước 16 (docs/features/toi-uu-hieu-nang) store đếm nhiều mục cùng bảng trong một truy vấn.
    // Các test sẵn có kiểm điều kiện của từng mục (HD1, HD2, HD3, TT3, KH1-KH4): lấy đúng phần của mục đó từ truy vấn gộp,
    // nên chúng vẫn chốt điều kiện lọc, xóa mềm, biên thời gian và phạm vi chi nhánh của truy vấn mới.
    internal static class ViecCanLamStoreSingleItemAdapters
    {
        public static async Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonNhapAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed)
            => (await s.DemHoaDonChuaGuiAsync(branchId, allowed, true, true, true)).Nhap;

        public static async Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonChoDuyetAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed)
            => (await s.DemHoaDonChuaGuiAsync(branchId, allowed, true, true, true)).ChoDuyet;

        public static async Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaChotChuaGuiAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed)
            => (await s.DemHoaDonChuaGuiAsync(branchId, allowed, true, true, true)).DaChotChuaGui;

        public static async Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaGuiChuaToiHanAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed, DateTime nowUtc)
            => (await s.DemHoaDonDaGuiChuaThuDuAsync(branchId, allowed, nowUtc)).ChuaToiHan;

        public static async Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoChoTiepNhanAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed, DateTime mocGapUtc)
            => (await s.DemSuCoCanXuLyAsync(branchId, allowed, mocGapUtc, mocGapUtc)).ChoTiepNhan;

        public static async Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoXuLyQuaLauAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed, DateTime mocUtc)
            => (await s.DemSuCoCanXuLyAsync(branchId, allowed, mocUtc, mocUtc)).XuLyQuaLau;

        public static async Task<IReadOnlyList<DemChiNhanhRow>> DemHopDongSapHetHanAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed, DateTime tuUtc, DateTime denTruocUtc, DateTime gapTruocUtc)
            => (await s.DemHopDongHetHanAsync(branchId, allowed, tuUtc, denTruocUtc, gapTruocUtc)).SapHetHan;

        public static async Task<IReadOnlyList<DemChiNhanhRow>> DemHopDongQuaHanAsync(this ViecCanLamStore s, int? branchId, IReadOnlyCollection<int>? allowed, DateTime homNayVnBatDauUtc)
            => (await s.DemHopDongHetHanAsync(branchId, allowed, homNayVnBatDauUtc, homNayVnBatDauUtc, homNayVnBatDauUtc)).QuaHan;
    }
}
