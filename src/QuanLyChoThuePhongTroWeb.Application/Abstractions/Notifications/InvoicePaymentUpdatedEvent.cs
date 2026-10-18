namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications
{
    // TrangThai là nhãn InvoiceStatusLabels.ThanhToan của hóa đơn sau lần ghi sổ, ví dụ "Đã thanh toán".
    public sealed record InvoicePaymentUpdatedEvent(int HoaDonId, decimal ConLai, string TrangThai);
}
