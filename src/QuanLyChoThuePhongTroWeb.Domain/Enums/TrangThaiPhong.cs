using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiPhong
    {
        [Description("Trống")]
        Trong = 0,

        [Description("Đã thuê")]
        DaThue = 1,

        [Description("Bảo trì")]
        BaoTri = 2
    }
}
