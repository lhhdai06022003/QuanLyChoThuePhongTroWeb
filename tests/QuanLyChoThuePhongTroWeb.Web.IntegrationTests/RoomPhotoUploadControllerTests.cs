using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

public sealed class RoomPhotoUploadControllerTests
{
    [Fact]
    public async Task UploadsEachSelectedPhotoAndReportsTheCount()
    {
        var photos = new FakePhotos();
        var controller = CreateController(photos);
        var files = new List<IFormFile> { Photo("a.png"), Photo("b.png"), Photo("c.png") };

        var result = await controller.TaiAnh(42, files, "Ảnh phòng");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(3, photos.UploadCount);
        Assert.Contains("3", controller.TempData["PhotoSuccess"]?.ToString());
    }

    [Fact]
    public async Task RejectsMoreThanTenPhotosBeforeUploading()
    {
        var photos = new FakePhotos();
        var controller = CreateController(photos);
        var files = Enumerable.Range(0, 11).Select(i => (IFormFile)Photo($"{i}.png")).ToList();

        await controller.TaiAnh(42, files, null);

        Assert.Equal(0, photos.UploadCount);
        Assert.Contains("10", controller.TempData["PhotoError"]?.ToString());
    }

    [Fact]
    public async Task StorageConnectionFailureReturnsToPhotoFormWithMessage()
    {
        var photos = new FakePhotos { FailConnection = true };
        var controller = CreateController(photos);

        var result = await controller.TaiAnh(42, new List<IFormFile> { Photo("a.png") }, null);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Contains("tải ảnh", controller.TempData["PhotoError"]?.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static TinPhongController CreateController(FakePhotos photos)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, "Admin") }, "Test"));
        return new TinPhongController(new FakeListings(), photos)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new MemoryTempDataProvider())
        };
    }

    private static FormFile Photo(string name)
    {
        var content = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        return new FormFile(content, 0, content.Length, "images", name)
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
    }

    private sealed class FakeListings : ITinPhongService
    {
        public Task<IReadOnlyList<ManagedRoomDto>> GetManageableAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<ManagedRoomDto>>(Array.Empty<ManagedRoomDto>());
        public Task<RoomManagementResult> UpdateAsync(int roomId, int actorId, bool published, string? title) =>
            Task.FromResult(new RoomManagementResult(false, "unused"));
    }

    private sealed class FakePhotos : IPhongAnhService
    {
        public int UploadCount { get; private set; }
        public bool FailConnection { get; init; }
        public Task<IReadOnlyList<ManagedRoomImageDto>> GetImagesAsync(int roomId, int actorId) =>
            Task.FromResult<IReadOnlyList<ManagedRoomImageDto>>(Array.Empty<ManagedRoomImageDto>());
        public Task<RoomManagementResult> AddAsync(int roomId, int actorId, UploadFile image, string? caption)
        {
            if (FailConnection) throw new HttpRequestException("Proxy unavailable");
            UploadCount++;
            return Task.FromResult(new RoomManagementResult(true, "Đã thêm ảnh phòng."));
        }
        public Task<RoomManagementResult> SetCoverAsync(int roomId, int imageId, int actorId) =>
            Task.FromResult(new RoomManagementResult(false, "unused"));
        public Task<RoomManagementResult> HideAsync(int roomId, int imageId, int actorId) =>
            Task.FromResult(new RoomManagementResult(false, "unused"));
    }

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
