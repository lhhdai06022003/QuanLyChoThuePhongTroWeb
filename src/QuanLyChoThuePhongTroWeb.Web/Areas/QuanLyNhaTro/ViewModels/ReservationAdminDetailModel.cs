using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

public sealed record ReservationAdminDetailModel(
    ReservationDto Reservation,
    IReadOnlyList<PaymentEvidenceDto> Evidence,
    SettlementSummaryDto? Settlement,
    IReadOnlyList<ReservationTransactionAdminDto>? Transactions = null)
{
    public IReadOnlyList<ReservationTransactionAdminDto> Transactions { get; } = Transactions ?? [];

    // Minh chứng chờ đối soát theo thứ tự store trả (mới nhất trước).
    public IReadOnlyList<PaymentEvidenceDto> PendingEvidence { get; } =
        Evidence.Where(e => e.Status == "ChoXacNhan").ToList();

    // Form đối soát chọn sẵn minh chứng chờ mới nhất; không có thì để "Không gắn ảnh".
    public int? DefaultEvidenceId => PendingEvidence.FirstOrDefault()?.Id;
}
