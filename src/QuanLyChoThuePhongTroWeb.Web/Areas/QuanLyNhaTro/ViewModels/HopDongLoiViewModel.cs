namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    // Trang lỗi thân thiện khi không mở được trang chi tiết, thanh lý hoặc gia hạn hợp đồng.
    public class HopDongLoiViewModel
    {
        public string TieuDe { get; set; } = string.Empty;
        public string ThongDiep { get; set; } = string.Empty;
        public string QuayLaiUrl { get; set; } = "/QuanLyNhaTro/QuanLyHopDong";
        public string QuayLaiText { get; set; } = "Về danh sách hợp đồng";

        public static HopDongLoiViewModel From(ServiceResult result, string quayLaiUrl = "/QuanLyNhaTro/QuanLyHopDong", string quayLaiText = "Về danh sách hợp đồng")
        {
            return new HopDongLoiViewModel
            {
                TieuDe = result.ErrorKind == ServiceErrorKind.Forbidden ? "Không có quyền xem" : "Không mở được trang",
                ThongDiep = result.ErrorKind == ServiceErrorKind.Forbidden
                    ? "Hợp đồng thuộc chi nhánh bạn chưa được phân công."
                    : result.Message,
                QuayLaiUrl = quayLaiUrl,
                QuayLaiText = quayLaiText
            };
        }
    }
}
