using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class SettlementMathTests
{
    [Theory]
    [InlineData(150, 100, 0, 0, 50)]
    [InlineData(150, 100, 0, 100, 100)]
    [InlineData(150, 100, 120, 0, 30)]
    [InlineData(100, 100, 0, 0, 0)]
    public void MandatoryRefund_RespectsExcessLateAndContractDeposit(
        decimal received, decimal approvedHold, decimal applied,
        decimal late, decimal expected)
    {
        Assert.Equal(expected, SettlementMath.MandatoryRefund(
            received, approvedHold, applied, late));
    }
}
