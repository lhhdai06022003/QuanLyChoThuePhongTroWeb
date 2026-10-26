namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications
{
    // DaNhan là tổng tiền giữ chỗ đã ghi sau lần này, ConLai = max(0, số giữ chỗ - DaNhan); TrangThai là tên enum của giữ chỗ, ví dụ "DangGiuCho".
    public sealed record ReservationPaymentUpdatedEvent(int YeuCauGiuChoId, decimal DaNhan, decimal ConLai, string TrangThai);
}
