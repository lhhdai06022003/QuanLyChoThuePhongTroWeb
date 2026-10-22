using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence
{
    // allowedBranchIds: null = không giới hạn (Admin); danh sách = chỉ các chi nhánh đó (rỗng thì không có dữ liệu).
    public interface IDashboardStore
    {
        Task<IReadOnlyList<BranchLookupItemDto>> GetActiveBranchesAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<(int Trong, int DaThue, int BaoTri)> GetRoomStatusCountsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<int> CountActiveContractsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardInvoiceStatDto>> GetInvoiceMonthlyStatsAsync(int? branchId, int year, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<int> CountUnpaidRoomsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardContractUtilityCheckDto>> GetActiveContractsForUtilityCheckAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardRecordedUtilityDto>> GetRecordedUtilitiesAsync(DateTime fromDate, CancellationToken cancellationToken = default);
        Task<int> CountExpiringContractsAsync(int? branchId, DateTime today, DateTime limitDate, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardUnpaidGroupDto>> GetUnpaidInvoicesGroupedAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardRecentPaymentDto>> GetRecentPaymentsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardRecentContractDto>> GetRecentContractsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
    }
}
