using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.Persistence
{
    // branchId: chi nhánh đang lọc (null = mọi chi nhánh).
    // allowedBranchIds: null = không giới hạn (Admin); danh sách = chỉ các chi nhánh đó (rỗng thì không có dữ liệu).
    // Mọi bản ghi xóa mềm đều bị loại.
    public interface IViecCanLamStore
    {
        // CS1: hợp đồng chưa xóa, đang hoạt động.
        Task<IReadOnlyList<HopDongChoChotChiSoRow>> GetHopDongDangHoatDongAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // CS1: kỳ chỉ số đã chốt (DaDuyet, khớp IsDaChot của màn Chốt điện nước), chưa xóa, từ (tuThang, tuNam) trở đi.
        Task<IReadOnlyList<KyChiSoDaChotRow>> GetKyChiSoDaChotAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, int tuThang, int tuNam, CancellationToken ct = default);

        // CS2: ảnh chỉ số chưa xóa, trạng thái DocDuoc/KhongDocDuoc/Loi, kỳ chỉ số chưa xóa; gom theo kỳ.
        Task<IReadOnlyList<DemTheoKyRow>> DemAnhChiSoChoXacNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // HD1: hóa đơn chưa xóa, TrangThaiPhatHanh = Nhap.
        Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonNhapAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // HD2: hóa đơn chưa xóa, TrangThaiPhatHanh = ChoDuyet.
        Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonChoDuyetAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // HD3: hóa đơn chưa xóa, TrangThaiPhatHanh = DaChot, TrangThaiHoaDon khác DaThanhToan.
        Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaChotChuaGuiAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // TT1: minh chứng ChoXacNhan, chưa xóa; yêu cầu thanh toán và hóa đơn của nó chưa xóa.
        Task<IReadOnlyList<DemChiNhanhRow>> DemMinhChungChoDoiChieuAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // TT2: hóa đơn DaGui, chưa xóa, khác DaThanhToan, HanThanhToan != null và < nowUtc. TongConNo = TongTien - ledger chưa xóa.
        Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default);

        // TT3: hóa đơn DaGui, chưa xóa, khác DaThanhToan, HanThanhToan null hoặc >= nowUtc.
        Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonDaGuiChuaToiHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default);

        // KH1: sự cố ChoTiepNhan chưa xóa; SoLuongGap = số sự cố có NgayGui <= mocGapUtc.
        Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoChoTiepNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocGapUtc, CancellationToken ct = default);

        // KH2: sự cố DangXuLy chưa xóa có NgayGui <= mocUtc.
        Task<IReadOnlyList<DemChiNhanhRow>> DemSuCoXuLyQuaLauAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocUtc, CancellationToken ct = default);

        // KH3: hợp đồng DangHoatDong chưa xóa có tuUtc <= ThoiDiemKetThuc < denTruocUtc; SoLuongGap = số có ThoiDiemKetThuc < gapTruocUtc.
        Task<IReadOnlyList<DemChiNhanhRow>> DemHopDongSapHetHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime tuUtc, DateTime denTruocUtc, DateTime gapTruocUtc, CancellationToken ct = default);

        // Bảng quá hạn: điều kiện như TT2, sắp HanThanhToan tăng rồi HoaDonId tăng, lấy gioiHan dòng.
        Task<IReadOnlyList<HoaDonQuaHanRow>> GetHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, int gioiHan, CancellationToken ct = default);
    }
}
