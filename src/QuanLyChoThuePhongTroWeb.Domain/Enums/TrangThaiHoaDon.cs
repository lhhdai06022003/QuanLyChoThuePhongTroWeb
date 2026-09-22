using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiHoaDon
    {
        [Description("Chưa thanh toán")]
        ChuaThanhToan = 0,

        [Description("Đã thanh toán")]
        DaThanhToan = 1,

        [Description("Thanh toán một phần")]
        ThanhToanMotPhan = 2
    }
}
