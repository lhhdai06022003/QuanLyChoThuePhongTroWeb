using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

public interface ITinPhongService
{
    Task<IReadOnlyList<ManagedRoomDto>> GetManageableAsync(int actorId);
    Task<RoomManagementResult> UpdateAsync(int roomId, int actorId, bool published, string? title);
}
