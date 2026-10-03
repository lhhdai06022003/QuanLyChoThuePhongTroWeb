using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class EmployeeAccessTests
    {
        private class FakeEmployeeBranchStore : IEmployeeBranchStore
        {
            public Dictionary<int, EmployeeBranchActorDto> Actors { get; } = new();

            public Task<EmployeeBranchActorDto?> GetActorWithActiveBranchesAsync(int actorId, CancellationToken cancellationToken = default)
            {
                Actors.TryGetValue(actorId, out var actor);
                return Task.FromResult(actor);
            }

            public Task<bool> HasActiveBranchAssignmentAsync(int actorId, int chiNhanhId, CancellationToken cancellationToken = default)
            {
                if (Actors.TryGetValue(actorId, out var actor) && actor.IsActive)
                {
                    return Task.FromResult(actor.ActiveBranchIds != null && actor.ActiveBranchIds.Contains(chiNhanhId));
                }
                return Task.FromResult(false);
            }
        }

        private readonly FakeEmployeeBranchStore _store;
        private readonly EmployeeAccessService _service;

        public EmployeeAccessTests()
        {
            _store = new FakeEmployeeBranchStore();
            _service = new EmployeeAccessService(_store);
        }

        [Theory]
        [InlineData(EmployeeActionCodes.MeterUpload)]
        [InlineData(EmployeeActionCodes.MeterReview)]
        [InlineData(EmployeeActionCodes.InvoiceDraft)]
        [InlineData(EmployeeActionCodes.InvoiceSubmit)]
        [InlineData(EmployeeActionCodes.InvoiceSend)]
        [InlineData(EmployeeActionCodes.InvoiceCancel)]
        [InlineData(EmployeeActionCodes.InvoiceFinalize)]
        [InlineData(EmployeeActionCodes.InvoiceReject)]
        [InlineData(EmployeeActionCodes.PaymentReview)]
        [InlineData(EmployeeActionCodes.PaymentConfigurePartial)]
        public async Task Admin_CanPerform_AllActions_AtAnyBranch(string action)
        {
            _store.Actors[1] = new EmployeeBranchActorDto
            {
                NguoiDungId = 1,
                Role = Role.Admin,
                IsActive = true,
                ActiveBranchIds = new List<int>() // Admin doesn't need branch assignment
            };

            var canBranch1 = await _service.CanPerformAsync(actorId: 1, chiNhanhId: 1, action: action);
            var canBranch2 = await _service.CanPerformAsync(actorId: 1, chiNhanhId: 2, action: action);
            var canBranch99 = await _service.CanPerformAsync(actorId: 1, chiNhanhId: 99, action: action);

            Assert.True(canBranch1);
            Assert.True(canBranch2);
            Assert.True(canBranch99);
        }

        [Theory]
        [InlineData("Invoice.Finalise")] // Misspelled
        [InlineData("Invoice.Delete")]   // Unknown
        [InlineData("Meter.FakeAction")] // Unknown
        [InlineData("random_string")]   // Unknown
        public async Task AdminAndStaff_CannotPerform_UnknownOrMisspelledActions(string invalidAction)
        {
            _store.Actors[1] = new EmployeeBranchActorDto
            {
                NguoiDungId = 1,
                Role = Role.Admin,
                IsActive = true,
                ActiveBranchIds = new List<int>()
            };

            _store.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            var adminResult = await _service.CanPerformAsync(actorId: 1, chiNhanhId: 1, action: invalidAction);
            var staffResult = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: invalidAction);

            Assert.False(adminResult);
            Assert.False(staffResult);
        }

        [Fact]
        public async Task Action_Comparison_IsCaseInsensitive()
        {
            _store.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            var lowerCaseResult = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: "meter.upload");
            var upperCaseResult = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: "METER.UPLOAD");

            Assert.True(lowerCaseResult);
            Assert.True(upperCaseResult);
        }

        [Fact]
        public async Task Staff_CanPerform_StaffActions_AtAssignedBranch()
        {
            _store.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            var canUpload = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: EmployeeActionCodes.MeterUpload);
            var canReview = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: EmployeeActionCodes.MeterReview);
            var canDraft = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: EmployeeActionCodes.InvoiceDraft);
            var canSubmit = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: EmployeeActionCodes.InvoiceSubmit);
            var canSend = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: EmployeeActionCodes.InvoiceSend);
            var canCancel = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: EmployeeActionCodes.InvoiceCancel);

            Assert.True(canUpload);
            Assert.True(canReview);
            Assert.True(canDraft);
            Assert.True(canSubmit);
            Assert.True(canSend);
            Assert.True(canCancel);
        }

        [Theory]
        [InlineData(EmployeeActionCodes.InvoiceFinalize)]
        [InlineData(EmployeeActionCodes.InvoiceReject)]
        [InlineData(EmployeeActionCodes.PaymentConfigurePartial)]
        public async Task Staff_CannotPerform_AdminOnlyActions_EvenAtAssignedBranch(string adminAction)
        {
            _store.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            var result = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 1, action: adminAction);

            Assert.False(result);
        }

        [Fact]
        public async Task Staff_CannotPerform_AtOtherBranch_Or_UnassignedBranch()
        {
            _store.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 } // only branch 1
            };

            var canBranch2 = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 2, action: EmployeeActionCodes.MeterReview);
            var canBranch3 = await _service.CanPerformAsync(actorId: 10, chiNhanhId: 3, action: EmployeeActionCodes.InvoiceDraft);

            Assert.False(canBranch2);
            Assert.False(canBranch3);
        }

        [Fact]
        public async Task Staff_WithRevokedOrNoBranchAssignment_IsDenied()
        {
            _store.Actors[15] = new EmployeeBranchActorDto
            {
                NguoiDungId = 15,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int>() // no active assignments
            };

            var result = await _service.CanPerformAsync(actorId: 15, chiNhanhId: 1, action: EmployeeActionCodes.MeterReview);

            Assert.False(result);
        }

        [Theory]
        [InlineData(Role.KhachThue)]
        [InlineData(Role.KhachVangLai)]
        public async Task NonEmployeeRoles_AreDenied_AllEmployeeActions(Role role)
        {
            _store.Actors[20] = new EmployeeBranchActorDto
            {
                NguoiDungId = 20,
                Role = role,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            var result = await _service.CanPerformAsync(actorId: 20, chiNhanhId: 1, action: EmployeeActionCodes.MeterReview);

            Assert.False(result);
        }

        [Fact]
        public async Task InactiveUser_IsDenied_EvenIfAdmin()
        {
            _store.Actors[99] = new EmployeeBranchActorDto
            {
                NguoiDungId = 99,
                Role = Role.Admin,
                IsActive = false, // Inactive
                ActiveBranchIds = new List<int>()
            };

            var result = await _service.CanPerformAsync(actorId: 99, chiNhanhId: 1, action: EmployeeActionCodes.InvoiceDraft);

            Assert.False(result);
        }

        [Fact]
        public async Task NonExistentUser_IsDenied()
        {
            var result = await _service.CanPerformAsync(actorId: 999, chiNhanhId: 1, action: EmployeeActionCodes.InvoiceDraft);

            Assert.False(result);
        }

        [Theory]
        [InlineData(0, 1, EmployeeActionCodes.MeterReview)]
        [InlineData(-1, 1, EmployeeActionCodes.MeterReview)]
        [InlineData(10, 0, EmployeeActionCodes.MeterReview)]
        [InlineData(10, -1, EmployeeActionCodes.MeterReview)]
        [InlineData(10, 1, "")]
        [InlineData(10, 1, "   ")]
        [InlineData(10, 1, null)]
        public async Task InvalidInputs_AreDenied(int actorId, int chiNhanhId, string? action)
        {
            var result = await _service.CanPerformAsync(actorId, chiNhanhId, action!);
            Assert.False(result);
        }

        [Fact]
        public void ActionCollections_CannotBeCastedToHashSet()
        {
            Assert.IsNotType<HashSet<string>>(EmployeeActionCodes.AllValidActions);
            Assert.IsNotType<HashSet<string>>(EmployeeActionCodes.AdminOnlyActions);
        }

        [Theory]
        [InlineData("Invoice.Read")]
        [InlineData("Meter.Read")]
        [InlineData("Payment.Review")]
        public void ReadActions_AreRecognizedAsValid(string action)
        {
            Assert.True(EmployeeActionCodes.IsValid(action));
            Assert.False(EmployeeActionCodes.IsAdminOnly(action));
        }

        [Fact]
        public async Task GetScopeAsync_Admin_ReturnsScopeWithIsAdminTrue()
        {
            _store.Actors[1] = new EmployeeBranchActorDto
            {
                NguoiDungId = 1,
                Role = Role.Admin,
                IsActive = true,
                ActiveBranchIds = new List<int>()
            };

            var scope = await _service.GetScopeAsync(1);

            Assert.NotNull(scope);
            Assert.True(scope.IsAdmin);
        }

        [Fact]
        public async Task GetScopeAsync_Staff_ReturnsOnlyActiveBranches()
        {
            _store.Actors[10] = new EmployeeBranchActorDto
            {
                NguoiDungId = 10,
                Role = Role.NhanVien,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1, 3 }
            };

            var scope = await _service.GetScopeAsync(10);

            Assert.NotNull(scope);
            Assert.False(scope.IsAdmin);
            Assert.Equal(2, scope.ActiveBranchIds.Count);
            Assert.Contains(1, scope.ActiveBranchIds);
            Assert.Contains(3, scope.ActiveBranchIds);
            Assert.DoesNotContain(2, scope.ActiveBranchIds);
        }

        [Theory]
        [InlineData(999)] // Non existent
        [InlineData(0)]   // Invalid ID
        [InlineData(-1)]  // Negative ID
        public async Task GetScopeAsync_InvalidOrMissingUser_ReturnsNull(int actorId)
        {
            var scope = await _service.GetScopeAsync(actorId);
            Assert.Null(scope);
        }

        [Fact]
        public async Task GetScopeAsync_InactiveOrNonEmployee_ReturnsNull()
        {
            _store.Actors[20] = new EmployeeBranchActorDto
            {
                NguoiDungId = 20,
                Role = Role.NhanVien,
                IsActive = false,
                ActiveBranchIds = new List<int> { 1 }
            };
            _store.Actors[30] = new EmployeeBranchActorDto
            {
                NguoiDungId = 30,
                Role = Role.KhachThue,
                IsActive = true,
                ActiveBranchIds = new List<int> { 1 }
            };

            var inactiveScope = await _service.GetScopeAsync(20);
            var nonEmployeeScope = await _service.GetScopeAsync(30);

            Assert.Null(inactiveScope);
            Assert.Null(nonEmployeeScope);
        }
    }
}
