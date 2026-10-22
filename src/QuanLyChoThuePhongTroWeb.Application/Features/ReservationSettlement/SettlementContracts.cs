namespace QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

public sealed record SettlementSummaryDto(int ReservationId, decimal ConfirmedAmount,
    decimal AppliedToContractDeposit, int? ContractId, int? RefundDecisionId,
    decimal? ApprovedRefundAmount, decimal RefundedAmount,
    decimal MandatoryRefundAmount, string? RefundStatus);

public enum SettlementError
{
    None,
    NotFound,
    InvalidState,
    ContractMismatch,
    AmountExceedsAvailable,
    BelowMandatoryRefund,
    DuplicateTransaction,
    Conflict
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
