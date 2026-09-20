using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiHopDong
    {
        [Description("Đang hoạt động")]
        DangHoatDong = 1,

        [Description("Đã kết thúc")]
        DaKetThuc = 2,

        [Description("Đã hủy")]
        DaHuy = 3
    }
}
