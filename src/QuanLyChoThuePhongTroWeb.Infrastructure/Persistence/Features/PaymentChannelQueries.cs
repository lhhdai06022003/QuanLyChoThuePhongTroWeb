using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    // Lọc sổ thanh toán theo kênh thu, cùng thứ tự xét với PaymentChannels.Of:
    // VNPay, SePay, cấn trừ cọc, tiền mặt, chuyển khoản. ToUpper vì PaymentChannels so sánh không phân biệt hoa thường
    // (các tiền tố là hằng chữ hoa).
    internal static class PaymentChannelQueries
    {
        public static IQueryable<LichSuThanhToan> TheoKenh(this IQueryable<LichSuThanhToan> q, KenhThu kenh)
        {
            var vnPay = InvoicePaymentPolicy.VnPayMaGiaoDichPrefix;
            var sePay = InvoicePaymentPolicy.SePayMaGiaoDichPrefix;

            return kenh switch
            {
                KenhThu.VnPay => q.Where(l => l.MaGiaoDich.ToUpper().StartsWith(vnPay)),
                KenhThu.SePay => q.Where(l => !l.MaGiaoDich.ToUpper().StartsWith(vnPay) && l.MaGiaoDich.ToUpper().StartsWith(sePay)),
                KenhThu.CanTruCoc => q.Where(l => !l.MaGiaoDich.ToUpper().StartsWith(vnPay) && !l.MaGiaoDich.ToUpper().StartsWith(sePay)
                    && l.PhuongThucThanhToan == PhuongThucThanhToan.CanTruTienCoc),
                KenhThu.TienMat => q.Where(l => !l.MaGiaoDich.ToUpper().StartsWith(vnPay) && !l.MaGiaoDich.ToUpper().StartsWith(sePay)
                    && l.PhuongThucThanhToan == PhuongThucThanhToan.TienMat),
                _ => q.Where(l => !l.MaGiaoDich.ToUpper().StartsWith(vnPay) && !l.MaGiaoDich.ToUpper().StartsWith(sePay)
                    && l.PhuongThucThanhToan != PhuongThucThanhToan.TienMat && l.PhuongThucThanhToan != PhuongThucThanhToan.CanTruTienCoc)
            };
        }
    }
}
