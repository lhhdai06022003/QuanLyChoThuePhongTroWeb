using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;

public interface IKhungGioXemPhongService
{
    Task<IReadOnlyList<ManagedViewingSlotDto>> GetManagedAsync(int actorId);
    Task<bool> CreateAsync(CreateViewingSlotRequest request, int actorId);
    Task<bool> CloseAsync(int id, int actorId);
}
