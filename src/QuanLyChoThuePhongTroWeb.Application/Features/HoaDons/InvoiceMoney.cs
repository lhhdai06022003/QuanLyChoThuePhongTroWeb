using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons
{
    // Tiền hóa đơn tính bằng đồng nguyên (spec thanh toán §9.1, §31.1): VietQR chỉ nhận số nguyên,
    // nên mọi thành tiền làm tròn về 0 chữ số thập phân.
    public static class InvoiceMoney
    {
        public static decimal RoundVnd(decimal amount)
        {
            return decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        }

        public static bool IsWholeVnd(decimal amount)
        {
            return amount == decimal.Truncate(amount);
        }
    }
}
