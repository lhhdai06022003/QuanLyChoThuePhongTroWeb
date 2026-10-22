using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

namespace QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

public sealed record GuestDepositPageModel(ReservationDto Reservation,
    IReadOnlyList<PaymentEvidenceDto> Evidence,
    SettlementSummaryDto? Settlement, GuestBankInfo? Bank);
