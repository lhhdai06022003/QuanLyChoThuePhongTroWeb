using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;

public interface ILichXemPhongStore
{
    Task<IReadOnlyList<ViewingSlotDto>> GetOpenSlotsAsync(string code, DateTime nowUtc);
    Task<bool> CreateAsync(CreateViewingCommand command);
    Task<IReadOnlyList<StaffViewingDto>> GetStaffQueueAsync(int actorId);
    Task<IReadOnlyList<ViewingSlotDto>> GetRescheduleSlotsAsync(int requestId, int actorId, DateTime nowUtc);
    Task<IReadOnlyList<GuestViewingDto>> GetMineAsync(int actorId);
    Task<bool> DecideAsync(int requestId, int actorId, bool confirm);
    Task<bool> ManageConfirmedAsync(int requestId, int actorId, ManageViewingRequest request);
}
