namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public sealed record InvoiceCancellationBlockers(
        bool HasPayment,
        bool HasPendingRequest,
        bool HasPendingProof);
}
