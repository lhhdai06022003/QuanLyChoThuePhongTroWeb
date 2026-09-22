using System.ComponentModel;

namespace QuanLyChoThuePhongTroWeb.Domain.Enums
{
    public enum TrangThaiXuLyAnhChiSo
    {
        [Description("Mới tải lên")]
        MoiTaiLen = 0,

        [Description("Đang xử lý")]
        DangXuLy = 1,

        [Description("Đọc được")]
        DocDuoc = 2,

        [Description("Lỗi xử lý")]
        Loi = 3,

        [Description("Không đọc được")]
        KhongDocDuoc = 4,

        [Description("Cần chụp lại")]
        CanChupLai = 5,

        [Description("Đã xác nhận")]
        DaXacNhan = 6,

        [Description("Đã thay thế")]
        DaThayThe = 7
    }
}
