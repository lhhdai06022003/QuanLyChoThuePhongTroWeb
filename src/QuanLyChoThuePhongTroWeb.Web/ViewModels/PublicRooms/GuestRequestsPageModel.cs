using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

namespace QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

public sealed record GuestRequestsPageModel(IReadOnlyList<ViewingRequestDto> Viewings,
    IReadOnlyList<ReservationDto> Reservations,
    IReadOnlyList<PaymentEvidenceDto> Evidence,
    IReadOnlyList<SettlementSummaryDto> Settlements,
    GuestBankInfo? Bank);

public sealed record GuestBankInfo(string BankId, string AccountNumber,
    string AccountName);
