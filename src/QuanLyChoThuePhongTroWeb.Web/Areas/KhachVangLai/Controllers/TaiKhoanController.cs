using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;

[Area("KhachVangLai")]
[Route("tai-khoan")]
public sealed class TaiKhoanController(IKhachVangLaiService guests) : Controller
{
    [Authorize(Roles = "KhachVangLai")]
    [HttpGet("ho-so")]
    public async Task<IActionResult> HoSo(string? returnUrl = null)
    {
        var profile = await guests.GetProfileAsync(ActorId);
        if (profile is null) return NotFound();
        return View(new HoSoViewModel
        {
            HoTen = profile.HoTen,
            SoDienThoai = profile.SoDienThoai,
            Email = profile.Email ?? string.Empty,
            CCCD = profile.CCCD ?? string.Empty,
            ReturnUrl = returnUrl
        });
    }

    [Authorize(Roles = "KhachVangLai")]
    [HttpPost("ho-so")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HoSo(HoSoViewModel model)
    {
        var profile = await guests.GetProfileAsync(ActorId);
        if (profile is null) return NotFound();
        model.HoTen = profile.HoTen;
        model.SoDienThoai = profile.SoDienThoai;
        if (!ModelState.IsValid) return View(model);
        var result = await guests.UpdateIdentityAsync(ActorId,
            new UpdateGuestIdentityRequest(model.Email, model.CCCD));
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) &&
            model.ReturnUrl.StartsWith("/giu-cho", StringComparison.OrdinalIgnoreCase))
            return LocalRedirect(model.ReturnUrl);
        TempData["ProfileSuccess"] = result.Message;
        return RedirectToAction(nameof(HoSo));
    }

    [AllowAnonymous]
    [HttpGet("dang-ky")]
    public IActionResult DangKy(string? returnUrl = null) =>
        View(new DangKyViewModel { ReturnUrl = GuestReturnUrl(returnUrl) });

    [AllowAnonymous]
    [HttpPost("dang-ky")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("guest-viewing")]
    public async Task<IActionResult> DangKy(DangKyViewModel model)
    {
        model.ReturnUrl = GuestReturnUrl(model.ReturnUrl);
        if (!ModelState.IsValid) return View(model);
        var result = await guests.RegisterAsync(new GuestRegistrationRequest(model.TenDangNhap,
            model.MatKhau, model.HoTen, model.SoDienThoai, model.Email, model.CCCD));
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(model.TenDangNhap), result.Message);
            return View(model);
        }
        TempData["AccountSuccess"] = "Tài khoản đã được tạo. Bạn có thể đăng nhập ngay.";
        return RedirectToAction("DangNhap", "NguoiDung", new
            { area = "QuanLyNhaTro", returnUrl = model.ReturnUrl });
    }

    [AllowAnonymous]
    [HttpGet("dang-nhap")]
    public IActionResult DangNhap(string? returnUrl = null) =>
        RedirectToAction("DangNhap", "NguoiDung", new { area = "QuanLyNhaTro", returnUrl });

    private string? GuestReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) &&
        (returnUrl.StartsWith("/lich-xem/", StringComparison.OrdinalIgnoreCase) ||
         returnUrl.StartsWith("/giu-cho/tao/", StringComparison.OrdinalIgnoreCase))
            ? returnUrl : null;

    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
