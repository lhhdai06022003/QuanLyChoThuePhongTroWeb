namespace QuanLyChoThuePhongTroWeb.Application.Features.Reservations;

public static class SelfServiceReservationRules
{
    public const string CreationReason = "Tự tạo thanh toán theo giá cọc công khai";
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ContractWindow = TimeSpan.FromHours(48);
}
