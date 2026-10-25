using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class TinPhongController(IRoomListingService listings, IConfiguration configuration) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var publishedIds = configuration.GetSection("PublicRooms:RoomIds").Get<int[]>()?.ToHashSet();
        var rooms = await listings.ListAsync(CurrentActorId, ct);
        var items = rooms.Select(room => new ListingPreviewItem(room.RoomId, room.RoomNumber,
            room.BranchName, room.MonthlyRent, room.Area, room.Status,
            room.Status == 0 && room.Published && room.BranchActive && !room.HasActiveHold &&
                (publishedIds is null || publishedIds.Contains(room.RoomId)),
            room.ImageCount, room.CoverUrl, room.Title, room.Description, room.Published,
            room.HasActiveHold)).ToArray();
        return View(new ListingPreviewPage(items));
    }

    [HttpPost]
    [RequestSizeLimit(55 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 55 * 1024 * 1024)]
    public async Task<IActionResult> LuuTin(int roomId, string? title, string? description,
        bool publish, List<IFormFile>? photos, CancellationToken ct)
    {
        var streams = new List<Stream>();
        try
        {
            var files = photos ?? new List<IFormFile>();
            if (!ModelState.IsValid || files.Count > 10 || files.Any(f => f.Length <= 0 || f.Length > 5 * 1024 * 1024))
            {
                TempData["ListingMessage"] = "Chọn tối đa 10 ảnh JPG, PNG hoặc WebP, mỗi ảnh không quá 5 MB.";
                TempData["ListingWarning"] = true;
                return RedirectToAction(nameof(Index));
            }
            var uploads = files.Select(file => {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                return new UploadFile(stream, file.FileName, file.ContentType, file.Length);
            }).ToArray();
            var result = await listings.SaveAsync(CurrentActorId,
                new SaveRoomListing(roomId, title ?? "", description ?? "", publish, uploads), ct);
            if (result.ErrorKind == ServiceErrorKind.Forbidden) return Forbid();
            TempData["ListingMessage"] = result.Message;
            TempData["ListingWarning"] = !result.Success;
            return RedirectToAction(nameof(Index));
        }
        finally { foreach (var stream in streams) await stream.DisposeAsync(); }
    }
}
