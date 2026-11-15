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

        // CS2: ảnh chỉ số chưa xóa, trạng thái DocDuoc/KhongDocDuoc/Loi, kỳ chỉ số chưa xóa và chưa chốt (khác DaDuyet),
        // kỳ không bị khóa bởi kỳ sau (kỳ sau DaDuyet hoặc đã có hóa đơn phát hành); gom theo kỳ.
        Task<IReadOnlyList<DemTheoKyRow>> DemAnhChiSoChoXacNhanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // HD1-HD3 trong một truy vấn; mục nào có cờ gom* = false thì không đọc và trả danh sách rỗng.
        // HD1 (Nhap): hóa đơn chưa xóa, TrangThaiPhatHanh = Nhap.
        // HD2 (ChoDuyet): hóa đơn chưa xóa, TrangThaiPhatHanh = ChoDuyet (kể cả hóa đơn tạm tính cũ còn HanPhanHoi).
        // HD3 (DaChotChuaGui): hóa đơn chưa xóa, TrangThaiPhatHanh = DaChot, TrangThaiHoaDon khác DaThanhToan.
        Task<DemHoaDonChuaGuiRows> DemHoaDonChuaGuiAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, bool gomNhap, bool gomChoDuyet, bool gomDaChotChuaGui, CancellationToken ct = default);

        // HD5: kỳ chỉ số chưa xóa, DaDuyet, thuộc phòng chưa xóa, từ (tuThang, tuNam) đến (denThang, denNam), mà phòng không có
        // hóa đơn chưa xóa nào của kỳ đó. Kỳ mới nhất trước rồi PhongTroId tăng dần; lấy tối đa gioiHan dòng.
        Task<IReadOnlyList<KyDaChotChuaCoHoaDonRow>> GetKyDaChotChuaCoHoaDonAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, int tuThang, int tuNam, int denThang, int denNam, int gioiHan, CancellationToken ct = default);

        // TT1: minh chứng ChoXacNhan, chưa xóa; yêu cầu thanh toán và hóa đơn của nó chưa xóa.
        Task<IReadOnlyList<DemChiNhanhRow>> DemMinhChungChoDoiChieuAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // TT2: hóa đơn DaGui, chưa xóa, khác DaThanhToan, HanThanhToan != null và < nowUtc. TongConNo = TongTien - ledger chưa xóa.
        // Dùng cho tổng số của bảng quá hạn; Trung tâm việc lấy TT2 từ DemHoaDonDaGuiChuaThuDuAsync.
        Task<IReadOnlyList<DemHoaDonRow>> DemHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default);

        // TT2 và TT3 trong một truy vấn: hóa đơn DaGui, chưa xóa, khác DaThanhToan.
        // TT2 (QuaHan): HanThanhToan != null và < nowUtc. TT3 (ChuaToiHan): HanThanhToan null hoặc >= nowUtc.
        // TongConNo = TongTien - ledger chưa xóa.
        Task<DemHoaDonDaGuiChuaThuDuRows> DemHoaDonDaGuiChuaThuDuAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, CancellationToken ct = default);

        // KH1 và KH2 trong một truy vấn, sự cố chưa xóa.
        // KH1 (ChoTiepNhan): trạng thái ChoTiepNhan; SoLuongGap = số sự cố có NgayGui <= mocGapUtc.
        // KH2 (XuLyQuaLau): trạng thái DangXuLy có NgayGui <= mocXuLyQuaLauUtc.
        Task<DemSuCoCanXuLyRows> DemSuCoCanXuLyAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime mocGapUtc, DateTime mocXuLyQuaLauUtc, CancellationToken ct = default);

        // KH3 và KH4 trong một truy vấn, hợp đồng DangHoatDong chưa xóa có ThoiDiemKetThuc.
        // KH3 (SapHetHan): homNayVnBatDauUtc <= ThoiDiemKetThuc < denTruocUtc; SoLuongGap = số có ThoiDiemKetThuc < gapTruocUtc.
        // KH4 (QuaHan): ThoiDiemKetThuc < homNayVnBatDauUtc (đã quá hạn mà chưa thanh lý hay gia hạn).
        Task<DemHopDongHetHanRows> DemHopDongHetHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime homNayVnBatDauUtc, DateTime denTruocUtc, DateTime gapTruocUtc, CancellationToken ct = default);

        // KH5: hợp đồng DaKetThuc chưa xóa có hóa đơn DaGui chưa xóa còn nợ, trừ hợp đồng mà khách đã gia hạn (cùng phòng và cùng
        // người thuê đang có hợp đồng DangHoatDong). SoLuong = số hợp đồng, TongConNo = tổng còn nợ.
        Task<IReadOnlyList<DemHoaDonRow>> DemHopDongDaTraPhongConNoAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);

        // Bảng quá hạn: điều kiện như TT2, sắp HanThanhToan tăng rồi HoaDonId tăng, lấy gioiHan dòng.
        Task<IReadOnlyList<HoaDonQuaHanRow>> GetHoaDonQuaHanAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds, DateTime nowUtc, int gioiHan, CancellationToken ct = default);
    }
}
