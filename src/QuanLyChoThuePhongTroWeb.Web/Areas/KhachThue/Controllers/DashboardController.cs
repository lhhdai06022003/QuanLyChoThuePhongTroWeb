using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.ViewModels;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers;

[Area("KhachThue")]
[Authorize(Roles = "KhachThue,KhachVangLai")]
public class DashboardController(
    INguoiThueService nguoiThueService,
    IHoaDonService hoaDonService,
    IViewingRequestService viewingRequests,
    IReservationService reservations) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Forbid();

        var model = new CustomerDashboardViewModel
        {
            CustomerName = User.Identity?.Name ?? "Khách",
            Viewings = (await viewingRequests.ListMineAsync(actorId, cancellationToken)).Take(5).ToArray(),
            Reservations = (await reservations.ListMineAsync(actorId, cancellationToken)).Take(5).ToArray()
        };

        if (User.IsInRole("KhachThue") &&
            int.TryParse(User.FindFirstValue("NguoiThueId"), out var tenantId))
        {
            var tenant = await nguoiThueService.GetByIdAsync(tenantId);
            model.HasTenantProfile = tenant is not null;
            model.CustomerName = tenant?.HoVaTen ?? model.CustomerName;
            if (model.HasTenantProfile)
            {
                var invoices = await hoaDonService.GetHoaDonsByNguoiThueIdAsync(tenantId);
                model.OutstandingInvoiceAmount = invoices
                    .Where(h => h.TrangThaiPhatHanhValue == 3 &&
                        h.TrangThaiHoaDonValue != (int)AppTrangThaiHoaDon.DaThanhToan)
                    .Sum(h => h.ConLai);
            }
        }

        return View(model);
    }
}
