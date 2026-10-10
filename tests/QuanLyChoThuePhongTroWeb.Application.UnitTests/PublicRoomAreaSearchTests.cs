using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class PublicRoomAreaSearchTests
{
    private static readonly PublicRoomDto Room = new(
        32, "107", "Gò Vấp", "Đường Quang Trung, TP. Hồ Chí Minh",
        500000, 25, 2, 1, "Phòng sáng");

    [Theory]
    [InlineData("Go Vap")]
    [InlineData("duong quang trung")]
    [InlineData("quang trung")]
    [InlineData("ho chi minh")]
    [InlineData("  GÒ VẤP  ")]
    public void Matches_FindsBranchOrAddressWithoutAccents(string query)
    {
        Assert.True(PublicRoomAreaSearch.Matches(Room, query));
    }

    [Fact]
    public void Matches_RejectsDifferentArea()
    {
        Assert.False(PublicRoomAreaSearch.Matches(Room, "Thủ Đức"));
    }

    [Fact]
    public void Matches_BlankQueryDoesNotFilterRooms()
    {
        Assert.True(PublicRoomAreaSearch.Matches(Room, "   "));
    }
}
