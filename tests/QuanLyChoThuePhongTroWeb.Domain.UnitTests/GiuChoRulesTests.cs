using QuanLyChoThuePhongTroWeb.Domain.Rules;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests;

public class GiuChoRulesTests
{
    [Theory]
    [InlineData(2000000, 1000000)]
    [InlineData(2000001, 1000001)]
    public void TinhTienGiuCho_UsesHalfMonthlyRentRoundedToVnd(decimal rent, decimal expected)
    {
        Assert.Equal(expected, GiuChoRules.TinhTienGiuCho(rent));
    }
}
