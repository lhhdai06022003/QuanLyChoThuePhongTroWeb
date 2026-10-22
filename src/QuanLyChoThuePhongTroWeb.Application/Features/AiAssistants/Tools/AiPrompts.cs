using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Tools
{
    public static class AiPrompts
    {
        public const string HetLuotCongCu = "Đã hết lượt tra cứu. Hãy trả lời bằng dữ liệu đã có và nói rõ phần nào chưa tra cứu được.";
        public const string ChuaHoTro = "Tôi chưa hỗ trợ câu hỏi này.";

        private static readonly IReadOnlyList<string> ManagerSuggestions = new[]
        {
            "Hóa đơn nào chưa thanh toán?",
            "Phòng nào còn trống?",
            "Hợp đồng nào hết hạn trong 30 ngày tới?",
            "Phòng nào chưa chốt điện nước tháng này?",
            "Doanh thu tháng này?"
        };

        private static readonly IReadOnlyList<string> TenantSuggestions = new[]
        {
            "Hợp đồng của tôi",
            "Tôi còn nợ hóa đơn nào?",
            "Chỉ số điện nước tháng này"
        };

        public static IReadOnlyList<string> Suggestions(AiCallerRole role)
            => role == AiCallerRole.KhachThue ? TenantSuggestions : ManagerSuggestions;

        public static string ForRole(AiCallerRole role, DateTime nowUtc)
        {
            var today = nowUtc.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var sb = new StringBuilder();

            if (role == AiCallerRole.KhachThue)
            {
                sb.AppendLine("Bạn là trợ lý AI của hệ thống quản lý cho thuê phòng trọ, đang trò chuyện với một KHÁCH THUÊ. Bạn chỉ hỗ trợ khách thuê xem thông tin của chính họ.");
            }
            else
            {
                sb.AppendLine("Bạn là trợ lý AI của hệ thống quản lý cho thuê phòng trọ, đang trò chuyện với NHÂN VIÊN QUẢN LÝ (Admin hoặc nhân viên). Bạn hỗ trợ tra cứu dữ liệu vận hành trong phạm vi chi nhánh của họ.");
            }

            sb.AppendLine($"Hôm nay là {today} (giờ Việt Nam).");
            sb.AppendLine("Quy tắc trả lời:");
            sb.AppendLine("- Chỉ dùng dữ liệu lấy từ các công cụ, tuyệt đối không bịa số liệu.");
            sb.AppendLine("- Khi kết quả có soLuong = 0, hãy nói rõ là không có dữ liệu phù hợp.");
            sb.AppendLine("- Khi kết quả có ghiChu, danh sách đã bị rút gọn: dùng tổng số và tổng tiền trong kết quả (không tự cộng từ danh sách) và nói lại nội dung ghiChu cho người dùng.");
            sb.AppendLine($"- Câu hỏi nằm ngoài các công cụ hiện có thì trả lời đúng câu \"{ChuaHoTro}\" kèm 2-3 gợi ý lấy từ danh sách: {string.Join(" | ", Suggestions(role))}.");
            sb.AppendLine("- Lời chào hỏi xã giao thì trả lời bình thường, thân thiện.");
            sb.AppendLine("- Nếu công cụ trả thanhCong = false với maLoi THAM_SO_KHONG_HOP_LE, hãy hỏi lại người dùng, không tự đoán tham số.");
            sb.AppendLine("- Nếu công cụ trả canChonChiNhanh = true, hãy hỏi người dùng muốn xem chi nhánh nào trong danh sách cacChiNhanh.");
            sb.AppendLine("- Nếu công cụ trả maLoi CONG_CU_KHONG_KHA_DUNG, hãy nói là chưa hỗ trợ yêu cầu này.");
            sb.AppendLine("- Bạn được gọi nhiều công cụ cùng lúc khi câu hỏi cần nhiều dữ liệu.");
            sb.AppendLine("- Với câu hỏi về hóa đơn chưa thanh toán mà người dùng không nói tháng, hãy gọi công cụ ngay không truyền tháng/năm, không hỏi lại.");
            sb.AppendLine("- Tiền ghi theo VND, có dấu phân cách hàng nghìn. Trả lời bằng tiếng Việt, định dạng Markdown, ngắn gọn.");

            if (role == AiCallerRole.KhachThue)
            {
                sb.AppendLine("- Không tiết lộ thông tin của người khác hoặc của phòng khác.");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
