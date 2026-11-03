using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence
{
    public interface IInvoiceViewStore
    {
        Task<DataTableResponse<HoaDonRes>> GetEmployeeListAsync(DataTableRequest request, InvoiceListFilter filter, IReadOnlyList<int>? allowedBranchIds, CancellationToken ct = default);
        Task<int?> GetViewableBranchIdAsync(int hoaDonId, CancellationToken ct = default);
        Task<HoaDonChiTietRes?> GetDetailAsync(int hoaDonId, bool includeHistory, CancellationToken ct = default);
        // Dữ kiện kỳ điện nước của hóa đơn để nhận biết đổi đồng hồ; null khi hóa đơn không có kỳ hoặc kỳ đã xóa.
        Task<InvoiceMeterBaselineFacts?> GetMeterBaselineFactsAsync(int hoaDonId, CancellationToken ct = default);
    }

    // MoiDienKyTruoc/MoiNuocKyTruoc null khi chưa có kỳ trước. GhiChu* là ghi chú xác nhận của ảnh chính thức (chưa xóa) theo loại.
    public sealed record InvoiceMeterBaselineFacts(
        decimal ChiSoDienCu,
        decimal ChiSoNuocCu,
        decimal? MoiDienKyTruoc,
        decimal? MoiNuocKyTruoc,
        string? GhiChuDien,
        string? GhiChuNuoc,
        decimal DonGiaDien,
        decimal DonGiaNuoc);
}
