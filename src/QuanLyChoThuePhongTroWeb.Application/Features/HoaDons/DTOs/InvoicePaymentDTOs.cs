using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    // ---------- Khách thuê ----------

    // Khung "Thanh toán" trong modal hóa đơn của khách (spec §27.1).
    public class PaymentPanelDto
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public decimal TongTien { get; set; }
        public decimal DaThu { get; set; }
        public decimal ConLai { get; set; }
        public bool ChoPhepThanhToanMotPhan { get; set; }
        public decimal? SoTienThanhToanToiThieu { get; set; }
        public bool CoTheTaoLuot { get; set; }
        public string? LyDoKhongTheTao { get; set; }
        // Lượt đang hoạt động, hoặc lượt gần nhất (để báo hết hạn/lý do từ chối).
        public PaymentRequestDto? LuotGanNhat { get; set; }
        public DateTime ServerNowUtc { get; set; }
        public string BankId { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
    }

    public class PaymentRequestDto
    {
        public int YeuCauId { get; set; }
        public int HoaDonId { get; set; }
        public string MaYeuCau { get; set; } = string.Empty;
        public decimal SoTien { get; set; }
        public string NoiDungChuyenKhoan { get; set; } = string.Empty;
        public DateTime? HanThanhToanUtc { get; set; }
        public DateTime NgayTaoUtc { get; set; }
        public AppTrangThaiYeuCauThanhToan TrangThai { get; set; }
        // Lượt chờ thanh toán quá hạn được hiển thị là HetHan dù chưa ghi xuống DB (spec §6.3).
        public AppTrangThaiYeuCauThanhToan TrangThaiHieuLuc { get; set; }
        public string TrangThaiText { get; set; } = string.Empty;
        public bool ChoAdmin { get; set; }
        public List<PaymentProofDto> MinhChungs { get; set; } = new();
        public List<PaymentRequestHistoryDto> LichSu { get; set; } = new();
    }

    public class PaymentProofDto
    {
        public int MinhChungId { get; set; }
        public DateTime NgayNopUtc { get; set; }
        public DateTime NgayChuyenKhaiBaoUtc { get; set; }
        public decimal SoTienKhaiBao { get; set; }
        public string? MaGiaoDichNganHang { get; set; }
        public AppTrangThaiMinhChungThanhToan TrangThai { get; set; }
        public string TrangThaiText { get; set; } = string.Empty;
        public string? LyDoTuChoi { get; set; }
        public DateTime? NgayDoiChieuUtc { get; set; }
        // Minh chứng đã được xác nhận nhưng khoản ghi nhận sau đó bị Admin hủy (spec §20).
        public bool GhiNhanBiHuy { get; set; }
    }

    public class PaymentRequestHistoryDto
    {
        public DateTime ThoiGianUtc { get; set; }
        public string TrangThaiCu { get; set; } = string.Empty;
        public string TrangThaiMoi { get; set; } = string.Empty;
        public string NguoiThucHien { get; set; } = string.Empty;
        public string LyDo { get; set; } = string.Empty;
    }

    public class SubmitPaymentProofRequest
    {
        public int YeuCauId { get; set; }
        public UploadFile? File { get; set; }
        public string? MaGiaoDichNganHang { get; set; }
        public DateTime NgayChuyenUtc { get; set; }
    }

    // ---------- Đối chiếu (nhân viên/Admin) ----------

    public enum KetQuaDoiChieu
    {
        DaXacNhan = 0,
        DaGhiNhanSoThucNhan = 1,
        DaChuyenAdmin = 2,
        DaXacNhanCoTienThua = 3
    }

    public class ConfirmPaymentRequest
    {
        public int MinhChungId { get; set; }
        public decimal SoTienThucNhan { get; set; }
        public string? MaGiaoDich { get; set; }
        public DateTime NgayGiaoDichUtc { get; set; }
        public string? GhiChu { get; set; }
    }

    public class ConfirmPaymentResult
    {
        public KetQuaDoiChieu KetQua { get; set; }
        public decimal SoTienGhiNhan { get; set; }
        // Thực nhận − số lượt (chênh lệch) hoặc thực nhận − số còn nợ (tiền thừa, chờ Admin).
        public decimal ChenhLech { get; set; }
        public decimal ConLai { get; set; }
        public string TrangThaiHoaDon { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ReviewQueueFilter
    {
        public int ChiNhanhId { get; set; }
        // false: tab "Chờ đối chiếu" (minh chứng ChoXacNhan); true: tab "Đã xử lý".
        public bool DaXuLy { get; set; }
        public bool ChiChoAdmin { get; set; }
    }

    public class ReviewQueueRowDto
    {
        public int MinhChungId { get; set; }
        public int YeuCauId { get; set; }
        public string MaYeuCau { get; set; } = string.Empty;
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string TenKhachThue { get; set; } = string.Empty;
        public decimal SoTienLuot { get; set; }
        public string? MaGiaoDichKhaiBao { get; set; }
        public DateTime NgayChuyenKhaiBaoUtc { get; set; }
        public DateTime NgayNopUtc { get; set; }
        public AppTrangThaiMinhChungThanhToan TrangThai { get; set; }
        public string TrangThaiText { get; set; } = string.Empty;
        public bool ChoAdmin { get; set; }
    }

    // Chi tiết một lượt kèm hóa đơn, dùng cho modal đối chiếu và tra cứu theo mã (spec §21).
    public class PaymentRequestDetailDto
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string TenKhachThue { get; set; } = string.Empty;
        public decimal TongTien { get; set; }
        public decimal DaThu { get; set; }
        public decimal ConLai { get; set; }
        public bool ChoPhepThanhToanMotPhan { get; set; }
        public string TrangThaiHoaDon { get; set; } = string.Empty;
        public bool HoaDonDaHuy { get; set; }
        public PaymentRequestDto LuotThanhToan { get; set; } = new();
        public int? MinhChungDangXemId { get; set; }
        public bool LaAdmin { get; set; }
        // Actor được xác nhận/từ chối minh chứng đang xem (minh chứng chờ và không bị khóa chờ Admin).
        public bool CoTheDoiChieu { get; set; }
    }

    public class ConfigurePartialPaymentRequest
    {
        public int HoaDonId { get; set; }
        public bool ChoPhep { get; set; }
        public decimal? SoTienToiThieu { get; set; }
    }

    // Tóm tắt thanh toán cho modal hóa đơn phía quản lý: nút "Thu tiền" và công tắc trả một phần.
    public class InvoicePaymentSummaryDto
    {
        public int HoaDonId { get; set; }
        public decimal TongTien { get; set; }
        public decimal DaThu { get; set; }
        public decimal ConLai { get; set; }
        public bool ChoPhepThanhToanMotPhan { get; set; }
        public decimal? SoTienThanhToanToiThieu { get; set; }
        public string? MaLuotHoatDong { get; set; }
        public AppTrangThaiYeuCauThanhToan? TrangThaiLuotHoatDong { get; set; }
        public bool LaAdmin { get; set; }
    }

    // ---------- Ledger (thu thủ công, hủy ghi nhận) ----------

    public class ManualCollectionRequest
    {
        public int HoaDonId { get; set; }
        public decimal SoTienThucNhan { get; set; }
        public AppPhuongThucThanhToan PhuongThuc { get; set; }
        public DateTime NgayThanhToanUtc { get; set; }
        public string? MaGiaoDich { get; set; }
        public string? GhiChu { get; set; }
    }

    public class ManualCollectionResult
    {
        public int LichSuThanhToanId { get; set; }
        public string MaGiaoDich { get; set; } = string.Empty;
        public decimal SoTienGhiNhan { get; set; }
        public decimal TienThua { get; set; }
        public decimal ConLai { get; set; }
        public string TrangThaiHoaDon { get; set; } = string.Empty;
        // Mã lượt chờ thanh toán bị hủy vì đã thu bằng phương thức khác (spec §18 mục 3).
        public string? MaLuotDaHuy { get; set; }
    }

    public class VoidPaymentResult
    {
        public int HoaDonId { get; set; }
        public decimal ConLai { get; set; }
        public string TrangThaiHoaDon { get; set; } = string.Empty;
    }
}
