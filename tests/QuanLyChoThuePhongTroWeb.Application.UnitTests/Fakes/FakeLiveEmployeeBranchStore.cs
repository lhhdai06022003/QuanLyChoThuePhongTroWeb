using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    /// <summary>
    /// Fake IEmployeeBranchStore tính "đang hiệu lực" trực tiếp từ danh sách phân công sống
    /// (dùng chung với FakeEmployeeBranchAssignmentStore.Assignments), để kiểm tra rằng sau khi
    /// EmployeeBranchAssignmentService.RevokeAsync chạy, EmployeeAccessService.CanPerformAsync
    /// của nhân viên tại chi nhánh đó trả về false — không cần DB thật.
    /// </summary>
    public class FakeLiveEmployeeBranchStore : IEmployeeBranchStore
    {
        private readonly List<NhanVienChiNhanh> _assignments;
        public Dictionary<int, (Role Role, bool IsActive, bool IsDeleted)> Accounts { get; } = new();

        public FakeLiveEmployeeBranchStore(List<NhanVienChiNhanh> assignments)
        {
            _assignments = assignments;
        }

        public Task<EmployeeBranchActorDto?> GetActorWithActiveBranchesAsync(int actorId, CancellationToken cancellationToken = default)
        {
            if (!Accounts.TryGetValue(actorId, out var account) || account.IsDeleted || !account.IsActive)
            {
                return Task.FromResult<EmployeeBranchActorDto?>(null);
            }

            var activeBranchIds = _assignments
                .Where(a => a.NguoiDungId == actorId && a.IsActive && a.NgayThuHoi == null)
                .Select(a => a.ChiNhanhId)
                .ToList();

            return Task.FromResult<EmployeeBranchActorDto?>(new EmployeeBranchActorDto
            {
                NguoiDungId = actorId,
                Role = account.Role,
                IsActive = account.IsActive,
                ActiveBranchIds = activeBranchIds
            });
        }

        public Task<bool> HasActiveBranchAssignmentAsync(int actorId, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            if (!Accounts.TryGetValue(actorId, out var account) || account.IsDeleted || !account.IsActive || account.Role != Role.NhanVien)
            {
                return Task.FromResult(false);
            }

            var hasActive = _assignments.Any(a => a.NguoiDungId == actorId && a.ChiNhanhId == chiNhanhId && a.IsActive && a.NgayThuHoi == null);
            return Task.FromResult(hasActive);
        }
    }
}
