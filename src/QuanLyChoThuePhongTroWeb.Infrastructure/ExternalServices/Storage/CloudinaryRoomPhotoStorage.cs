using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;

public sealed class CloudinaryRoomPhotoStorage : IRoomPhotoStorage
{
    private readonly Cloudinary? cloudinary;

    public CloudinaryRoomPhotoStorage(IConfiguration configuration)
    {
        var name = configuration["Cloudinary:CloudName"];
        var key = configuration["Cloudinary:ApiKey"];
        var secret = configuration["Cloudinary:ApiSecret"];
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key) &&
            !string.IsNullOrWhiteSpace(secret))
        {
            cloudinary = new Cloudinary(new Account(name, key, secret));
            cloudinary.Api.Secure = true;
        }
    }

    public async Task<StoredRoomPhoto> SaveAsync(int roomId, UploadFile file)
    {
        if (file.Length is <= 0 or > 5_242_880 ||
            file.ContentType is not ("image/png" or "image/jpeg" or "image/webp"))
            throw new InvalidDataException("Ảnh phòng không hợp lệ.");
        if (cloudinary is null) throw new InvalidOperationException("Cloudinary chưa được cấu hình.");
        var result = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(file.FileName, file.Content),
            Folder = $"public-rooms/{roomId}",
            UniqueFilename = true,
            UseFilename = false,
            Overwrite = false
        });
        if (result.Error is not null || result.SecureUrl is null ||
            string.IsNullOrWhiteSpace(result.PublicId))
            throw new InvalidOperationException("Không thể tải ảnh phòng lên.");
        return new StoredRoomPhoto(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId)
    {
        if (cloudinary is not null && !string.IsNullOrWhiteSpace(publicId))
            await cloudinary.DestroyAsync(new DeletionParams(publicId));
    }
}
