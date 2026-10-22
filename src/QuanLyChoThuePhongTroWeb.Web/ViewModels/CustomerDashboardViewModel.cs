using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

namespace QuanLyChoThuePhongTroWeb.ViewModels;

public sealed class CustomerDashboardViewModel
{
    public string CustomerName { get; set; } = "Khách";
    public bool HasTenantProfile { get; set; }
    public decimal OutstandingInvoiceAmount { get; set; }
    public IReadOnlyList<ViewingRequestDto> Viewings { get; set; } = [];
    public IReadOnlyList<ReservationDto> Reservations { get; set; } = [];
}
