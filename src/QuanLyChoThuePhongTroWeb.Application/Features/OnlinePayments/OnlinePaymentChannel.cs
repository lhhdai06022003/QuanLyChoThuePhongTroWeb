namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Kênh báo tiền vào: SePay (biến động số dư ngân hàng) hoặc VNPay (IPN thanh toán thẻ/ATM).
public enum OnlinePaymentChannel
{
    SePay = 0,
    VnPay = 1
}
