using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class EmployeeBranchAssignmentServiceTests
    {
        private class FakeApplicationTransaction : IApplicationTransaction
        {
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }
            public Task CommitAsync(CancellationToken cancellationToken = default) { Committed = true; return Task.CompletedTask; }
            public Task RollbackAsync(CancellationToken cancellationToken = default) { RolledBack = true; return Task.CompletedTask; }
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class FakeUnitOfWork : IUnitOfWork
        {
            public FakeApplicationTransaction? LastTransaction { get; private set; }
            public int SaveChangesCallCount { get; private set; }

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            {
                LastTransaction = new FakeApplicationTransaction();
                return Task.FromResult<IApplicationTransaction>(LastTransaction);
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCallCount++;
                return Task.FromResult(1);
            }
        }

        private const int AdminId = 1;
        private const int StaffId = 10;
        private const int OtherStaffId = 11;
        private const int TenantId = 20;
        private const int InactiveAdminId = 2;
        private const int BranchId = 100;
        private const int DeletedBranchId = 101;

        private readonly FakeEmployeeBranchAssignmentStore _assignmentStore = new();
        private readonly FakeLiveEmployeeBranchStore _branchStore;
        private readonly FakeUnitOfWork _uow = new();
        private readonly EmployeeBranchAssignmentService _service;

        public EmployeeBranchAssignmentServiceTests()
        {
            _branchStore = new FakeLiveEmployeeBranchStore(_assignmentStore.Assignments);

            _branchStore.Accounts[AdminId] = (Role.Admin, true, false);
            _branchStore.Accounts[InactiveAdminId] = (Role.Admin, false, false);
            _branchStore.Accounts[StaffId] = (Role.NhanVien, true, false);
            _branchStore.Accounts[OtherStaffId] = (Role.NhanVien, true, false);
            _branchStore.Accounts[TenantId] = (Role.KhachThue, true, false);

            _assignmentStore.Employees[StaffId] = new EmployeeAccountSnapshotDto { NguoiDungId = StaffId, Role = Role.NhanVien, IsActive = true, IsDeleted = false };
            _assignmentStore.Employees[OtherStaffId] = new EmployeeAccountSnapshotDto { NguoiDungId = OtherStaffId, Role = Role.NhanVien, IsActive = false, IsDeleted = false };
            _assignmentStore.Employees[AdminId] = new EmployeeAccountSnapshotDto { NguoiDungId = AdminId, Role = Role.Admin, IsActive = true, IsDeleted = false };
            _assignmentStore.Employees[TenantId] = new EmployeeAccountSnapshotDto { NguoiDungId = TenantId, Role = Role.KhachThue, IsActive = true, IsDeleted = false };
            _assignmentStore.Employees[999] = new EmployeeAccountSnapshotDto { NguoiDungId = 999, Role = Role.NhanVien, IsActive = true, IsDeleted = true };

            _assignmentStore.AssignableBranches.Add(BranchId);

            _service = new EmployeeBranchAssignmentService(_branchStore, _assignmentStore, _uow, NullLogger<EmployeeBranchAssignmentService>.Instance);
        }

        // ---------- Actor bị từ chối ----------

        [Fact]
        public async Task AssignAsync_ActorIsStaff_IsRejected_EvenIfStaffIsAssignedSomewhere()
        {
            _assignmentStore.Assignments.Add(new NhanVienChiNhanh { NguoiDungId = StaffId, ChiNhanhId = BranchId, IsActive = true, NguoiPhanCongId = AdminId, NgayPhanCong = DateTime.UtcNow });

            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = OtherStaffId, ChiNhanhId = BranchId }, actorId: StaffId);

            Assert.False(result.Success);
            Assert.Equal("Chỉ Quản trị viên đang hoạt động mới được quản lý phân công chi nhánh.", result.Message);
            Assert.Equal(0, _assignmentStore.AddAssignmentCallCount);
        }

        [Fact]
        public async Task GetEmployeesAsync_ActorIsTenant_IsRejected()
        {
            var result = await _service.GetEmployeesAsync(null, actorId: TenantId);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task RevokeAsync_ActorIsInactiveAdmin_IsRejected()
        {
            var result = await _service.RevokeAsync(new RevokeEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = "Nghỉ việc" }, actorId: InactiveAdminId);

            Assert.False(result.Success);
            Assert.Equal("Chỉ Quản trị viên đang hoạt động mới được quản lý phân công chi nhánh.", result.Message);
        }

        [Fact]
        public async Task GetEmployeesAsync_ActorIsStaff_IsRejected()
        {
            var result = await _service.GetEmployeesAsync(null, actorId: StaffId);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetEmployeeAsync_ActorIsStaff_IsRejected()
        {
            var result = await _service.GetEmployeeAsync(StaffId, actorId: StaffId);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetBranchOptionsAsync_ActorIsStaff_IsRejected()
        {
            var result = await _service.GetBranchOptionsAsync(actorId: StaffId);
            Assert.False(result.Success);
        }

        // ---------- Đích bị từ chối ----------

        [Fact]
        public async Task AssignAsync_TargetIsAdmin_IsRejected()
        {
            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = AdminId, ChiNhanhId = BranchId }, actorId: AdminId);

            Assert.False(result.Success);
            Assert.Equal("Tài khoản không tồn tại, đã bị xóa hoặc không phải nhân viên.", result.Message);
        }

        [Fact]
        public async Task AssignAsync_TargetIsTenant_IsRejected()
        {
            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = TenantId, ChiNhanhId = BranchId }, actorId: AdminId);

            Assert.False(result.Success);
            Assert.Equal("Tài khoản không tồn tại, đã bị xóa hoặc không phải nhân viên.", result.Message);
        }

        [Fact]
        public async Task AssignAsync_TargetIsDeleted_IsRejected()
        {
            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = 999, ChiNhanhId = BranchId }, actorId: AdminId);

            Assert.False(result.Success);
            Assert.Equal("Tài khoản không tồn tại, đã bị xóa hoặc không phải nhân viên.", result.Message);
        }

        [Fact]
        public async Task AssignAsync_TargetIsInactiveStaff_IsStillAllowed()
        {
            // OtherStaffId có Employees[OtherStaffId].IsActive == false: tài khoản khóa vẫn phân công được.
            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = OtherStaffId, ChiNhanhId = BranchId }, actorId: AdminId);

            Assert.True(result.Success);
            Assert.Equal("Đã phân công nhân viên phụ trách chi nhánh.", result.Message);
        }

        [Fact]
        public async Task AssignAsync_BranchNotAssignable_IsRejected()
        {
            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = DeletedBranchId }, actorId: AdminId);

            Assert.False(result.Success);
            Assert.Equal("Chi nhánh không tồn tại hoặc đã bị xóa.", result.Message);
            Assert.Equal(0, _assignmentStore.AddAssignmentCallCount);
        }

        // ---------- Phân công ----------

        [Fact]
        public async Task AssignAsync_NoExistingRow_CreatesNewAssignment()
        {
            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = "  Nhân sự mới  " }, actorId: AdminId);

            Assert.True(result.Success);
            Assert.Equal("Đã phân công nhân viên phụ trách chi nhánh.", result.Message);
            Assert.Equal(1, _assignmentStore.AddAssignmentCallCount);
            var created = Assert.Single(_assignmentStore.Assignments);
            Assert.True(created.IsActive);
            Assert.Equal(AdminId, created.NguoiPhanCongId);
            Assert.Equal("Nhân sự mới", created.LyDo);
            Assert.Equal(1, _uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task AssignAsync_RevokedRow_ReusesSameRow_ViaPhanCongLai()
        {
            var revoked = new NhanVienChiNhanh
            {
                NhanVienChiNhanhId = 55,
                NguoiDungId = StaffId,
                ChiNhanhId = BranchId,
                IsActive = false,
                NgayPhanCong = DateTime.UtcNow.AddMonths(-3),
                NguoiPhanCongId = AdminId,
                NgayThuHoi = DateTime.UtcNow.AddDays(-5),
                NguoiThuHoiId = AdminId,
                LyDo = "Thu hồi cũ"
            };
            _assignmentStore.Assignments.Add(revoked);

            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = "Phân công lại" }, actorId: AdminId);

            Assert.True(result.Success);
            Assert.Equal(0, _assignmentStore.AddAssignmentCallCount);
            Assert.Single(_assignmentStore.Assignments);
            var reused = _assignmentStore.Assignments[0];
            Assert.Equal(55, reused.NhanVienChiNhanhId);
            Assert.True(reused.IsActive);
            Assert.Null(reused.NgayThuHoi);
            Assert.Null(reused.NguoiThuHoiId);
            Assert.Equal("Phân công lại", reused.LyDo);
        }

        [Fact]
        public async Task AssignAsync_AlreadyActive_ReturnsOk_WithoutChangingData()
        {
            var active = new NhanVienChiNhanh { NhanVienChiNhanhId = 7, NguoiDungId = StaffId, ChiNhanhId = BranchId, IsActive = true, NguoiPhanCongId = AdminId, NgayPhanCong = DateTime.UtcNow.AddDays(-1) };
            _assignmentStore.Assignments.Add(active);

            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId }, actorId: AdminId);

            Assert.True(result.Success);
            Assert.Equal("Nhân viên đã phụ trách chi nhánh này", result.Message);
            Assert.Equal(0, _assignmentStore.AddAssignmentCallCount);
            Assert.Equal(0, _uow.SaveChangesCallCount);
            Assert.True(_uow.LastTransaction!.RolledBack);
            Assert.False(_uow.LastTransaction!.Committed);
        }

        [Fact]
        public async Task AssignAsync_ReasonTooLong_IsRejected_BeforeTransaction()
        {
            var lyDo501 = new string('x', 501);
            var result = await _service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = lyDo501 }, actorId: AdminId);

            Assert.False(result.Success);
            Assert.Equal("Lý do tối đa 500 ký tự.", result.Message);
            Assert.Equal(0, _assignmentStore.LockEmployeeCallCount);
        }

        // ---------- Thu hồi ----------

        [Fact]
        public async Task RevokeAsync_TrimsWhitespace_FromReason()
        {
            var active = new NhanVienChiNhanh { NhanVienChiNhanhId = 8, NguoiDungId = StaffId, ChiNhanhId = BranchId, IsActive = true, NguoiPhanCongId = AdminId, NgayPhanCong = DateTime.UtcNow.AddDays(-1) };
            _assignmentStore.Assignments.Add(active);

            var result = await _service.RevokeAsync(new RevokeEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = "  Chuyển chi nhánh khác  " }, actorId: AdminId);

            Assert.True(result.Success);
            Assert.Equal("Đã thu hồi phân công chi nhánh.", result.Message);
            Assert.Equal("Chuyển chi nhánh khác", active.LyDo);
            Assert.False(active.IsActive);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RevokeAsync_MissingReason_IsRejected(string? lyDo)
        {
            var result = await _service.RevokeAsync(new RevokeEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = lyDo! }, actorId: AdminId);

            Assert.False(result.Success);
            Assert.Equal("Vui lòng nhập lý do thu hồi (1–500 ký tự).", result.Message);
        }

        [Fact]
        public async Task RevokeAsync_ReasonTooLong_IsRejected()
        {
            var result = await _service.RevokeAsync(new RevokeEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = new string('y', 501) }, actorId: AdminId);

            Assert.False(result.Success);
            Assert.Equal("Vui lòng nhập lý do thu hồi (1–500 ký tự).", result.Message);
        }

        [Fact]
        public async Task RevokeAsync_NoRowOrAlreadyRevoked_ReturnsOk_WithoutChangingData()
        {
            var result = await _service.RevokeAsync(new RevokeEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = "Không còn hiệu lực" }, actorId: AdminId);

            Assert.True(result.Success);
            Assert.Equal("Nhân viên không còn phụ trách chi nhánh này", result.Message);
            Assert.Equal(0, _uow.SaveChangesCallCount);
        }

        [Fact]
        public async Task RevokeAsync_ThenCanPerformAsync_AtThatBranch_ReturnsFalse()
        {
            var active = new NhanVienChiNhanh { NhanVienChiNhanhId = 9, NguoiDungId = StaffId, ChiNhanhId = BranchId, IsActive = true, NguoiPhanCongId = AdminId, NgayPhanCong = DateTime.UtcNow.AddDays(-1) };
            _assignmentStore.Assignments.Add(active);

            var accessService = new EmployeeAccessService(_branchStore);
            var canBefore = await accessService.CanPerformAsync(StaffId, BranchId, QuanLyChoThuePhongTroWeb.Application.Common.Security.EmployeeActionCodes.MeterReview);
            Assert.True(canBefore);

            var revokeResult = await _service.RevokeAsync(new RevokeEmployeeBranchRequest { NguoiDungId = StaffId, ChiNhanhId = BranchId, LyDo = "Nghỉ việc" }, actorId: AdminId);
            Assert.True(revokeResult.Success);

            var canAfter = await accessService.CanPerformAsync(StaffId, BranchId, QuanLyChoThuePhongTroWeb.Application.Common.Security.EmployeeActionCodes.MeterReview);
            Assert.False(canAfter);
        }
    }
}
