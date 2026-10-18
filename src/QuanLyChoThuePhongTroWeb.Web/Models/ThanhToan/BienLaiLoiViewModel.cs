using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Models.ThanhToan
{
    // Trang lỗi thân thiện khi không mở được biên lai (không tìm thấy, đã hủy ghi nhận, không thuộc quyền xem).
    // Dùng chung cho cổng khách thuê và cổng quản lý; mã HTTP vẫn theo loại lỗi (xem ToHttpStatusCode).
    public class BienLaiLoiViewModel
    {
        public string TieuDe { get; set; } = string.Empty;
        public string ThongDiep { get; set; } = string.Empty;
        public string QuayLaiUrl { get; set; } = string.Empty;
        public string QuayLaiText { get; set; } = string.Empty;

        public static BienLaiLoiViewModel From(ServiceResult result, string quayLaiUrl, string quayLaiText)
        {
            return new BienLaiLoiViewModel
            {
                TieuDe = result.Message,
                ThongDiep = result.ErrorKind == ServiceErrorKind.Forbidden
                    ? "Biên lai này thuộc chi nhánh bạn chưa được phân công."
                    : "Biên lai không tồn tại, đã bị hủy ghi nhận hoặc không thuộc hóa đơn của bạn.",
                QuayLaiUrl = quayLaiUrl,
                QuayLaiText = quayLaiText
            };
        }
    }
}
