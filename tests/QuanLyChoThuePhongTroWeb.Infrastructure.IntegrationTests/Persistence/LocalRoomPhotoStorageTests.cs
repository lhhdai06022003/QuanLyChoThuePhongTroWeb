using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

public sealed class LocalRoomPhotoStorageTests
{
    [Fact]
    public async Task StoresPublicImageWithOpaqueNameAndDeletesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"room-photo-test-{Guid.NewGuid():N}");
        try
        {
            var storage = new LocalRoomPhotoStorage(root);
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 };

            var photo = await storage.SaveAsync(32,
                new UploadFile(new MemoryStream(png), "my-private-name.png", "image/png", png.Length));

            Assert.StartsWith("/uploads/rooms/32/", photo.Url);
            Assert.DoesNotContain("my-private-name", photo.Url);
            Assert.EndsWith(".png", photo.Url);
            var filename = photo.Url.Split('/').Last();
            Assert.Equal(png, await File.ReadAllBytesAsync(Path.Combine(root, "32", filename)));
            await storage.DeleteAsync(photo.PublicId);
            Assert.False(File.Exists(Path.Combine(root, "32", filename)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsFileThatClaimsToBePngButIsNot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"room-photo-test-{Guid.NewGuid():N}");
        var storage = new LocalRoomPhotoStorage(root);

        await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(32,
            new UploadFile(new MemoryStream("not an image"u8.ToArray()), "fake.png", "image/png", 12)));
        Assert.False(Directory.Exists(root));
    }
}
