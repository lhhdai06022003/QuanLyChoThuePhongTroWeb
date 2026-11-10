using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Đếm số lần gọi từng hàm, lưu tham số cuối và trả giá trị cấu hình sẵn.
    public class FakeDashboardStore : IDashboardStore
    {
        public int GetActiveBranchesCalls { get; private set; }
        public int GetRoomStatusCountsCalls { get; private set; }
        public int CountActiveContractsCalls { get; private set; }
        public int GetInvoiceMonthlyStatsCalls { get; private set; }
        public int GetCongNoCalls { get; private set; }
        public int GetTienMatCalls { get; private set; }
        public int GetRecentPaymentsCalls { get; private set; }
        public int GetRecentContractsCalls { get; private set; }

        public IReadOnlyCollection<int>? LastAllowedBranchIds { get; private set; }
        public int? LastBranchId { get; private set; }
        public (int Year, int Month, DateTime NowUtc)? LastCongNoArgs { get; private set; }

        public (DateTime TuUtc, DateTime DenUtc, int? NguoiXacNhanId)? LastTienMatArgs { get; private set; }
        public List<DashboardTienMatNguoiDto> TienMat { get; } = new();
        public List<DashboardInvoiceStatDto> MonthlyStats { get; } = new();
        public List<DashboardRecentPaymentDto> RecentPayments { get; } = new();
        public DashboardCongNoDto CongNo { get; set; } = new();

        public Task<IReadOnlyList<BranchLookupItemDto>> GetActiveBranchesAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            GetActiveBranchesCalls++;
            return Task.FromResult<IReadOnlyList<BranchLookupItemDto>>(new List<BranchLookupItemDto>());
        }

        public Task<(int Trong, int DaThue, int BaoTri)> GetRoomStatusCountsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            GetRoomStatusCountsCalls++;
            Ghi(branchId, allowedBranchIds);
            return Task.FromResult((0, 0, 0));
        }

        public Task<int> CountActiveContractsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            CountActiveContractsCalls++;
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<DashboardInvoiceStatDto>> GetInvoiceMonthlyStatsAsync(int? branchId, int year, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            GetInvoiceMonthlyStatsCalls++;
            return Task.FromResult<IReadOnlyList<DashboardInvoiceStatDto>>(MonthlyStats);
        }

        public Task<DashboardCongNoDto> GetCongNoAsync(int? branchId, int year, int month, DateTime nowUtc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            GetCongNoCalls++;
            LastCongNoArgs = (year, month, nowUtc);
            Ghi(branchId, allowedBranchIds);
            return Task.FromResult(CongNo);
        }

        public Task<IReadOnlyList<DashboardTienMatNguoiDto>> GetTienMatTheoNguoiAsync(int? branchId, DateTime tuUtc, DateTime denUtc, int? nguoiXacNhanId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            GetTienMatCalls++;
            LastTienMatArgs = (tuUtc, denUtc, nguoiXacNhanId);
            Ghi(branchId, allowedBranchIds);
            // Như store thật: lọc theo người xác nhận khi được yêu cầu.
            IEnumerable<DashboardTienMatNguoiDto> rows = TienMat;
            if (nguoiXacNhanId.HasValue)
            {
                rows = rows.Where(r => r.NguoiDungId == nguoiXacNhanId.Value);
            }

            return Task.FromResult<IReadOnlyList<DashboardTienMatNguoiDto>>(rows.ToList());
        }

        public Task<IReadOnlyList<DashboardRecentPaymentDto>> GetRecentPaymentsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            GetRecentPaymentsCalls++;
            return Task.FromResult<IReadOnlyList<DashboardRecentPaymentDto>>(RecentPayments);
        }

        public Task<IReadOnlyList<DashboardRecentContractDto>> GetRecentContractsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
        {
            GetRecentContractsCalls++;
            return Task.FromResult<IReadOnlyList<DashboardRecentContractDto>>(new List<DashboardRecentContractDto>());
        }

        private void Ghi(int? branchId, IReadOnlyCollection<int>? allowed)
        {
            LastBranchId = branchId;
            LastAllowedBranchIds = allowed;
        }
    }
}
