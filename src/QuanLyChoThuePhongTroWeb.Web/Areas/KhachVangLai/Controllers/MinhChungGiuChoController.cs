using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachVangLai.Controllers;

[Area("KhachVangLai")]
[Authorize]
[Route("minh-chung-giu-cho")]
public sealed class MinhChungGiuChoController(IThanhToanGiuChoService payments) : Controller
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Anh(int id)
    {
        var actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId : 0;
        var proof = await payments.ReadProofAsync(id, actorId);
        if (proof is null) return NotFound();
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(proof.Bytes, proof.ContentType);
    }
}
