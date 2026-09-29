using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

public sealed class PhongAnhService(ITinPhongStore store, IRoomPhotoStorage storage) : IPhongAnhService
{
    public Task<IReadOnlyList<ManagedRoomImageDto>> GetImagesAsync(int roomId, int actorId) =>
        store.GetImagesAsync(roomId, actorId);

    public async Task<RoomManagementResult> AddAsync(int roomId, int actorId,
        UploadFile image, string? caption)
    {
        if (roomId <= 0 || actorId <= 0 || image.Length is <= 0 or > 5_242_880 ||
            caption?.Length > 255)
            return new(false, "Ảnh hoặc chú thích không hợp lệ (tối đa 5 MB).");
        if (!await store.CanManageAsync(roomId, actorId))
            return new(false, "Bạn không có quyền sửa ảnh phòng này.");

        StoredRoomPhoto saved;
        try { saved = await storage.SaveAsync(roomId, image); }
        catch (InvalidDataException) { return new(false, "Chỉ chấp nhận ảnh PNG, JPEG hoặc WebP."); }

        try
        {
            if (await store.AddImageAsync(roomId, actorId, saved, caption?.Trim()))
                return new(true, "Đã thêm ảnh phòng.");
            await storage.DeleteAsync(saved.PublicId);
            return new(false, "Phòng vừa thay đổi. Vui lòng thử lại.");
        }
        catch
        {
            await storage.DeleteAsync(saved.PublicId);
            throw;
        }
    }

    public async Task<RoomManagementResult> SetCoverAsync(int roomId, int imageId, int actorId) =>
        await store.SetCoverAsync(roomId, imageId, actorId)
            ? new(true, "Đã chọn ảnh bìa.") : new(false, "Không thể chọn ảnh bìa.");

    public async Task<RoomManagementResult> HideAsync(int roomId, int imageId, int actorId) =>
        await store.HideImageAsync(roomId, imageId, actorId)
            ? new(true, "Đã ẩn ảnh phòng.") : new(false, "Không thể ẩn ảnh phòng.");
}
