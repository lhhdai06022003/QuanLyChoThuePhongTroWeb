namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Nhãn tiếng Việt cho trạng thái hẹn xem phòng, giữ chỗ, thanh toán giữ chỗ và minh chứng.
    // Các DTO trả tên enum dạng chuỗi nên màn của nhân viên phải đổi sang chữ dễ đọc.
    // Nhãn lấy theo cách các trang khách (YeuCauCuaToi, DatCoc) đang hiển thị; giá trị chưa có nhãn thì trả nguyên chuỗi.
    public static class GuestStatusText
    {
        public static string Viewing(string? status) => status switch
        {
            "ChoChonLich" => "Chờ chọn lịch",
            "ChoNhanVienXuLy" => "Chờ nhân viên xác nhận",
            "DaXacNhanLich" => "Đã xác nhận lịch",
            "DaXemPhong" => "Đã xem phòng",
            "TuChoi" => "Đã từ chối",
            "DaHuy" => "Đã hủy",
            _ => status ?? string.Empty
        };

        public static string Reservation(string? status) => status switch
        {
            "MoiTao" => "Chờ nhân viên duyệt",
            "ChoThanhToan" => "Chờ chuyển tiền",
            "ChoXacNhanTien" => "Chờ đối soát tiền",
            "DangGiuCho" => "Đang giữ chỗ",
            "DaChuyenHopDong" => "Đã ký hợp đồng",
            "DaHuy" => "Đã hủy",
            "HetHan" => "Đã hết hạn",
            "TuChoi" => "Đã từ chối",
            _ => status ?? string.Empty
        };

        public static string Payment(string? status) => status switch
        {
            null => "Chưa có",
            "ChoThanhToan" => "Chờ chuyển tiền",
            "DaBaoChuyen" => "Đã báo chuyển, chờ đối soát",
            "DangDoiChieu" => "Đang đối soát",
            "DaHoanTat" => "Đã nhận đủ",
            "TuChoi" => "Đã từ chối",
            "HetHan" => "Đã hết hạn",
            "DaHuy" => "Đã hủy",
            _ => status
        };

        public static string Evidence(string? status) => status switch
        {
            "ChoXacNhan" => "Chờ đối soát",
            "DaXacNhan" => "Đã đối soát",
            "TuChoi" => "Đã từ chối",
            _ => status ?? string.Empty
        };
    }
}
