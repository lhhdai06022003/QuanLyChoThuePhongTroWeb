using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiPhatHanhHoaDon
    {
        [Description("Nháp")]
        Nhap = 0,

        [Description("Chờ duyệt")]
        ChoDuyet = 1,

        [Description("Đã chốt")]
        DaChot = 2,

        [Description("Đã gửi")]
        DaGui = 3,

        [Description("Đã hủy")]
        DaHuy = 4
    }
}
