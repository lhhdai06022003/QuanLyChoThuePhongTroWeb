namespace QuanLyChoThuePhongTroWeb.Helpers;

public static class GuestStatusText
{
    public static string Of(string? status) => status switch
    {
        "MoiTao" => "Chờ duyệt", "ChoThanhToan" => "Chờ chuyển tiền",
        "ChoXacNhanTien" or "ChoXacNhan" or "DaBaoChuyen" => "Chờ đối chiếu",
        "DangGiuCho" => "Đang giữ phòng", "DaChuyenHopDong" => "Đã lập hợp đồng",
        "TuChoi" => "Đã từ chối", "HetHan" => "Đã hết hạn", "DaHuy" => "Đã hủy",
        "DaXacNhan" => "Đã xác nhận", "DaHoanTat" => "Đã nhận đủ tiền",
        "ChoNhanVienXuLy" => "Chờ nhân viên xác nhận", "DaXacNhanLich" => "Đã xác nhận lịch",
        "DaXemPhong" => "Đã xem phòng", "ChoHoanTien" => "Chờ chuyển hoàn",
        "DangHoanTien" => "Đang hoàn tiền", "DaHoanTien" => "Đã hoàn đủ",
        "KhongHoan" => "Không hoàn tiền", "Trong" => "Trống",
        "DaThue" => "Đã thuê", "BaoTri" => "Bảo trì", _ => "Đang cập nhật"
    };
}
