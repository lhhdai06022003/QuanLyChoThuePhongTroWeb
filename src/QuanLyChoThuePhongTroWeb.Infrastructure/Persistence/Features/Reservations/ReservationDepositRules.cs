using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Reservations;

internal static class ReservationDepositRules
{
    public static bool IsSelfService(YeuCauGiuCho hold) =>
        hold.LichSuTrangThais.Any(h => h.LyDo == SelfServiceReservationRules.CreationReason);

    public static decimal PaidDeposit(YeuCauGiuCho hold)
    {
        var agreedDeposit = hold.SoTienGiuCho.GetValueOrDefault();
        var onTimeAmount = hold.YeuCauThanhToanGiuChos.Sum(p => p.GiaoDichGiuChos
            .Where(g => g.NgayThucNhan <= p.HanThanhToan &&
                g.GhiChu != ReservationPaymentMarkers.LateRefundRequired)
            .Sum(g => g.SoTienThucNhan));
        return agreedDeposit > 0m && onTimeAmount >= agreedDeposit ? agreedDeposit : 0m;
    }
}
