using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;
using QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Controllers;

[Route("dang-ky-khach")]
public sealed class DangKyKhachController(IGuestRegistrationService registration) : Controller
{
    [HttpGet("")]
    public IActionResult Index(string? returnUrl = null)
    {
        return View(new GuestRegistrationModel
        {
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(GuestRegistrationModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await registration.RegisterAsync(new GuestRegistrationRequest(
            model.Username, model.Password, model.FullName, model.Phone, model.Email),
            cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        var returnUrl = Url.IsLocalUrl(model.ReturnUrl)
            ? model.ReturnUrl
            : "/tai-khoan/yeu-cau-phong";
        return RedirectToAction("DangNhap", "NguoiDung",
            new { area = "QuanLyNhaTro", returnUrl });
    }
}
