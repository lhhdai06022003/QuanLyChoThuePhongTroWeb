using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

public sealed class TinPhongService(ITinPhongStore store) : ITinPhongService
{
    public Task<IReadOnlyList<ManagedRoomDto>> GetManageableAsync(int actorId) =>
        store.GetManageableAsync(actorId);

    public async Task<RoomManagementResult> UpdateAsync(int roomId, int actorId,
        bool published, string? title)
    {
        title = title?.Trim();
        if (roomId <= 0 || actorId <= 0 || (published && string.IsNullOrWhiteSpace(title)) ||
            title?.Length > 255)
            return new(false, "Vui lòng nhập tiêu đề hợp lệ khi đăng tin.");
        var saved = await store.UpdateAsync(roomId, actorId, published, title);
        return saved ? new(true, published ? "Đã đăng tin phòng." : "Đã ẩn tin phòng.")
            : new(false, "Không thể cập nhật tin hoặc bạn không có quyền ở chi nhánh này.");
    }
}
