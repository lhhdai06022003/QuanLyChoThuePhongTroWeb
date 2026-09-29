using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiHoaDon
    {
        [Description("Chưa thanh toán")]
        ChuaThanhToan = 0,

        [Description("Đã thanh toán")]
        DaThanhToan = 1
    }
}
