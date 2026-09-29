using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;

public interface ITinPhongStore
{
    Task<IReadOnlyList<ManagedRoomDto>> GetManageableAsync(int actorId);
    Task<bool> UpdateAsync(int roomId, int actorId, bool published, string? title);
    Task<bool> CanManageAsync(int roomId, int actorId);
    Task<IReadOnlyList<ManagedRoomImageDto>> GetImagesAsync(int roomId, int actorId);
    Task<bool> AddImageAsync(int roomId, int actorId, StoredRoomPhoto photo, string? caption);
    Task<bool> SetCoverAsync(int roomId, int imageId, int actorId);
    Task<bool> HideImageAsync(int roomId, int imageId, int actorId);
}
