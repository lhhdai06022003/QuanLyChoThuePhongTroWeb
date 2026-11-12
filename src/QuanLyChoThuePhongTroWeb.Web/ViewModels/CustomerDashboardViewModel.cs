using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;

namespace QuanLyChoThuePhongTroWeb.ViewModels;

public sealed class CustomerDashboardViewModel
{
    public string CustomerName { get; set; } = "Khách";
    public bool HasTenantProfile { get; set; }
    public decimal OutstandingInvoiceAmount { get; set; }
    // Hóa đơn đã gửi, chưa thanh toán đủ, còn nợ > 0.
    public int OutstandingInvoiceCount { get; set; }
    // Hạn thanh toán sớm nhất (UTC) trong các hóa đơn trên; null khi không hóa đơn nào có hạn.
    public DateTime? NearestDueUtc { get; set; }
    // Đã qua thời điểm hạn trên. NearestDueDays: số ngày còn lại (chưa quá hạn) hoặc số ngày đã quá (quá hạn), theo ngày Việt Nam.
    public bool NearestDueOverdue { get; set; }
    public int NearestDueDays { get; set; }
    public IReadOnlyList<ViewingRequestDto> Viewings { get; set; } = [];
    public IReadOnlyList<ReservationDto> Reservations { get; set; } = [];
}
