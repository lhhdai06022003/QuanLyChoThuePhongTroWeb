using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    public enum KenhThu
    {
        TienMat,
        ChuyenKhoan,
        SePay,
        VnPay,
        CanTruCoc
    }

    // Phân loại kênh thu của một dòng sổ; thứ tự xét giống nhãn phương thức trên biên lai.
    public static class PaymentChannels
    {
        public static KenhThu Of(string? maGiaoDich, PhuongThucThanhToan phuongThuc)
        {
            if (InvoicePaymentPolicy.IsVnPayKey(maGiaoDich))
            {
                return KenhThu.VnPay;
            }

            if (InvoicePaymentPolicy.IsSePayKey(maGiaoDich))
            {
                return KenhThu.SePay;
            }

            if (phuongThuc == PhuongThucThanhToan.CanTruTienCoc)
            {
                return KenhThu.CanTruCoc;
            }

            return phuongThuc == PhuongThucThanhToan.TienMat ? KenhThu.TienMat : KenhThu.ChuyenKhoan;
        }
    }
}
