using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiYeuCauThanhToan
    {
        [Description("Chờ thanh toán")]
        ChoThanhToan = 0,

        [Description("Đã báo chuyển")]
        DaBaoChuyen = 1,

        [Description("Đang đối chiếu")]
        DangDoiChieu = 2,

        [Description("Đã hoàn tất")]
        DaHoanTat = 3,

        [Description("Từ chối")]
        TuChoi = 4,

        [Description("Hết hạn")]
        HetHan = 5,

        [Description("Đã hủy")]
        DaHuy = 6
    }
}
