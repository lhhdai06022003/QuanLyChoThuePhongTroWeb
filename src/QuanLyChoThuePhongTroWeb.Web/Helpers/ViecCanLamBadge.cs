using QuanLyChoThuePhongTroWeb.Application.Features.ViecCanLam.DTOs;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Huy hiệu "Việc cần làm" trên menu (wwwroot/js/viec-can-lam-badge.js) thường tự gọi /QuanLyNhaTro/ViecCanLam/TomTat.
    // Trang nào đã tính tổng hợp cùng phạm vi (actor hiện tại, không lọc chi nhánh) thì đặt vào ViewData theo khóa này,
    // layout in sẵn thành window.viecCanLamTomTat để huy hiệu không gọi lại (docs/features/toi-uu-hieu-nang, bước 9).
    public static class ViecCanLamBadge
    {
        public const string ViewDataKey = "ViecCanLamTomTat";

        // Cùng các trường mà TomTat trả về.
        public static object Payload(ViecCanLamTongHopDto t) => new
        {
            tongSo = t.TongSo,
            soKhan = t.SoKhan,
            soCanLam = t.SoCanLam,
            soTheoDoi = t.SoTheoDoi
        };
    }
}
