namespace QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs
{
    public sealed record PendingProvisionalEmail(int HoaDonId, string ToEmail, ProvisionalInvoiceEmailData Data);
}
