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

        // Quy tắc vào luồng thanh toán: hóa đơn đã gửi hoặc đã hủy sau khi gửi. Cùng quy tắc với IHoaDonStore.CheckHoaDonOwnershipAsync.
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
        public int Thang { get; init; }
        public int Nam { get; init; }
        // UTC.
        public DateTime? HanThanhToan { get; init; }
    }

    // Một dòng sổ chưa xóa mềm của hóa đơn (panel thanh toán khách thuê, spec thanh toán trực tuyến §3.2).
    public sealed class PaymentLedgerRowSnapshot
    {
        public int LichSuThanhToanId { get; init; }
        public decimal SoTienThanhToan { get; init; }
        public DateTime NgayThanhToanUtc { get; init; }
        public string MaGiaoDich { get; init; } = string.Empty;
        public PhuongThucThanhToan PhuongThucThanhToan { get; init; }
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

    // Dữ liệu biên lai của một dòng sổ (spec thanh toán trực tuyến §3.4, §6.3). Đọc không tracking, kể cả dòng đã hủy ghi nhận.
    public sealed class PaymentReceiptSnapshot
    {
        public int LichSuThanhToanId { get; init; }
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public int ChiNhanhId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public string DiaChiChiNhanh { get; init; } = string.Empty;
        // PhongTro.SoPhong.
        public string TenPhong { get; init; } = string.Empty;
        public int Thang { get; init; }
        public int Nam { get; init; }
        // NguoiThue.HoVaTen của hợp đồng.
        public string TenKhachThue { get; init; } = string.Empty;
        // NguoiThue.Email.
        public string? EmailKhachThue { get; init; }
        public decimal TongTien { get; init; }
        public decimal SoTienThanhToan { get; init; }
        public DateTime NgayThanhToanUtc { get; init; }
        public string MaGiaoDich { get; init; } = string.Empty;
        public PhuongThucThanhToan PhuongThucThanhToan { get; init; }
        public string GhiChu { get; init; } = string.Empty;
        public bool IsDeleted { get; init; }
        // Thứ tự lần trả chưa xóa mềm theo (NgayThanhToan, LichSuThanhToanId), tính cả dòng này nếu chưa xóa.
        public int DotThu { get; init; }
        // Tổng SoTienThanhToan các dòng chưa xóa có (NgayThanhToan, Id) không sau dòng này.
        public decimal LuyKeDaThanhToan { get; init; }
    }

    // Ngữ cảnh để soạn cảnh báo Telegram của một hóa đơn (không lọc IsDeleted để tìm được hóa đơn thay thế).
    public sealed class TransferAlertContext
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public int ChiNhanhId { get; init; }
        // Chỉ có khi hóa đơn đã xóa mềm hoặc DaHuy: hóa đơn khác cùng HopDongId/Thang/Nam, chưa xóa, khác DaHuy, Id lớn nhất.
        public int? HoaDonThayTheId { get; init; }
        public string? MaHoaDonThayThe { get; init; }
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
