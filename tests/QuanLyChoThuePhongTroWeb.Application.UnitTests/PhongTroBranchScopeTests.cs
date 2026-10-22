using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Phòng trọ chỉ hiện và sửa được trong chi nhánh được phân công; thêm, xóa, phát sinh chỉ dành cho Admin.
    public class PhongTroBranchScopeTests
    {
        private const int Staff = 5;
        private const int BranchA = 1;
        private const int BranchB = 2;

        private sealed class FakeStore : IPhongTroStore
        {
            public Dictionary<int, PhongTro> Rooms { get; } = new();
            public List<IReadOnlyCollection<int>?> Filters { get; } = new();
            public int Updates { get; private set; }
            public int Adds { get; private set; }
            public bool QuickContractRead { get; private set; }

            public Task<IReadOnlyList<SelectOptionDto>> GetChiNhanhDropdownAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IReadOnlyList<SelectOptionDto>>(Array.Empty<SelectOptionDto>());
            }

            public Task<IReadOnlyList<PhongTroListItemDto>> GetDanhSachPhongTroAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult<IReadOnlyList<PhongTroListItemDto>>(Array.Empty<PhongTroListItemDto>());
            }

            public Task<PhongTro?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
                => Task.FromResult(Rooms.TryGetValue(id, out var p) ? p : null);

            public Task<bool> ExistsSoPhongAsync(string soPhong, int chiNhanhId, int excludeId = 0, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<bool> HasActiveHopDongAsync(int phongTroId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<int> GetActiveMembersCountForActiveContractAsync(int phongTroId, CancellationToken cancellationToken = default) => Task.FromResult(0);

            public Task<List<PhongTro>> GetDanhSachPhongTroConTrongAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult(new List<PhongTro>());
            }

            public Task<List<PhongCardRes>> GetSoDoPhongAsync(int chiNhanhId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Filters.Add(allowedBranchIds);
                return Task.FromResult(new List<PhongCardRes>());
            }

            public Task<QuickContractDto?> GetQuickContractAsync(int phongTroId, CancellationToken cancellationToken = default)
            {
                QuickContractRead = true;
                return Task.FromResult<QuickContractDto?>(new QuickContractDto());
            }

            public Task<UnpaidInvoiceDto?> GetUnpaidInvoiceAsync(int phongTroId, CancellationToken cancellationToken = default)
                => Task.FromResult<UnpaidInvoiceDto?>(new UnpaidInvoiceDto());

            public Task<List<ChiNhanh>> GetAllActiveChiNhanhsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<ChiNhanh>());
            public Task<List<(int ChiNhanhId, string SoPhong)>> GetAllRoomNumbersAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<(int, string)>());
            public void Add(PhongTro phongTro) => Adds++;
            public void Update(PhongTro phongTro) => Updates++;
        }

        private static (PhongTroService Service, FakeStore Store) Create(EmployeeAccessScope? scope)
        {
            var store = new FakeStore();
            store.Rooms[10] = new PhongTro { PhongTroId = 10, ChiNhanhId = BranchA, SoPhong = "101" };
            store.Rooms[20] = new PhongTro { PhongTroId = 20, ChiNhanhId = BranchB, SoPhong = "201" };
            var access = new FakeEmployeeAccessService { ScopeToReturn = scope };
            return (new PhongTroService(store, new RecordingUnitOfWork(), access), store);
        }

        private static EmployeeAccessScope StaffOfA() => new(Staff, false, new[] { BranchA });

        private static PhongTroReq Req(int phongTroId, int chiNhanhId) => new() { PhongTroId = phongTroId, ChiNhanhId = chiNhanhId, SoPhong = "X" };

        [Fact]
        public async Task Staff_Lists_ArePassedAssignedBranches_AdminIsUnrestricted()
        {
            var (staff, staffStore) = Create(StaffOfA());
            await staff.GetDanhSachChiNhanhDropdownAsync(Staff);
            await staff.GetDanhSachPhongTroAsync(Staff);
            await staff.DanhSachPhongTroConTrong(Staff);
            await staff.GetSoDoPhongAsync(Staff, 0);

            Assert.Equal(4, staffStore.Filters.Count);
            Assert.All(staffStore.Filters, f => Assert.Equal(new[] { BranchA }, f));

            var (admin, adminStore) = Create(new EmployeeAccessScope(1, true));
            await admin.GetDanhSachPhongTroAsync(1);
            Assert.Null(Assert.Single(adminStore.Filters));
        }

        [Fact]
        public async Task Staff_SoDoForOtherBranch_IsEmptyWithoutQuery()
        {
            var (service, store) = Create(StaffOfA());

            Assert.Empty(await service.GetSoDoPhongAsync(Staff, BranchB));
            Assert.Empty(store.Filters);
        }

        [Fact]
        public async Task Staff_GetRoomOfOtherBranch_IsNotFound()
        {
            var (service, _) = Create(StaffOfA());

            var own = await service.GetPhongTroByIdAsync(Staff, 10);
            var other = await service.GetPhongTroByIdAsync(Staff, 20);

            Assert.True(own.Success);
            Assert.False(other.Success);
            Assert.Equal(ServiceErrorKind.NotFound, other.ErrorKind);
        }

        [Fact]
        public async Task Staff_UpdateRoomOfOtherBranch_IsForbidden()
        {
            var (service, store) = Create(StaffOfA());

            var result = await service.CapNhatPhongTroAsync(Staff, Req(20, BranchB));

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal(0, store.Updates);
        }

        [Fact]
        public async Task Staff_MovingOwnRoomToOtherBranch_IsForbidden()
        {
            var (service, store) = Create(StaffOfA());

            var result = await service.CapNhatPhongTroAsync(Staff, Req(10, BranchB));

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal(BranchA, store.Rooms[10].ChiNhanhId);
            Assert.Equal(0, store.Updates);
        }

        [Fact]
        public async Task Staff_UpdateOwnRoom_Succeeds()
        {
            var (service, store) = Create(StaffOfA());

            var result = await service.CapNhatPhongTroAsync(Staff, Req(10, BranchA));

            Assert.True(result.Success, result.Message);
            Assert.Equal(1, store.Updates);
        }

        [Fact]
        public async Task Staff_CreateDeleteGenerate_AreAdminOnly()
        {
            var (service, store) = Create(StaffOfA());

            Assert.Equal(ServiceErrorKind.Forbidden, (await service.ThemPhongTroAsync(Staff, Req(0, BranchA))).ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.XoaPhongTroAsync(Staff, 10)).ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.PhatSinhNgauNhienAsync(Staff)).ErrorKind);
            Assert.Equal(0, store.Adds);
            Assert.Equal(0, store.Updates);
        }

        [Fact]
        public async Task Staff_QuickContractOfOtherBranch_IsHidden()
        {
            var (service, store) = Create(StaffOfA());

            Assert.Null(await service.GetQuickContractAsync(Staff, 20));
            Assert.Null(await service.GetUnpaidInvoiceAsync(Staff, 20));
            Assert.False(store.QuickContractRead);
            Assert.NotNull(await service.GetQuickContractAsync(Staff, 10));
        }

        [Fact]
        public async Task InvalidActor_SeesNothing()
        {
            var (service, store) = Create(null);

            Assert.Empty(await service.GetDanhSachChiNhanhDropdownAsync(0));
            Assert.Empty(await service.GetDanhSachPhongTroAsync(Staff));
            Assert.Equal(ServiceErrorKind.NotFound, (await service.GetPhongTroByIdAsync(Staff, 10)).ErrorKind);
            Assert.Equal(ServiceErrorKind.Forbidden, (await service.CapNhatPhongTroAsync(Staff, Req(10, BranchA))).ErrorKind);
            Assert.Empty(store.Filters);
        }
    }
}
