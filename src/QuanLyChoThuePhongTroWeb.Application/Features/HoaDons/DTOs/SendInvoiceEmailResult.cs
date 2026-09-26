namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public class SendInvoiceEmailResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsResend { get; set; }
        public string? InvoiceCode { get; set; }
        public string? RecipientEmail { get; set; }
    }
}
