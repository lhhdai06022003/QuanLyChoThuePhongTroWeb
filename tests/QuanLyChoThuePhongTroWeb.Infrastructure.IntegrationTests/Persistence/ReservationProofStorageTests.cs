using Microsoft.Extensions.Configuration;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

public class ReservationProofStorageTests
{
    [Fact]
    public async Task SaveAndRead_UsesOpaqueKeyAndRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"proof-test-{Guid.NewGuid():N}");
        try
        {
            var storage = new LocalReservationProofStorage(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                    { ["PrivateProofStorage:Directory"] = root }).Build());
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 };
            var key = await storage.SaveAsync(new UploadFile(new MemoryStream(png),
                "ignored.png", "image/png", png.Length));

            Assert.DoesNotContain("ignored", key);
            Assert.Equal(png, (await storage.ReadAsync(key))?.Bytes);
            Assert.Null(await storage.ReadAsync("../secret.png"));
            await storage.DeleteAsync(key);
            Assert.Null(await storage.ReadAsync(key));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsUnexpectedFileContent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"proof-test-{Guid.NewGuid():N}");
        var storage = new LocalReservationProofStorage(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                { ["PrivateProofStorage:Directory"] = root }).Build());

        await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(
            new UploadFile(new MemoryStream("<svg>"u8.ToArray()), "proof.png", "image/png", 5)));
        Assert.False(Directory.Exists(root));
    }
}
