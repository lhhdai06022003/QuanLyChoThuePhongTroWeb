using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeAiAssistantStore : IAiAssistantStore
    {
        public sealed record StoreCall(string Method, IReadOnlyCollection<int>? Allowed, IReadOnlyDictionary<string, object?> Args);

        public List<StoreCall> Calls { get; } = new();
        public Exception? ThrowOnCall { get; set; }

        public List<AiRoomRow> RentedRoomsWithoutMeter { get; } = new();
        public List<AiUnpaidInvoiceRow> UnpaidInvoices { get; } = new();
        public AiRevenueTotalRow Revenue { get; set; } = new(0m, 0);
        public List<AiRoomRow> VacantRooms { get; } = new();
        public List<AiExpiringContractRow> ExpiringContracts { get; } = new();
        public List<AiTenantLookupRow> TenantLookup { get; } = new();
        public List<AiRoomRow> RoomsByNumber { get; } = new();
        public List<AiBranchRevenueRow> BranchRevenue { get; } = new();
        public List<AiMeterReadingRow> MeterReadings { get; } = new();
        public List<AiTenantContractRow> TenantContracts { get; } = new();
        public List<AiUnpaidInvoiceRow> TenantUnpaidInvoices { get; } = new();

        public IEnumerable<StoreCall> CallsOf(string method) => Calls.Where(c => c.Method == method);

        private void Record(string method, IReadOnlyCollection<int>? allowed, params (string Key, object? Value)[] args)
        {
            Calls.Add(new StoreCall(method, allowed, args.ToDictionary(a => a.Key, a => a.Value)));
            if (ThrowOnCall != null) throw ThrowOnCall;
        }

        public Task<IReadOnlyList<AiRoomRow>> GetRentedRoomsWithoutMeterReadingAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(GetRentedRoomsWithoutMeterReadingAsync), allowedBranchIds, ("thang", thang), ("nam", nam));
            return Task.FromResult<IReadOnlyList<AiRoomRow>>(RentedRoomsWithoutMeter.ToList());
        }

        public Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetUnpaidInvoicesAsync(int? thang, int? nam, int? phongTroId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(GetUnpaidInvoicesAsync), allowedBranchIds, ("thang", thang), ("nam", nam), ("phongTroId", phongTroId));
            return Task.FromResult<IReadOnlyList<AiUnpaidInvoiceRow>>(UnpaidInvoices.ToList());
        }

        public Task<AiRevenueTotalRow> GetRevenueAsync(DateTime fromUtc, DateTime toExclusiveUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(GetRevenueAsync), allowedBranchIds, ("fromUtc", fromUtc), ("toExclusiveUtc", toExclusiveUtc));
            return Task.FromResult(Revenue);
        }

        public Task<IReadOnlyList<AiRoomRow>> GetVacantRoomsAsync(decimal? giaToiDa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(GetVacantRoomsAsync), allowedBranchIds, ("giaToiDa", giaToiDa));
            return Task.FromResult<IReadOnlyList<AiRoomRow>>(VacantRooms.ToList());
        }

        public Task<IReadOnlyList<AiExpiringContractRow>> GetExpiringContractsAsync(DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(GetExpiringContractsAsync), allowedBranchIds, ("fromUtc", fromUtc), ("toUtc", toUtc));
            return Task.FromResult<IReadOnlyList<AiExpiringContractRow>>(ExpiringContracts.ToList());
        }

        public Task<IReadOnlyList<AiTenantLookupRow>> SearchTenantsAsync(string tuKhoa, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(SearchTenantsAsync), allowedBranchIds, ("tuKhoa", tuKhoa));
            return Task.FromResult<IReadOnlyList<AiTenantLookupRow>>(TenantLookup.ToList());
        }

        public Task<IReadOnlyList<AiRoomRow>> FindRoomsByNumberAsync(string soPhong, string? tenChiNhanh, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(FindRoomsByNumberAsync), allowedBranchIds, ("soPhong", soPhong), ("tenChiNhanh", tenChiNhanh));
            return Task.FromResult<IReadOnlyList<AiRoomRow>>(RoomsByNumber.ToList());
        }

        public Task<IReadOnlyList<AiBranchRevenueRow>> GetRevenueByBranchAsync(int thang, int nam, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default)
        {
            Record(nameof(GetRevenueByBranchAsync), allowedBranchIds, ("thang", thang), ("nam", nam));
            return Task.FromResult<IReadOnlyList<AiBranchRevenueRow>>(BranchRevenue.ToList());
        }

        public Task<IReadOnlyList<AiMeterReadingRow>> GetMeterReadingsAsync(IReadOnlyCollection<int> phongTroIds, int thang, int nam, CancellationToken ct = default)
        {
            Record(nameof(GetMeterReadingsAsync), null, ("phongTroIds", phongTroIds.ToList()), ("thang", thang), ("nam", nam));
            return Task.FromResult<IReadOnlyList<AiMeterReadingRow>>(MeterReadings.ToList());
        }

        public Task<IReadOnlyList<AiTenantContractRow>> GetActiveContractsOfTenantAsync(int nguoiThueId, CancellationToken ct = default)
        {
            Record(nameof(GetActiveContractsOfTenantAsync), null, ("nguoiThueId", nguoiThueId));
            return Task.FromResult<IReadOnlyList<AiTenantContractRow>>(TenantContracts.ToList());
        }

        public Task<IReadOnlyList<AiUnpaidInvoiceRow>> GetTenantUnpaidInvoicesAsync(int nguoiThueId, int? thang, int? nam, CancellationToken ct = default)
        {
            Record(nameof(GetTenantUnpaidInvoicesAsync), null, ("nguoiThueId", nguoiThueId), ("thang", thang), ("nam", nam));
            return Task.FromResult<IReadOnlyList<AiUnpaidInvoiceRow>>(TenantUnpaidInvoices.ToList());
        }
    }
}
