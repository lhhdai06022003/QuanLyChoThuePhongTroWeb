using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    /// <summary>
    /// Fake dùng cho EmployeeBranchAssignmentServiceTests. Ghi lại số lần Add/lock để
    /// khẳng định các nhánh trả sớm (phân công lặp / thu hồi lặp) không đổi dữ liệu.
    /// </summary>
    public class FakeEmployeeBranchAssignmentStore : IEmployeeBranchAssignmentStore
    {
        public Dictionary<int, EmployeeAccountSnapshotDto> Employees { get; } = new();
        public HashSet<int> AssignableBranches { get; } = new();
        public List<NhanVienChiNhanh> Assignments { get; } = new();
        public List<EmployeeBranchSummaryDto> Summaries { get; set; } = new();
        public EmployeeBranchDetailDto? Detail { get; set; }
        public List<BranchOptionDto> BranchOptions { get; set; } = new();

        public int AddAssignmentCallCount { get; private set; }
        public int LockEmployeeCallCount { get; private set; }

        public Task<EmployeeAccountSnapshotDto?> LockEmployeeAsync(int nguoiDungId, CancellationToken ct = default)
        {
            LockEmployeeCallCount++;
            Employees.TryGetValue(nguoiDungId, out var employee);
            return Task.FromResult(employee);
        }

        public Task<bool> IsBranchAssignableAsync(int chiNhanhId, CancellationToken ct = default)
        {
            return Task.FromResult(AssignableBranches.Contains(chiNhanhId));
        }

        public Task<NhanVienChiNhanh?> GetAssignmentAsync(int nguoiDungId, int chiNhanhId, CancellationToken ct = default)
        {
            var assignment = Assignments.FirstOrDefault(a => a.NguoiDungId == nguoiDungId && a.ChiNhanhId == chiNhanhId);
            return Task.FromResult(assignment);
        }

        public Task AddAssignmentAsync(NhanVienChiNhanh assignment, CancellationToken ct = default)
        {
            AddAssignmentCallCount++;
            Assignments.Add(assignment);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EmployeeBranchSummaryDto>> GetEmployeeSummariesAsync(int? chiNhanhId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<EmployeeBranchSummaryDto>>(Summaries);
        }

        public Task<EmployeeBranchDetailDto?> GetEmployeeDetailAsync(int nguoiDungId, CancellationToken ct = default)
        {
            return Task.FromResult(Detail);
        }

        public Task<IReadOnlyList<BranchOptionDto>> GetBranchOptionsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<BranchOptionDto>>(BranchOptions);
        }
    }
}
