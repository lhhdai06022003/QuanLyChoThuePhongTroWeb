using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools
{
    public sealed record AiToolResult(bool Success, string Outcome, int ResultCount, string Json)
    {
        public const string ThanhCong = "ThanhCong";
        public const string LoiThamSo = "LoiThamSo";
        public const string KhongKhaDung = "KhongKhaDung";
        public const string VuotGioiHan = "VuotGioiHan";
        public const string LoiTruyVan = "LoiTruyVan";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Payload là object ẩn danh hoặc record; thanhCong và soLuong luôn được thêm vào.
        public static AiToolResult Ok(object payload, int soLuong)
        {
            var node = JsonSerializer.SerializeToNode(payload, JsonOptions) as JsonObject ?? new JsonObject();
            node["thanhCong"] = true;
            node["soLuong"] = soLuong;
            return new AiToolResult(true, ThanhCong, soLuong, node.ToJsonString(JsonOptions));
        }

        public static AiToolResult Error(string maLoi, string thongBao)
        {
            var outcome = maLoi switch
            {
                "THAM_SO_KHONG_HOP_LE" => LoiThamSo,
                "CONG_CU_KHONG_KHA_DUNG" => KhongKhaDung,
                "VUOT_GIOI_HAN" => VuotGioiHan,
                _ => LoiTruyVan
            };
            var json = JsonSerializer.Serialize(new { thanhCong = false, maLoi, thongBao }, JsonOptions);
            return new AiToolResult(false, outcome, 0, json);
        }
    }
}
