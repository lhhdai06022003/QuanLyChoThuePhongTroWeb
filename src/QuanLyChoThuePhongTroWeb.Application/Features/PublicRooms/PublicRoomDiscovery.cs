namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public sealed record PublicRoomSearch(decimal? MinPrice = null, decimal? MaxPrice = null,
    double? MinArea = null, string? Area = null, int? Occupants = null, string Sort = "mac-dinh");

public static class PublicRoomDiscovery
{
    public static PublicRoomSearch Normalize(PublicRoomSearch search)
    {
        var min = search.MinPrice >= 0 ? search.MinPrice : null;
        var max = search.MaxPrice >= 0 ? search.MaxPrice : null;
        if (min.HasValue && max.HasValue && min > max)
            (min, max) = (max, min);
        var area = string.IsNullOrWhiteSpace(search.Area) ? null : search.Area.Trim();
        return search with
        {
            MinPrice = min, MaxPrice = max,
            MinArea = search.MinArea is { } size && double.IsFinite(size) && size > 0 ? size : null,
            Area = area is { Length: > 100 } ? area[..100] : area,
            Occupants = search.Occupants > 0 ? search.Occupants : null,
            Sort = search.Sort is "gia-tang" or "gia-giam" or "dien-tich-giam" ? search.Sort : "mac-dinh"
        };
    }

    public static IReadOnlyList<PublicRoomDto> Apply(IEnumerable<PublicRoomDto> rooms, PublicRoomSearch input)
    {
        var search = Normalize(input);
        var result = rooms.Where(room =>
            (!search.MinPrice.HasValue || room.GiaThue >= search.MinPrice.Value) &&
            (!search.MaxPrice.HasValue || room.GiaThue <= search.MaxPrice.Value) &&
            (!search.MinArea.HasValue || room.DienTich >= search.MinArea.Value) &&
            (!search.Occupants.HasValue || room.SoNguoiToiDa >= search.Occupants.Value) &&
            PublicRoomAreaSearch.Matches(room, search.Area));
        return (search.Sort switch
        {
            "gia-tang" => result.OrderBy(room => room.GiaThue).ThenBy(room => room.PhongTroId),
            "gia-giam" => result.OrderByDescending(room => room.GiaThue).ThenBy(room => room.PhongTroId),
            "dien-tich-giam" => result.OrderByDescending(room => room.DienTich).ThenBy(room => room.PhongTroId),
            _ => result
        }).ToArray();
    }
}
