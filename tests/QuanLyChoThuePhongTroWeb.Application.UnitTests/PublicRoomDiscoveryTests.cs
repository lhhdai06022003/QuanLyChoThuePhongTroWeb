using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class PublicRoomDiscoveryTests
{
    private static readonly PublicRoomDto[] Rooms =
    [
        new(3, "103", "Gò Vấp", "Quang Trung", 3000000, 30, 3, 1, ""),
        new(1, "101", "Thủ Đức", "Võ Văn Ngân", 2000000, 20, 2, 2, ""),
        new(2, "102", "Gò Vấp", "Quang Trung", 3000000, 25, 2, 1, "")
    ];

    [Fact]
    public void Apply_CombinesAreaBudgetSizeAndOccupants()
    {
        var search = new PublicRoomSearch(2500000, 3500000, 26, "go vap", 3, "mac-dinh");
        Assert.Equal([3], PublicRoomDiscovery.Apply(Rooms, search).Select(r => r.PhongTroId));
    }

    [Theory]
    [InlineData("gia-tang", 1, 2, 3)]
    [InlineData("gia-giam", 2, 3, 1)]
    [InlineData("dien-tich-giam", 3, 2, 1)]
    [InlineData("mac-dinh", 3, 1, 2)]
    public void Apply_OrdersWholeResultWithStableTies(string sort, int first, int second, int third)
    {
        var query = new PublicRoomSearch(Sort: sort);
        Assert.Equal([first, second, third], PublicRoomDiscovery.Apply(Rooms, query).Select(r => r.PhongTroId));
    }

    [Fact]
    public void Normalize_SwapsBudgetBoundsAndTrimsArea()
    {
        var query = PublicRoomDiscovery.Normalize(new(5000000, 2000000, 20, "  Gò Vấp  ", 2, "invalid"));
        Assert.Equal(2000000, query.MinPrice);
        Assert.Equal(5000000, query.MaxPrice);
        Assert.Equal("Gò Vấp", query.Area);
        Assert.Equal("mac-dinh", query.Sort);
    }

    [Fact]
    public void Normalize_DiscardsInvalidBounds()
    {
        var query = PublicRoomDiscovery.Normalize(new(-1, -2, double.NaN, " ", -1));
        Assert.Null(query.MinPrice);
        Assert.Null(query.MaxPrice);
        Assert.Null(query.MinArea);
        Assert.Null(query.Area);
        Assert.Null(query.Occupants);
    }
}
