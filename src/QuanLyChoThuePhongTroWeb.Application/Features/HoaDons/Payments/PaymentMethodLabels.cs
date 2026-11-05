using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    public static class PaymentMethodLabels
    {
        // Giá trị lạ giữ hành vi cũ: coi là chuyển khoản.
        public static string Label(PhuongThucThanhToan phuongThuc) => phuongThuc switch
        {
            PhuongThucThanhToan.TienMat => "Tiền mặt",
            PhuongThucThanhToan.CanTruTienCoc => "Cấn trừ tiền cọc",
            _ => "Chuyển khoản"
        };
    }
}
