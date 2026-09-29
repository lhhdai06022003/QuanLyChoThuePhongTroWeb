using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;

public sealed class LocalReservationProofStorage : IReservationProofStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private readonly string root;

    public LocalReservationProofStorage(IConfiguration configuration)
    {
        root = Path.GetFullPath(configuration["PrivateProofStorage:Directory"] ??
            Path.Combine(AppContext.BaseDirectory, "private-proofs"));
        if (root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Private proof storage cannot be placed under wwwroot.");
    }

    public async Task<string> SaveAsync(UploadFile file)
    {
        if (file.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Ảnh minh chứng phải có dung lượng tối đa 5 MB.");

        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await file.Content.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new InvalidDataException("Ảnh minh chứng phải có dung lượng tối đa 5 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }
        if (buffer.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Ảnh minh chứng phải có dung lượng tối đa 5 MB.");

        var bytes = buffer.ToArray();
        var extension = DetectExtension(bytes);
        if (extension is null) throw new InvalidDataException("Chỉ chấp nhận ảnh PNG, JPEG hoặc WebP.");
        Directory.CreateDirectory(root);
        var key = $"{Guid.NewGuid():N}{extension}";
        await using (var output = new FileStream(Path.Combine(root, key), FileMode.CreateNew,
            FileAccess.Write, FileShare.None))
        {
            await output.WriteAsync(bytes);
        }
        return key;
    }

    public async Task<StoredReservationProof?> ReadAsync(string key)
    {
        var extension = ValidateKey(key);
        if (extension is null) return null;
        var path = Path.Combine(root, key);
        if (!File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path);
        return new StoredReservationProof(bytes, extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            _ => "image/webp"
        });
    }

    public Task DeleteAsync(string key)
    {
        if (ValidateKey(key) is not null)
            File.Delete(Path.Combine(root, key));
        return Task.CompletedTask;
    }

    private static string? ValidateKey(string key)
    {
        if (key.Length < 36) return null;
        var extension = Path.GetExtension(key);
        return extension is ".jpg" or ".png" or ".webp" &&
            Guid.TryParseExact(key[..^extension.Length], "N", out _) ? extension : null;
    }

    private static string? DetectExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[]
            { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return ".jpg";
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
