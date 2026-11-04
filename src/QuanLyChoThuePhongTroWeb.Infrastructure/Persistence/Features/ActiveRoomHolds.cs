using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

// Chỉ ẩn phòng công khai sau khi đã nhận đủ tiền và giữ chỗ còn hiệu lực.
// Lượt chờ thanh toán vẫn được unique index bảo vệ, nhưng không che tin phòng.
internal static class ActiveRoomHolds
{
    public static IQueryable<YeuCauGiuCho> Query(ApplicationDbContext context, DateTime nowUtc) =>
        context.YeuCauGiuChos.Where(hold =>
            hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho);
}
