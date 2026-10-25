using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

// Giữ chỗ còn khóa phòng khỏi trang công khai: chờ chuyển tiền chưa quá hạn, chờ đối soát, đang giữ chỗ.
// Trang công khai và màn Tin phòng dùng chung để không lệch nhau.
internal static class ActiveRoomHolds
{
    public static IQueryable<YeuCauGiuCho> Query(ApplicationDbContext context, DateTime nowUtc) =>
        context.YeuCauGiuChos.Where(hold =>
            hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan && hold.HanThanhToan > nowUtc ||
            hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
            hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho);
}
