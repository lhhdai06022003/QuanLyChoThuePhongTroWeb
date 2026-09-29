using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

public interface IRoomPhotoStorage
{
    Task<StoredRoomPhoto> SaveAsync(int roomId, UploadFile file);
    Task DeleteAsync(string publicId);
}

public sealed record StoredRoomPhoto(string Url, string PublicId);
