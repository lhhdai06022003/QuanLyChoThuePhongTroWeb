using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

public sealed record ReservationAdminDetailModel(ReservationDto Reservation,
    IReadOnlyList<PaymentEvidenceDto> Evidence,
    SettlementSummaryDto? Settlement);
