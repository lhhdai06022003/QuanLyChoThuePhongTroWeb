using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;

public interface ILichXemPhongService
{
    Task<IReadOnlyList<ViewingSlotDto>> GetOpenSlotsAsync(string code);
    Task<ViewingSubmissionResult> CreateAsync(CreateViewingRequest request, int? actorId = null);
    Task<IReadOnlyList<StaffViewingDto>> GetStaffQueueAsync(int actorId);
    Task<IReadOnlyList<ViewingSlotDto>> GetRescheduleSlotsAsync(int requestId, int actorId);
    Task<IReadOnlyList<GuestViewingDto>> GetMineAsync(int actorId);
    Task<bool> DecideAsync(int requestId, int actorId, bool confirm);
    Task<bool> ManageConfirmedAsync(int requestId, int actorId, ManageViewingRequest request);
}
