using System.Text.Json;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GuestAssistant;

internal static class GuestAssistantTools
{
    public const string Search = "TimPhongCongKhai";
    public const string Detail = "XemPhongCongKhai";
    public static IReadOnlyList<AiToolDefinition> Catalog { get; } = [
        new(Search, "Tìm các phòng hiện đang công khai, còn trống. Giá tính bằng VND (3 triệu = 3000000). Chỉ truyền bộ lọc khách đã nói, không tự thêm khu vực/ngân sách.", [
            new("giaTu", AiToolParameterType.Number, "Giá thuê tối thiểu VND, từ 0."),
            new("giaDen", AiToolParameterType.Number, "Giá thuê tối đa VND, từ 0."),
            new("khuVuc", AiToolParameterType.String, "Khu vực, địa chỉ hoặc chi nhánh, tối đa 100 ký tự."),
            new("dienTichTu", AiToolParameterType.Number, "Diện tích tối thiểu m², lớn hơn 0."),
            new("soNguoi", AiToolParameterType.Integer, "Số người dự định ở, từ 1 đến 100.")]),
        new(Detail, "Xem dữ liệu công khai mới nhất và mức cọc của một phòng theo ID trong thẻ phòng/ngữ cảnh trang; không đoán ID từ số phòng.", [
            new("phongId", AiToolParameterType.Integer, "ID phòng từ kết quả tìm kiếm hoặc trang đang xem.", true)])
    ];

    public static PublicRoomSearch ParseSearch(JsonElement args)
    {
        EnsureKeys(args, ["giaTu", "giaDen", "khuVuc", "dienTichTu", "soNguoi"]);
        var min = Decimal(args, "giaTu");
        var max = Decimal(args, "giaDen");
        var size = Decimal(args, "dienTichTu");
        var people = Integer(args, "soNguoi");
        if (min < 0 || max < 0 || min > max || size <= 0 || people is < 1 or > 100)
            throw new JsonException();
        string? area = null;
        if (Value(args, "khuVuc") is { } value)
        {
            if (value.ValueKind != JsonValueKind.String) throw new JsonException();
            area = value.GetString()?.Trim();
            if (area?.Length > 100) throw new JsonException();
        }
        return new(min, max, (double?)size, area, people, "gia-tang");
    }

    public static int ParseId(JsonElement args)
    {
        EnsureKeys(args, ["phongId"]);
        return Integer(args, "phongId") is > 0 and { } id ? id : throw new JsonException();
    }

    private static void EnsureKeys(JsonElement args, string[] keys)
    {
        if (args.ValueKind != JsonValueKind.Object || args.EnumerateObject().Any(p => !keys.Contains(p.Name)))
            throw new JsonException();
    }
    private static JsonElement? Value(JsonElement args, string key) => args.TryGetProperty(key, out var value)
        && value.ValueKind != JsonValueKind.Null ? value : null;
    private static decimal? Decimal(JsonElement args, string key) => Value(args, key) is not { } value ? null
        : value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : throw new JsonException();
    private static int? Integer(JsonElement args, string key) => Value(args, key) is not { } value ? null
        : value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : throw new JsonException();
}
