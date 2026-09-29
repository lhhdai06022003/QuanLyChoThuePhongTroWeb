using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;

public interface IKhungGioXemPhongStore
{
    Task<IReadOnlyList<ManagedViewingSlotDto>> GetManagedAsync(int actorId);
    Task<bool> CreateAsync(CreateViewingSlotRequest request, int actorId);
    Task<bool> CloseAsync(int id, int actorId);
}
