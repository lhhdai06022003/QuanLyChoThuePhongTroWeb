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
        // DaThu: ledger chưa xóa của mọi hóa đơn chưa xóa trong tháng. ChoThu: chỉ hóa đơn DaGui chưa thanh toán đủ.
        Task<IReadOnlyList<DashboardInvoiceStatDto>> GetInvoiceMonthlyStatsAsync(int? branchId, int year, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        // Chỉ tính hóa đơn DaGui chưa thanh toán đủ.
        Task<int> CountUnpaidRoomsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        // Hóa đơn DaGui, chưa xóa, chưa thanh toán đủ, đã quá HanThanhToan (so với nowUtc); mọi kỳ.
        Task<DashboardOverdueSummaryDto> GetOverdueSummaryAsync(int? branchId, DateTime nowUtc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardRecentPaymentDto>> GetRecentPaymentsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardRecentContractDto>> GetRecentContractsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
    }
}
