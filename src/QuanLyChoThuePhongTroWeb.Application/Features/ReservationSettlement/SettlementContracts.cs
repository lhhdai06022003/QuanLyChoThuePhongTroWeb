namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

public sealed record SettlementSummaryDto(int ReservationId, decimal ConfirmedAmount,
    decimal AppliedToContractDeposit, int? ContractId, int? RefundDecisionId,
    decimal? ApprovedRefundAmount, decimal RefundedAmount,
    decimal MandatoryRefundAmount, string? RefundStatus, decimal RetainedDepositAmount = 0m,
    decimal AvailableRefundAmount = 0m, bool CanCloseWithoutRent = false, string? ClosureReason = null,
    bool UsesPriorRefundForClosure = false);

public sealed record ReservationContractDraft(int ReservationId, string RoomNumber,
    string CustomerName, string Email, string Phone, bool RequiresCccd,
    decimal Rent, decimal PaidDeposit);

public sealed class ReservationContractInput
{
    public string? CCCD { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Rent { get; set; }
}

public enum SettlementError
{
    None,
    NotFound,
    InvalidState,
    ContractMismatch,
    AmountExceedsAvailable,
    BelowMandatoryRefund,
    DuplicateTransaction,
    Conflict,
    ContractConfirmationRequired
}

public static class SettlementMath
{
    public static decimal MandatoryRefund(decimal confirmedAmount,
        decimal approvedHoldAmount, decimal appliedToContractDeposit,
        decimal lateAmount)
    {
        if (appliedToContractDeposit > 0)
            return Math.Max(0, confirmedAmount - appliedToContractDeposit);
        return Math.Max(Math.Max(0, confirmedAmount - approvedHoldAmount), lateAmount);
    }
}
