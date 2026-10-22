using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class ViewingSlotAdminServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_EmployeeOutsideBranch_DoesNotWrite()
    {
        var store = new FakeStore();
        var access = new FakeEmployeeAccessService
        {
            ScopeToReturn = new EmployeeAccessScope(3, false, [99])
        };
        var service = new ViewingSlotAdminService(store, access, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(3, 7, Now.AddDays(1),
            Now.AddDays(1).AddHours(1), 2);

        Assert.False(result.Success);
        Assert.Equal(0, store.CreateCalls);
    }

    private sealed class FakeStore : IViewingSlotAdminStore
    {
        public int CreateCalls { get; private set; }
        public Task<int?> GetPublishedRoomBranchIdAsync(int roomId,
            CancellationToken cancellationToken = default) => Task.FromResult<int?>(4);
        public Task<int?> TryCreateAsync(int actorId, int roomId, DateTime startUtc,
            DateTime endUtc, int capacity, DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult<int?>(1);
        }
        public Task<IReadOnlyList<ViewingSlotAdminDto>> ListAsync(
            IReadOnlyCollection<int> roomIds, DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ViewingSlotAdminDto>>([]);
    }
}
