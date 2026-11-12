using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.ViewModels;
using QuanLyChoThuePhongTroWeb.Filters;

namespace QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers;

[Area("KhachThue")]
[Authorize(Roles = "KhachThue,KhachVangLai")]
[YeuCauDoiMatKhau]
public class DashboardController(
    INguoiThueService nguoiThueService,
    IHoaDonService hoaDonService,
    IViewingRequestService viewingRequests,
    IReservationService reservations,
    TimeProvider timeProvider) : Controller
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
                var outstanding = invoices
                    .Where(h => h.TrangThaiPhatHanhValue == 3 &&
                        h.TrangThaiHoaDonValue != (int)AppTrangThaiHoaDon.DaThanhToan)
                    .ToList();
                model.OutstandingInvoiceAmount = outstanding.Sum(h => h.ConLai);
                // Số hóa đơn và hạn sớm nhất chỉ tính hóa đơn còn nợ thật (ConLai > 0); tổng tiền giữ cách tính cũ.
                var conNo = outstanding.Where(h => h.ConLai > 0).ToList();
                model.OutstandingInvoiceCount = conNo.Count;
                model.NearestDueUtc = conNo
                    .Where(h => h.HanThanhToanUtc.HasValue)
                    .Select(h => h.HanThanhToanUtc)
                    .Min();
                if (model.NearestDueUtc.HasValue)
                {
                    // So theo ngày Việt Nam (UTC+7) để "còn n ngày" không lệch ngày; quá hạn khi đã qua thời điểm hạn.
                    var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                    var homNayVn = nowUtc.AddHours(7).Date;
                    var ngayHanVn = model.NearestDueUtc.Value.AddHours(7).Date;
                    model.NearestDueOverdue = model.NearestDueUtc.Value < nowUtc;
                    model.NearestDueDays = Math.Abs((ngayHanVn - homNayVn).Days);
                }
            }
        }

        return View(model);
    }
}
