using System.Globalization;
using System.Text;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public static class PublicRoomAreaSearch
{
    public static bool Matches(PublicRoomDto room, string? area)
    {
        if (string.IsNullOrWhiteSpace(area))
            return true;

        var location = Normalize($"{room.TenChiNhanh} {room.DiaChi}");
        return location.Contains(Normalize(area), StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var folded = char.ToLowerInvariant(character) == 'đ'
                ? 'd'
                : char.ToLowerInvariant(character);
            if (char.IsWhiteSpace(folded))
            {
                if (!previousWasSpace)
                    result.Append(' ');
                previousWasSpace = true;
            }
            else
            {
                result.Append(folded);
                previousWasSpace = false;
            }
        }

        return result.ToString().Trim();
    }
}
