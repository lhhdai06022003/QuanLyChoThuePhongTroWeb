using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Persistence
{
    // allowedBranchIds: null = không giới hạn (Admin); danh sách = chỉ các chi nhánh đó (rỗng thì không có dữ liệu).
    public interface IAiAssistantStore
    {
        Task<IReadOnlyList<AiRoomRow>> GetRentedRoomsWithoutMeterReadingAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetUnpaidInvoicesAsync(int? thang, int? nam, int? phongTroId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<AiRevenueTotalRow> GetRevenueAsync(DateTime fromUtc, DateTime toExclusiveUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<IReadOnlyList<AiRoomRow>> GetVacantRoomsAsync(decimal? giaToiDa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<IReadOnlyList<AiExpiringContractRow>> GetExpiringContractsAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<IReadOnlyList<AiTenantLookupRow>> SearchTenantsAsync(string tuKhoa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<IReadOnlyList<AiRoomRow>> FindRoomsByNumberAsync(string soPhong, string? tenChiNhanh, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<IReadOnlyList<AiBranchRevenueRow>> GetRevenueByBranchAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        Task<IReadOnlyList<AiMeterReadingRow>> GetMeterReadingsAsync(IReadOnlyCollection<int> phongTroIds, int thang, int nam, CancellationToken ct = default);
        Task<IReadOnlyList<AiTenantContractRow>> GetActiveContractsOfTenantAsync(int nguoiThueId, CancellationToken ct = default);
        Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetTenantUnpaidInvoicesAsync(int nguoiThueId, int? thang, int? nam, CancellationToken ct = default);
    }
}
