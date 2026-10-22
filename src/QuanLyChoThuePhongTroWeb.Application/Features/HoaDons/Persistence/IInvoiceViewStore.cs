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
    }
}
