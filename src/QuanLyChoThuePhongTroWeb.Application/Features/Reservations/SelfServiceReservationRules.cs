namespace QuanLyChoThuePhongTroWeb.Application.Features.Reservations;

public static class SelfServiceReservationRules
{
    public const string CreationReason = "Tự tạo thanh toán theo giá cọc công khai";
    public const string DepositPolicy = "Khi đã nhận đủ cọc đúng hạn, nếu bạn không thuê thì khoản cọc được giữ lại, không hoàn và không chuyển sang lần đặt khác. Tiền chuyển dư hoặc đến muộn được xử lý hoàn riêng.";
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ContractWindow = TimeSpan.FromHours(48);
}
