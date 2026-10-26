namespace QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs
{
    // Email báo khách đã nhận tiền giữ chỗ (spec giữ chỗ §8). Văn bản thuần: tầng gửi email không mã hóa HTML.
    public sealed record ReservationPaymentNoticeEmail(string TenNguoiNhan, string TieuDe, string NoiDung);
}
