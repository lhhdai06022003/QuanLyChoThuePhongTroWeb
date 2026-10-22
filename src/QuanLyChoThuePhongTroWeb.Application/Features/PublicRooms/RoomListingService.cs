using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public sealed record RoomListingDto(int RoomId, int BranchId, string RoomNumber, string BranchName,
    decimal MonthlyRent, double Area, int Status, bool BranchActive, bool Published,
    string Title, string Description, int ImageCount, string? CoverUrl);
public sealed record ListingPhoto(string Url, string PublicId);
public sealed record SaveRoomListing(int RoomId, string Title, string Description, bool Publish,
    IReadOnlyList<UploadFile> Photos);

public interface IRoomListingStore
{
    Task<IReadOnlyList<RoomListingDto>> ListAsync(IReadOnlyCollection<int>? branchIds, CancellationToken ct);
    Task<RoomListingDto?> GetAsync(int roomId, CancellationToken ct);
    Task<bool> SaveAsync(int actorId, int branchId, SaveRoomListing request,
        IReadOnlyList<ListingPhoto> photos, CancellationToken ct);
}

public interface IRoomListingService
{
    Task<IReadOnlyList<RoomListingDto>> ListAsync(int actorId, CancellationToken ct = default);
    Task<ServiceResult> SaveAsync(int actorId, SaveRoomListing request, CancellationToken ct = default);
}

public sealed class RoomListingService(IRoomListingStore store, IEmployeeAccessService access,
    IMeterImageStorageService storage, ILogger<RoomListingService> logger) : IRoomListingService
{
    public async Task<IReadOnlyList<RoomListingDto>> ListAsync(int actorId, CancellationToken ct = default)
    {
        var scope = await access.GetScopeAsync(actorId, ct);
        return scope is null ? Array.Empty<RoomListingDto>() : await store.ListAsync(scope.AllowedBranchIds, ct);
    }

    public async Task<ServiceResult> SaveAsync(int actorId, SaveRoomListing request, CancellationToken ct = default)
    {
        var scope = await access.GetScopeAsync(actorId, ct);
        var room = await store.GetAsync(request.RoomId, ct);
        if (scope is null || room is null || !scope.CanAccessBranch(room.BranchId))
            return ServiceResult.Forbidden("Bạn không có quyền chỉnh sửa tin phòng này.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 255)
            return ServiceResult.Fail("Tiêu đề tin phải có từ 1 đến 255 ký tự.");
        if (request.Description.Length > 10000)
            return ServiceResult.Fail("Mô tả phòng tối đa 10.000 ký tự.");
        if (request.Publish && !room.Published && (room.Status != 0 || !room.BranchActive))
            return ServiceResult.Fail("Phòng chưa đủ điều kiện đăng công khai. Bạn vẫn có thể lưu ảnh khi ẩn tin.");
        if (room.ImageCount + request.Photos.Count > 10)
            return ServiceResult.Fail("Mỗi phòng tối đa 10 ảnh. Vui lòng chọn ít ảnh hơn.");
        foreach (var photo in request.Photos)
        {
            var error = ProofImageValidator.Validate(photo, 5 * 1024 * 1024);
            if (error is not null) return ServiceResult.Fail(error);
        }
        var uploaded = new List<ListingPhoto>();
        var committed = false;
        try
        {
            foreach (var photo in request.Photos)
            {
                var image = await storage.UploadAsync(photo, "room_listings", ct);
                uploaded.Add(new ListingPhoto(image.Url, image.PublicId));
                if (!Uri.TryCreate(image.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                    image.Url.Length > 500 || string.IsNullOrWhiteSpace(image.PublicId) || image.PublicId.Length > 200)
                    throw new InvalidOperationException("Invalid image storage response");
            }
            committed = await store.SaveAsync(actorId, room.BranchId, request with {
                Title = request.Title.Trim(), Description = request.Description.Trim() }, uploaded, ct);
            return committed ? ServiceResult.Ok("Đã lưu tin và ảnh phòng.") :
                ServiceResult.Fail("Phòng vừa thay đổi hoặc đã đủ 10 ảnh. Vui lòng tải lại trang.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không thể lưu tin/ảnh phòng {RoomId}", request.RoomId);
            return ServiceResult.Fail("Chưa lưu được tin và ảnh. Vui lòng kiểm tra kết nối hoặc cấu hình Cloudinary rồi thử lại.");
        }
        finally
        {
            if (!committed)
                foreach (var photo in uploaded)
                    try { await storage.DeleteAsync(photo.PublicId, CancellationToken.None); }
                    catch (Exception ex) { logger.LogWarning(ex, "Không thể dọn ảnh tải dở {PublicId}", photo.PublicId); }
        }
    }
}
