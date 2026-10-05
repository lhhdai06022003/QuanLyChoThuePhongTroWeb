using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Người thuê phía quản lý: danh sách lọc theo chi nhánh, sửa/xóa người thuộc chi nhánh khác bị chặn,
    // còn GetByIdAsync của cổng khách thuê thì không bị giới hạn.
    public class NguoiThueBranchScopeTests
    {
        private const int Staff = 5;

        private sealed class FakeStore : INguoiThueStore
        {
            public HashSet<int> Visible { get; } = new();
            public List<IReadOnlyCollection<int>?> Filters { get; } = new();
            public NguoiThue Tenant { get; } = new() { NguoiThueId = 9, HoVaTen = "B", CCCD = "1", SoDienThoai = "1", Email = "b@x" };

            public Task<IEnumerable<NguoiThue>> GetAllAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IEnumerable<NguoiThue>>(Array.Empty<NguoiThue>());
            }

            public Task<IEnumerable<NguoiThue>> GetAvailableAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IEnumerable<NguoiThue>>(Array.Empty<NguoiThue>());
            }

            public Task<bool> IsVisibleAsync(int nguoiThueId, IReadOnlyCollection<int> allowedBranchIds, CancellationToken cancellationToken = default)
                => Task.FromResult(Visible.Contains(nguoiThueId));

            public Task<NguoiThue?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
                => Task.FromResult<NguoiThue?>(id == Tenant.NguoiThueId ? Tenant : null);

            public Task<bool> ExistsDuplicateAsync(string cccd, string soDienThoai, string email, int excludeId = 0, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<bool> IsDaiDienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<bool> IsThanhVienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default) => Task.FromResult(false);

            public Task<IReadOnlyList<SelectOptionDto>> GetDropdownListAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IReadOnlyList<SelectOptionDto>>(Array.Empty<SelectOptionDto>());
            }

            public Task<IReadOnlyList<SelectOptionDto>> GetDropdownChuaCoPhongAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IReadOnlyList<SelectOptionDto>>(Array.Empty<SelectOptionDto>());
            }

            public Task<IReadOnlyList<SelectOptionDto>> GetDropdownCoHopDongAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IReadOnlyList<SelectOptionDto>>(Array.Empty<SelectOptionDto>());
            }

            public Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(string searchTerm, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IReadOnlyList<NguoiThueAutocompleteDto>>(Array.Empty<NguoiThueAutocompleteDto>());
            }

            public void Add(NguoiThue entity) { }
        }

        private static (NguoiThueService Service, FakeStore Store, RecordingUnitOfWork Uow) Create(EmployeeAccessScope? scope)
        {
            var store = new FakeStore();
            var uow = new RecordingUnitOfWork();
            return (new NguoiThueService(store, uow, new FakeEmployeeAccessService { ScopeToReturn = scope }), store, uow);
        }

        private static EmployeeAccessScope StaffOfA() => new(Staff, false, new[] { 1 });

        private static NguoiThueUpdateDto UpdateDto() => new() { NguoiThueId = 9, HoVaTen = "Moi", CCCD = "1", SoDienThoai = "1", Email = "b@x" };

        [Fact]
        public async Task Staff_Lists_ArePassedAssignedBranches()
        {
            var (service, store, _) = Create(StaffOfA());

            await service.GetAllAsync(Staff);
            await service.GetAvailableAsync(Staff);
            await service.DanhSachNguoiThue(Staff);
            await service.DanhSachNguoiThueChuaCoPhong(Staff);
            await service.DanhSachNguoiThueCoHopDongAsync(Staff);
            await service.SearchAutocompleteAsync(Staff, "a");

            Assert.Equal(6, store.Filters.Count);
            Assert.All(store.Filters, f => Assert.Equal(new[] { 1 }, f));
        }

        [Fact]
        public async Task Staff_InvisibleTenant_ReadIsNull_WritesAreForbidden()
        {
            var (service, store, uow) = Create(StaffOfA());

            Assert.Null(await service.GetByIdForStaffAsync(Staff, 9));
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.UpdateAsync(Staff, 9, UpdateDto())).ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.DeleteAsync(Staff, 9)).ErrorKind);
            Assert.Equal("B", store.Tenant.HoVaTen);
            Assert.False(store.Tenant.IsDeleted);
            Assert.Equal(0, uow.SaveChangesCalls);
        }

        [Fact]
        public async Task Staff_VisibleTenant_CanReadAndUpdate()
        {
            var (service, store, _) = Create(StaffOfA());
            store.Visible.Add(9);

            Assert.NotNull(await service.GetByIdForStaffAsync(Staff, 9));
            Assert.True((await service.UpdateAsync(Staff, 9, UpdateDto())).Success);
            Assert.Equal("Moi", store.Tenant.HoVaTen);
        }

        [Fact]
        public async Task TenantPortalLookup_IsNotScoped()
        {
            var (service, _, _) = Create(null);

            Assert.NotNull(await service.GetByIdAsync(9));
        }

        [Fact]
        public async Task Staff_GenerateRandomTenants_IsAdminOnly()
        {
            var (service, _, uow) = Create(StaffOfA());

            Assert.Equal(ServiceErrorKind.Forbidden, (await service.PhatSinhNgauNhienAsync(Staff)).ErrorKind);
            Assert.Equal(0, uow.SaveChangesCalls);
        }
    }
}
