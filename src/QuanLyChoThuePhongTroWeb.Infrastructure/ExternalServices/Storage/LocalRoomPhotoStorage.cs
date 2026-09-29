using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;

// Development storage for public room photos when an external image service is unavailable.
public sealed class LocalRoomPhotoStorage(string directory) : IRoomPhotoStorage
{
    private const long MaxBytes = 5_242_880;
    private readonly string root = Path.GetFullPath(directory);

    public async Task<StoredRoomPhoto> SaveAsync(int roomId, UploadFile file)
    {
        if (roomId <= 0 || file.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Ảnh phòng không hợp lệ.");

        var extension = file.ContentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => throw new InvalidDataException("Chỉ chấp nhận ảnh PNG, JPEG hoặc WebP.")
        };

        var header = new byte[12];
        var headerLength = 0;
        while (headerLength < header.Length)
        {
            var read = await file.Content.ReadAsync(header.AsMemory(headerLength));
            if (read == 0) break;
            headerLength += read;
        }
        if (!MatchesSignature(file.ContentType, header.AsSpan(0, headerLength)))
            throw new InvalidDataException("Nội dung tệp không phải ảnh hợp lệ.");

        var roomDirectory = Path.Combine(root, roomId.ToString());
        Directory.CreateDirectory(roomDirectory);
        var filename = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(roomDirectory, filename);
        try
        {
            await using (var destination = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await destination.WriteAsync(header.AsMemory(0, headerLength));
                var total = (long)headerLength;
                var buffer = new byte[81920];
                int read;
                while ((read = await file.Content.ReadAsync(buffer)) > 0)
                {
                    total += read;
                    if (total > MaxBytes) throw new InvalidDataException("Ảnh vượt quá 5 MB.");
                    await destination.WriteAsync(buffer.AsMemory(0, read));
                }
            }
            return new StoredRoomPhoto($"/uploads/rooms/{roomId}/{filename}",
                $"local-room/{roomId}/{filename}");
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task DeleteAsync(string publicId)
    {
        var parts = publicId.Split('/');
        if (parts.Length != 3 || parts[0] != "local-room" ||
            !int.TryParse(parts[1], out var roomId) || roomId <= 0 ||
            Path.GetFileName(parts[2]) != parts[2] ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(parts[2]), "N", out _) ||
            Path.GetExtension(parts[2]) is not (".png" or ".jpg" or ".webp"))
            return Task.CompletedTask;

        var path = Path.Combine(root, roomId.ToString(), parts[2]);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static bool MatchesSignature(string contentType, ReadOnlySpan<byte> header) =>
        contentType switch
        {
            "image/png" => header.Length >= 8 &&
                header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => header.Length >= 3 &&
                header[..3].SequenceEqual(new byte[] { 255, 216, 255 }),
            "image/webp" => header.Length >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
}
