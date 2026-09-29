using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

public interface IPhongAnhService
{
    Task<IReadOnlyList<ManagedRoomImageDto>> GetImagesAsync(int roomId, int actorId);
    Task<RoomManagementResult> AddAsync(int roomId, int actorId, UploadFile image, string? caption);
    Task<RoomManagementResult> SetCoverAsync(int roomId, int imageId, int actorId);
    Task<RoomManagementResult> HideAsync(int roomId, int imageId, int actorId);
}
