using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    public sealed record AutoInvoiceConsumption(decimal Dien, decimal Nuoc);

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
        // Tối đa soKyTrungBinh kỳ DaDuyet trước kỳ này, mới đến cũ.
        public IReadOnlyList<AutoInvoiceConsumption> TieuThuCacKyTruoc { get; init; } = Array.Empty<AutoInvoiceConsumption>();
        public IReadOnlyList<AutoInvoiceContractRef> HopDongHieuLuc { get; init; } = Array.Empty<AutoInvoiceContractRef>();
        public bool CoHoaDonHoatDong { get; init; }
        public bool KyTruocChuaChot { get; init; }
        // Chỉ điền khi HopDongHieuLuc.Count == 1.
        public int? NguoiDungKhachId { get; init; }
        public string? EmailKhach { get; init; }
    }

    public sealed record AutoInvoiceDueItem(int HoaDonId, string MaHoaDon, int PhongTroId, DateTime HanPhanHoiUtc);

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
    }

    public interface IAutoInvoiceStore
    {
        // AsNoTracking toàn bộ. Phòng không tồn tại hoặc IsDeleted thì trả null.
        Task<AutoInvoiceRoomContext?> GetRoomContextAsync(int phongTroId, int thang, int nam, int soKyTrungBinh, CancellationToken ct = default);

        // Nhân viên đang được phân công (chưa thu hồi) của chi nhánh, phân công sớm nhất trước; null nếu không có.
        Task<int?> FindFallbackActorAsync(int chiNhanhId, CancellationToken ct = default);

        // AsNoTracking. ChoDuyet, !IsDeleted, HanPhanHoi != null && HanPhanHoi <= nowUtc; sắp HanPhanHoi rồi HoaDonId tăng dần.
        Task<IReadOnlyList<AutoInvoiceDueItem>> GetDueProvisionalInvoicesAsync(DateTime nowUtc, CancellationToken ct = default);

        // NguoiThucHienId của dòng lịch sử mới nhất (NgayThucHien rồi LichSuTrangThaiHoaDonId giảm dần) có TrangThaiPhatHanhMoi == ChoDuyet && TrangThaiPhatHanhCu != ChoDuyet; null nếu không có.
        Task<int?> GetLastProvisionalSubmitterIdAsync(int hoaDonId, CancellationToken ct = default);

        // AsNoTracking, hóa đơn !IsDeleted; chỉ số lấy từ kỳ liên kết (0 nếu không có kỳ); null nếu không có hóa đơn.
        Task<AutoInvoiceAdjustmentSnapshot?> GetAdjustmentSnapshotAsync(int hoaDonId, CancellationToken ct = default);

        // Bỏ mọi thay đổi chưa lưu trong DbContext dùng chung; chỉ gọi khi không còn thay đổi cần giữ.
        void DiscardPendingChanges();
    }
}
