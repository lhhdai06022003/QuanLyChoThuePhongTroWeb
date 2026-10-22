using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IInvoiceViewService
    {
        Task<DataTableResponse<HoaDonRes>> GetEmployeeListAsync(DataTableRequest request, InvoiceListFilter filter, int actorId, CancellationToken ct = default);
        Task<HoaDonChiTietRes?> GetEmployeeDetailAsync(int hoaDonId, int actorId, CancellationToken ct = default);
        Task<byte[]?> ExportEmployeePdfAsync(int hoaDonId, int actorId, CancellationToken ct = default);
        Task<byte[]?> ExportEmployeeExcelAsync(int hoaDonId, int actorId, CancellationToken ct = default);
        Task<HoaDonChiTietRes?> GetTenantDetailAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default);
        Task<byte[]?> ExportTenantPdfAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default);
    }
}
