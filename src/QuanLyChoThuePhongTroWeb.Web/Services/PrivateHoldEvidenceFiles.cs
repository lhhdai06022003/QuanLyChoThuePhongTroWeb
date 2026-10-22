namespace QuanLyChoThuePhongTroWeb.Services;

public sealed class PrivateHoldEvidenceFiles(IWebHostEnvironment environment)
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private string DirectoryPath => Path.Combine(environment.ContentRootPath,
        "App_Data", "hold-evidence");

    public async Task<string> SaveAsync(IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Ảnh cần có dung lượng từ 1 byte đến 5 MB.");

        await using var buffer = new MemoryStream();
        await using var input = file.OpenReadStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new InvalidDataException("Ảnh vượt quá 5 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        var bytes = buffer.ToArray();
        var kind = DetectKind(bytes);
        if (kind is null)
            throw new InvalidDataException("Chỉ nhận ảnh JPG, PNG hoặc WebP hợp lệ.");

        var declaredType = file.ContentType.ToLowerInvariant();
        if (declaredType != kind.Value.MimeType)
            throw new InvalidDataException("Loại ảnh không khớp nội dung tệp.");

        Directory.CreateDirectory(DirectoryPath);
        var fileName = Guid.NewGuid().ToString("N") + kind.Value.Extension;
        var path = Path.Combine(DirectoryPath, fileName);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return fileName;
    }

    public void Delete(string fileName)
    {
        if (IsSafeFileName(fileName))
            File.Delete(Path.Combine(DirectoryPath, fileName));
    }

    public string? GetPath(string fileName) =>
        IsSafeFileName(fileName) ? Path.Combine(DirectoryPath, fileName) : null;

    public static string ContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

    private static bool IsSafeFileName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return stem.Length == 32 && stem.All(Uri.IsHexDigit) &&
            Path.GetExtension(fileName).ToLowerInvariant() is
                ".jpg" or ".png" or ".webp";
    }

    private static (string Extension, string MimeType)? DetectKind(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 &&
            bytes[2] == 0xff)
            return (".jpg", "image/jpeg");
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return (".png", "image/png");
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        return null;
    }
}
