namespace QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs
{
    // Dữ liệu email biên lai thanh toán (spec thanh toán trực tuyến §3.4, §6.3). Chuỗi chưa mã hóa HTML:
    // tầng gửi email tự mã hóa khi dựng nội dung.
    public sealed record PaymentReceiptEmail
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public int LichSuThanhToanId { get; init; }
        public string TenChiNhanh { get; init; } = string.Empty;
        public string DiaChiChiNhanh { get; init; } = string.Empty;
        public string TenPhong { get; init; } = string.Empty;
        public string TenNguoiThanhToan { get; init; } = string.Empty;
        public int Thang { get; init; }
        public int Nam { get; init; }
        // "dd/MM/yyyy HH:mm:ss" theo giờ Việt Nam.
        public string NgayThanhToanVn { get; init; } = string.Empty;
        public string PhuongThuc { get; init; } = string.Empty;
        public string? MaThamChieu { get; init; }
        public decimal TongTien { get; init; }
        public decimal SoTienLanNay { get; init; }
        public decimal LuyKeDaThanhToan { get; init; }
        public decimal ConNo { get; init; }
        public decimal TienThua { get; init; }
        public bool DaThuDu { get; init; }
        public int DotThu { get; init; }
        public string? LinkXemBienLai { get; init; }
    }
}
