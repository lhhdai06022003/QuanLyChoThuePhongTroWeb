using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;

public sealed class TinPhongController(ITinPhongService listings, IPhongAnhService photos) : AdminBaseController
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await listings.GetManageableAsync(ActorId));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhat(int id, bool published, string? title)
    {
        var result = await listings.UpdateAsync(id, ActorId, published, title);
        TempData[result.Success ? "ListingSuccess" : "ListingError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Anh(int id)
    {
        var room = (await listings.GetManageableAsync(ActorId)).FirstOrDefault(item => item.Id == id);
        if (room is null) return NotFound();
        ViewBag.Room = room;
        return View(await photos.GetImagesAsync(id, ActorId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(55_000_000)]
    public async Task<IActionResult> TaiAnh(int id, List<IFormFile>? images, string? caption)
    {
        if (images is null || images.Count == 0)
        {
            TempData["PhotoError"] = "Vui lòng chọn ít nhất một ảnh phòng.";
            return RedirectToAction(nameof(Anh), new { id });
        }
        if (images.Count > 10)
        {
            TempData["PhotoError"] = "Mỗi lần chỉ tải tối đa 10 ảnh.";
            return RedirectToAction(nameof(Anh), new { id });
        }

        var uploaded = 0;
        var errors = new List<string>();
        foreach (var image in images)
        {
            try
            {
                await using var stream = image.OpenReadStream();
                var result = await photos.AddAsync(id, ActorId,
                    new UploadFile(stream, image.FileName, image.ContentType, image.Length), caption);
                if (result.Success) uploaded++;
                else errors.Add(result.Message);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException)
            {
                errors.Add("Chưa cấu hình dịch vụ lưu ảnh phòng hoặc tải ảnh thất bại.");
                break;
            }
        }
        if (uploaded > 0) TempData["PhotoSuccess"] = $"Đã tải lên {uploaded} ảnh phòng.";
        if (errors.Count > 0)
            TempData["PhotoError"] = $"{images.Count - uploaded} ảnh chưa tải được. {string.Join("; ", errors.Distinct())}";
        return RedirectToAction(nameof(Anh), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChonAnhBia(int id, int imageId)
    {
        var result = await photos.SetCoverAsync(id, imageId, ActorId);
        TempData[result.Success ? "PhotoSuccess" : "PhotoError"] = result.Message;
        return RedirectToAction(nameof(Anh), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnAnh(int id, int imageId)
    {
        var result = await photos.HideAsync(id, imageId, ActorId);
        TempData[result.Success ? "PhotoSuccess" : "PhotoError"] = result.Message;
        return RedirectToAction(nameof(Anh), new { id });
    }

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
