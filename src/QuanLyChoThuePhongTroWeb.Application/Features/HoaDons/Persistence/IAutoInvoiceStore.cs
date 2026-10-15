using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence
{
    public sealed record AutoInvoiceMeterImage(
        int AnhChiSoDongHoId,
        LoaiDongHo LoaiDongHo,
        TrangThaiXuLyAnhChiSo TrangThaiXuLy,
        decimal? GiaTriAIGoiY,
        double? DoTinCay,
        decimal? GiaTriXacNhan,
        bool DuocChonLamChiSoChinhThuc,
        DateTime NgayGui);

    public sealed record AutoInvoiceContractRef(int HopDongId, int NguoiThueId);

    public sealed class AutoInvoiceRoomContext
    {
        public int PhongTroId { get; init; }
        public string SoPhong { get; init; } = string.Empty;
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public string SoDienThoaiChiNhanh { get; init; } = string.Empty;
        public int Thang { get; init; }
        public int Nam { get; init; }
        // null = chưa có kỳ điện nước của phòng.
        public int? DichVuDienNuocCuaPhongId { get; init; }
        public TrangThaiGhiNhan? TrangThaiGhiNhan { get; init; }
        public decimal ChiSoDienCu { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocCu { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
        // Đơn giá của kỳ (chụp lúc tạo kỳ); 0 khi chưa có kỳ.
        public decimal DonGiaDien { get; init; }
        public decimal DonGiaNuoc { get; init; }
        // Ảnh chưa xóa của kỳ, NgayGui giảm dần rồi Id giảm dần.
        public IReadOnlyList<AutoInvoiceMeterImage> Images { get; init; } = Array.Empty<AutoInvoiceMeterImage>();
        // Lịch sử tiêu thụ theo hợp đồng hiệu lực (tối đa soKyLichSuToiDa kỳ trọn tháng); không null.
        public MeterConsumptionHistory LichSu { get; init; } = new(Array.Empty<MeterConsumption>(), 30, 30, null);
        public IReadOnlyList<AutoInvoiceContractRef> HopDongHieuLuc { get; init; } = Array.Empty<AutoInvoiceContractRef>();
        public bool CoHoaDonHoatDong { get; init; }
        public bool KyTruocChuaChot { get; init; }
    }

    // Dòng chi tiết chưa xóa mềm của hóa đơn; LoaiDichVu và DonVi chỉ có khi dòng gắn dịch vụ.
    public sealed record AutoInvoiceInvoiceLine(
        int ChiTietHoaDonId,
        string TenDichVu,
        decimal DonGia,
        decimal SoLuong,
        decimal TongTien,
        int? DichVuId,
        LoaiDichVu? LoaiDichVu,
        string? DonVi);

    public sealed class AutoInvoiceAdjustmentSnapshot
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public int HopDongId { get; init; }
        public int ChiNhanhId { get; init; }
        public int PhongTroId { get; init; }
        public int Thang { get; init; }
        public int Nam { get; init; }
        public TrangThaiPhatHanhHoaDon TrangThaiPhatHanh { get; init; }
        public TrangThaiHoaDon TrangThaiHoaDon { get; init; }
        public DateTime? HanPhanHoi { get; init; }
        public int? DichVuDienNuocCuaPhongId { get; init; }
        public decimal ChiSoDienCu { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocCu { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
        public decimal TongTien { get; init; }
        // Đơn giá của kỳ (chụp lúc tạo kỳ) và dữ liệu hợp đồng để tính lại tiền điện nước theo số ngày ở.
        public decimal DonGiaDien { get; init; }
        public decimal DonGiaNuoc { get; init; }
        public decimal TienThuePhong { get; init; }
        public DateTime HopDongBatDau { get; init; }
        public DateTime? HopDongKetThuc { get; init; }
        // Dòng chưa xóa mềm, theo ChiTietHoaDonId tăng dần.
        public IReadOnlyList<AutoInvoiceInvoiceLine> ChiTiets { get; init; } = Array.Empty<AutoInvoiceInvoiceLine>();
    }

    public interface IAutoInvoiceStore
    {
        // AsNoTracking toàn bộ. Phòng không tồn tại hoặc IsDeleted thì trả null.
        Task<AutoInvoiceRoomContext?> GetRoomContextAsync(int phongTroId, int thang, int nam, int soKyLichSuToiDa, CancellationToken ct = default);

        // Nhân viên đang được phân công (chưa thu hồi) của chi nhánh, phân công sớm nhất trước; null nếu không có.
        Task<int?> FindFallbackActorAsync(int chiNhanhId, CancellationToken ct = default);

        // AsNoTracking, hóa đơn !IsDeleted; chỉ số và đơn giá lấy từ kỳ liên kết (0 nếu không có kỳ); kèm các dòng chi tiết chưa xóa
        // và dữ liệu hợp đồng (tiền thuê, ngày bắt đầu/kết thúc); null nếu không có hóa đơn.
        Task<AutoInvoiceAdjustmentSnapshot?> GetAdjustmentSnapshotAsync(int hoaDonId, CancellationToken ct = default);

        // Bỏ mọi thay đổi chưa lưu trong DbContext dùng chung; chỉ gọi khi không còn thay đổi cần giữ.
        void DiscardPendingChanges();
    }
}
