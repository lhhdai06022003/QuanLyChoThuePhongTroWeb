using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Sự cố phía quản lý: danh sách và xem chi tiết chỉ trong chi nhánh được phân công, actor không hợp lệ không thấy gì.
    public class YeuCauSuCoBranchScopeTests
    {
        private const int Staff = 5;
        private const int BranchA = 1;
        private const int BranchB = 2;

        private sealed class FakeStore : IYeuCauSuCoStore
        {
            public List<(int? ChiNhanhId, IReadOnlyCollection<int>? Allowed)> Calls { get; } = new();
            public Dictionary<int, YeuCauSuCo> Incidents { get; } = new();

            public Task<List<YeuCauSuCo>> GetAllAsync(int? chiNhanhId, TrangThaiSuCo? trangThai, int? soThang = 6, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default)
            {
                Calls.Add((chiNhanhId, allowedBranchIds));
                return Task.FromResult(Incidents.Values.ToList());
            }

            public Task<List<YeuCauSuCo>> GetByNguoiThueAsync(int nguoiThueId, CancellationToken cancellationToken = default)
                => Task.FromResult(Incidents.Values.Where(x => x.NguoiThueId == nguoiThueId).ToList());

            public Task<YeuCauSuCo?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
                => Task.FromResult(Incidents.TryGetValue(id, out var x) ? x : null);

            public void Add(YeuCauSuCo model) => Incidents[model.Id] = model;
            public void Update(YeuCauSuCo model) => Incidents[model.Id] = model;
        }

        private static (YeuCauSuCoService Service, FakeStore Store) Create(EmployeeAccessScope? scope)
        {
            var store = new FakeStore();
            store.Add(Incident(10, BranchA));
            store.Add(Incident(20, BranchB));
            var service = new YeuCauSuCoService(
                store,
                new RecordingUnitOfWork(),
                new FakeEmployeeAccessService { ScopeToReturn = scope },
                new NotUsedInvoiceIssuanceStore(),
                NullLogger<YeuCauSuCoService>.Instance);
            return (service, store);
        }

        private static YeuCauSuCo Incident(int id, int branchId) => new()
        {
            Id = id,
            PhongTroId = id * 10,
            NguoiThueId = id * 100,
            TieuDe = $"Su co {id}",
            TrangThai = TrangThaiSuCo.ChoTiepNhan,
            NgayGui = DateTime.UtcNow,
            PhongTro = new PhongTro { PhongTroId = id * 10, ChiNhanhId = branchId, SoPhong = $"P{id}" }
        };

        private static EmployeeAccessScope StaffOfA() => new(Staff, false, new[] { BranchA });

        [Fact]
        public async Task GetAll_Staff_PassesAssignedBranchesToStore()
        {
            var (service, store) = Create(StaffOfA());

            await service.GetAllAsync(Staff, null, null, 6);
            await service.GetAllAsync(Staff, BranchA, AppTrangThaiSuCo.DangXuLy, 6);

            Assert.Equal(2, store.Calls.Count);
            Assert.All(store.Calls, c => Assert.Equal(new[] { BranchA }, c.Allowed));
            Assert.Equal(BranchA, store.Calls[1].ChiNhanhId);
        }

        [Fact]
        public async Task GetAll_Admin_IsUnrestricted()
        {
            var (service, store) = Create(new EmployeeAccessScope(1, true));

            var list = await service.GetAllAsync(1, null, null, 6);

            Assert.Null(Assert.Single(store.Calls).Allowed);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task GetAll_StaffFiltersOtherBranch_ReturnsEmptyWithoutQuery()
        {
            var (service, store) = Create(StaffOfA());

            var list = await service.GetAllAsync(Staff, BranchB, null, 6);

            Assert.Empty(list);
            Assert.Empty(store.Calls);
        }

        [Fact]
        public async Task GetAll_InvalidActor_FailsClosed()
        {
            var (service, store) = Create(null);

            Assert.Empty(await service.GetAllAsync(0, null, null, 6));
            Assert.Empty(store.Calls);
        }

        [Fact]
        public async Task GetAll_StaffWithoutAssignment_PassesEmptyBranchList()
        {
            var (service, store) = Create(new EmployeeAccessScope(Staff, false));

            await service.GetAllAsync(Staff, null, null, 6);

            Assert.Empty(Assert.Single(store.Calls).Allowed!);
        }

        [Fact]
        public async Task GetById_OnlyReturnsIncidentInScope()
        {
            var (staffService, _) = Create(StaffOfA());
            Assert.NotNull(await staffService.GetByIdAsync(Staff, 10));
            Assert.Null(await staffService.GetByIdAsync(Staff, 20));

            var (anonymousService, _) = Create(null);
            Assert.Null(await anonymousService.GetByIdAsync(0, 10));

            var (adminService, _) = Create(new EmployeeAccessScope(1, true));
            Assert.NotNull(await adminService.GetByIdAsync(1, 20));
        }

        // Các truy vấn danh sách/chi tiết không đụng tới hóa đơn.
        private sealed class NotUsedInvoiceIssuanceStore : IInvoiceIssuanceStore
        {
            private static Exception NotUsed() => new InvalidOperationException("Không được gọi trong test này.");
            public Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default) => throw NotUsed();
            public Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default) => throw NotUsed();
            public Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default) => throw NotUsed();
            public Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default) => throw NotUsed();
            public Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default) => throw NotUsed();
            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime nextMonthStartUtc, CancellationToken ct = default) => throw NotUsed();
            public Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default) => throw NotUsed();
            public Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default) => throw NotUsed();
            public Task LockMeterPeriodsForRoomsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken ct = default) => throw NotUsed();
            public Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default) => throw NotUsed();
            public Task<IReadOnlyList<int>> GetContractIdsForTenantRoomAsync(int phongTroId, int nguoiThueId, CancellationToken ct = default) => throw NotUsed();
        }
    }
}
