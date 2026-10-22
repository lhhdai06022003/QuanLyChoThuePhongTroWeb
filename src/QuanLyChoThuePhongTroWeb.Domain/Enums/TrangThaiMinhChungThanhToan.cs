using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiMinhChungThanhToan
    {
        [Description("Chờ xác nhận")]
        ChoXacNhan = 0,

        [Description("Đã xác nhận")]
        DaXacNhan = 1,

        [Description("Từ chối")]
        TuChoi = 2
    }
}
