using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class ReservationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Request_WithViewingOwnedByAnotherGuest_IsRejected()
    {
        var store = new FakeStore { CreateError = ReservationError.ViewingUnavailable };
        var service = CreateService(store);

        var result = await service.RequestAsync(7, 5, 12);

        Assert.False(result.Success);
        Assert.Equal(1, store.CreateCalls);
    }

    [Fact]
    public async Task Approve_EmployeeOutsideBranch_DoesNotWrite()
    {
        var store = new FakeStore();
        var service = CreateService(store);

        var result = await service.ApproveAsync(3, 11, 500000m,
            Now.AddDays(1), Now.AddDays(2));

        Assert.False(result.Success);
        Assert.Equal(0, store.ApproveCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Approve_InvalidMoney_DoesNotWrite(decimal amount)
    {
        var store = new FakeStore();
        var service = CreateService(store);

        var result = await service.ApproveAsync(3, 11, amount,
            Now.AddDays(1), Now.AddDays(2));

        Assert.False(result.Success);
        Assert.Equal(0, store.ApproveCalls);
    }

    [Fact]
    public async Task ExtendContractDeadline_EmployeeOutsideBranch_DoesNotWrite()
    {
        var store = new FakeStore();
        var service = CreateService(store);

        var result = await service.ExtendContractDeadlineAsync(3, 11,
            Now.AddDays(3));

        Assert.False(result.Success);
        Assert.Equal(0, store.ExtendCalls);
    }

    private static ReservationService CreateService(FakeStore store) => new(store,
        new FakeEmployeeAccessService
        {
            ScopeToReturn = new EmployeeAccessScope(3, false, [99])
        }, new FixedTimeProvider(Now));

    private sealed class FakeStore : IReservationStore
    {
        public Task ExpireUnpaidAsync(DateTime nowUtc,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ReservationError CreateError { get; set; }
        public int CreateCalls { get; private set; }
        public int ApproveCalls { get; private set; }
        public int ExtendCalls { get; private set; }
        public Task<ReservationCreateResult> CreateAsync(int actorId, int roomId,
            int? viewingId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new ReservationCreateResult(null, CreateError));
        }
        public Task<IReadOnlyList<ReservationDto>> ListMineAsync(int actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReservationDto>>([]);
        public Task<IReadOnlyList<ReservationDto>> ListForStaffAsync(
            IReadOnlyCollection<int>? branchIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReservationDto>>([]);
        public Task<int?> GetBranchIdAsync(int reservationId,
            CancellationToken cancellationToken = default) => Task.FromResult<int?>(4);
        public Task<ReservationError> ApproveAsync(int actorId, int reservationId,
            decimal amount, DateTime paymentDeadlineUtc, DateTime contractDeadlineUtc,
            DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            ApproveCalls++;
            return Task.FromResult(ReservationError.None);
        }
        public Task<ReservationError> CancelAsync(int actorId, int reservationId,
            DateTime nowUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReservationError.None);
        public Task<ReservationError> ExtendContractDeadlineAsync(int actorId,
            int reservationId, DateTime newDeadlineUtc, DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            ExtendCalls++;
            return Task.FromResult(ReservationError.None);
        }
        public Task<ReservationError> CancelExpiredContractAsync(int actorId,
            int reservationId, string reason, DateTime nowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ReservationError.None);
    }
}
