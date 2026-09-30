using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeInvoiceViewStore : IInvoiceViewStore
    {
        public DataTableResponse<HoaDonRes> ListToReturn { get; set; } = new();
        public Dictionary<int, int?> BranchIds { get; } = new();
        public Dictionary<int, HoaDonChiTietRes> Details { get; } = new();

        public DataTableRequest? LastRequest { get; private set; }
        public InvoiceListFilter? LastFilter { get; private set; }
        public IReadOnlyList<int>? LastAllowedBranchIds { get; private set; }
        public bool? LastIncludeHistory { get; private set; }
        public int LastDetailId { get; private set; }
        public int GetDetailCallCount { get; private set; }

        public Task<DataTableResponse<HoaDonRes>> GetEmployeeListAsync(
            DataTableRequest request,
            InvoiceListFilter filter,
            IReadOnlyList<int>? allowedBranchIds,
            CancellationToken ct = default)
        {
            LastRequest = request;
            LastFilter = filter;
            LastAllowedBranchIds = allowedBranchIds;
            return Task.FromResult(ListToReturn);
        }

        public Task<int?> GetViewableBranchIdAsync(int hoaDonId, CancellationToken ct = default)
        {
            BranchIds.TryGetValue(hoaDonId, out var bId);
            return Task.FromResult(bId);
        }

        public Task<HoaDonChiTietRes?> GetDetailAsync(int hoaDonId, bool includeHistory, CancellationToken ct = default)
        {
            GetDetailCallCount++;
            LastDetailId = hoaDonId;
            LastIncludeHistory = includeHistory;
            Details.TryGetValue(hoaDonId, out var detail);
            return Task.FromResult<HoaDonChiTietRes?>(detail);
        }
    }
}
