using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence
{
    // Thông tin hóa đơn đọc trước khi khóa (không tracking) để kiểm quyền khách hoặc chi nhánh.
    public sealed class InvoicePaymentTarget
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public int HopDongId { get; init; }
        public int NguoiThueId { get; init; }
        public int ChiNhanhId { get; init; }
        public bool IsDeleted { get; init; }
        public TrangThaiPhatHanhHoaDon TrangThaiPhatHanh { get; init; }
        public DateTime? NgayGui { get; init; }
        // Có khi target được tìm từ lượt, minh chứng hoặc khoản ghi nhận.
        public int? YeuCauId { get; init; }
        public string? MaYeuCau { get; init; }

        // Cùng quy tắc với IHoaDonStore.CheckHoaDonOwnershipAsync: khách thấy hóa đơn đã gửi hoặc đã hủy sau khi gửi.
        public bool KhachXemDuoc(int nguoiThueId)
        {
            return nguoiThueId > 0 && NguoiThueId == nguoiThueId &&
                ((TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaGui && !IsDeleted) ||
                 (TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy && NgayGui != null));
        }
    }

    public sealed class PaymentInvoiceSnapshot
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public string TenPhong { get; init; } = string.Empty;
        public string TenChiNhanh { get; init; } = string.Empty;
        public string TenKhachThue { get; init; } = string.Empty;
        public decimal TongTien { get; init; }
        public decimal DaThu { get; init; }
        public bool ChoPhepThanhToanMotPhan { get; init; }
        public decimal? SoTienThanhToanToiThieu { get; init; }
        public TrangThaiHoaDon TrangThaiHoaDon { get; init; }
        public TrangThaiPhatHanhHoaDon TrangThaiPhatHanh { get; init; }
        public bool IsDeleted { get; init; }
    }

    public sealed class PaymentRequestSnapshot
    {
        public int YeuCauId { get; init; }
        public int HoaDonId { get; init; }
        public string MaYeuCau { get; init; } = string.Empty;
        public decimal SoTien { get; init; }
        public string NoiDungChuyenKhoan { get; init; } = string.Empty;
        public DateTime? HanThanhToan { get; init; }
        public DateTime NgayTao { get; init; }
        public TrangThaiYeuCauThanhToan TrangThai { get; init; }
        public List<PaymentProofSnapshot> MinhChungs { get; init; } = new();
        // Sắp theo thời gian tăng dần.
        public List<PaymentHistorySnapshot> LichSu { get; init; } = new();
    }

    public sealed class PaymentProofSnapshot
    {
        public int MinhChungId { get; init; }
        public string HinhAnhUrl { get; init; } = string.Empty;
        public string? PublicId { get; init; }
        public DateTime NgayTao { get; init; }
        public DateTime NgayChuyenKhaiBao { get; init; }
        public decimal SoTienKhaiBao { get; init; }
        public string? MaGiaoDichNganHang { get; init; }
        public TrangThaiMinhChungThanhToan TrangThai { get; init; }
        public string? LyDoTuChoi { get; init; }
        public DateTime? NgayDoiChieu { get; init; }
        public bool GhiNhanBiHuy { get; init; }
    }

    public sealed class PaymentHistorySnapshot
    {
        public int Id { get; init; }
        public TrangThaiYeuCauThanhToan? TrangThaiCu { get; init; }
        public TrangThaiYeuCauThanhToan TrangThaiMoi { get; init; }
        public string? NguoiThucHien { get; init; }
        public DateTime NgayThucHien { get; init; }
        public string? LyDo { get; init; }
    }

    public sealed class ReviewQueueQuery
    {
        public int ChiNhanhId { get; init; }
        // null: không giới hạn (Admin).
        public IReadOnlyList<int>? AllowedBranchIds { get; init; }
        public bool DaXuLy { get; init; }
        public bool ChiChoAdmin { get; init; }
        public string? SearchValue { get; init; }
        public int Start { get; init; }
        public int Length { get; init; }
    }

    public sealed class ReviewQueueRowSnapshot
    {
        public int MinhChungId { get; init; }
        public int YeuCauId { get; init; }
        public string MaYeuCau { get; init; } = string.Empty;
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public string TenPhong { get; init; } = string.Empty;
        public string TenChiNhanh { get; init; } = string.Empty;
        public string TenKhachThue { get; init; } = string.Empty;
        public decimal SoTienLuot { get; init; }
        public string? MaGiaoDichKhaiBao { get; init; }
        public DateTime NgayChuyenKhaiBao { get; init; }
        public DateTime NgayNop { get; init; }
        public TrangThaiMinhChungThanhToan TrangThai { get; init; }
        public bool ChoAdmin { get; init; }
    }

    public sealed class ReviewQueuePage
    {
        public int Total { get; init; }
        public IReadOnlyList<ReviewQueueRowSnapshot> Rows { get; init; } = Array.Empty<ReviewQueueRowSnapshot>();
    }
}
