using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiSuCo
    {
        [Description("Chờ tiếp nhận")]
        ChoTiepNhan = 0,

        [Description("Đang xử lý")]
        DangXuLy = 1,

        [Description("Đã hoàn thành")]
        DaHoanThanh = 2,

        [Description("Đã từ chối/hủy")]
        DaHuy = 3
    }
}
