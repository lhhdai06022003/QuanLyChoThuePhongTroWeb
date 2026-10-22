using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiGhiNhan
    {
        [Description("Nháp")]
        Nhap = 0,

        [Description("Chờ duyệt")]
        ChoDuyet = 1,

        [Description("Đã duyệt")]
        DaDuyet = 2,

        [Description("Từ chối")]
        TuChoi = 3
    }
}
