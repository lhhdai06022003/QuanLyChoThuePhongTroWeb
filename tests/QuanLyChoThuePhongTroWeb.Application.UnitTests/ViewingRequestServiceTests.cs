using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class ViewingRequestServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Submit_ValidPreferredTime_RequestsStaffReview()
    {
        var store = new FakeStore();
        var service = new ViewingRequestService(store, new FixedTimeProvider(Now));
        var input = new ViewingRequestInput(7, null, Now.AddDays(1),
            "Nguyễn Văn A", "0912345678", "a@example.com", null);

        var result = await service.SubmitAsync(3, input);

        Assert.True(result.Success, result.Message);
        Assert.Equal(19, result.Data);
        Assert.Equal(3, store.ActorId);
        Assert.Equal(Now, store.NowUtc);
    }

    [Theory]
    [InlineData(0, null, 1, "a@example.com")]
    [InlineData(3, 5, 1, "a@example.com")]
    [InlineData(3, null, -1, "a@example.com")]
    [InlineData(3, null, 1, "invalid")]
    public async Task Submit_InvalidRequest_DoesNotPersist(int actorId, int? slotId,
        int preferredDays, string email)
    {
        var store = new FakeStore();
        var service = new ViewingRequestService(store, new FixedTimeProvider(Now));
        var input = new ViewingRequestInput(7, slotId, Now.AddDays(preferredDays),
            "Nguyễn Văn A", "0912345678", email, null);

        var result = await service.SubmitAsync(actorId, input);

        Assert.False(result.Success);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Submit_FullSlot_ReturnsClearConflict()
    {
        var store = new FakeStore { NextResult = new ViewingCreateResult(null, ViewingCreateError.SlotFull) };
        var service = new ViewingRequestService(store, new FixedTimeProvider(Now));
        var input = new ViewingRequestInput(7, 5, null,
            "Nguyễn Văn A", "0912345678", "a@example.com", null);

        var result = await service.SubmitAsync(3, input);

        Assert.False(result.Success);
        Assert.Contains("hết chỗ", result.Message);
    }

    private sealed class FakeStore : IViewingRequestStore
    {
        public Task<IReadOnlyList<ViewingRequestDto>> ListMineAsync(int actorId,
            DateTime nowUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ViewingRequestDto>>(Array.Empty<ViewingRequestDto>());

        public Task<ViewingCancelError> CancelAsync(int actorId, int requestId,
            DateTime nowUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(ViewingCancelError.NotFound);

        public Task<IReadOnlyList<ViewingSlotDto>> ListAvailableSlotsAsync(int roomId,
            DateTime nowUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ViewingSlotDto>>(Array.Empty<ViewingSlotDto>());

        public int ActorId { get; private set; }
        public DateTime NowUtc { get; private set; }
        public int Calls { get; private set; }
        public ViewingCreateResult NextResult { get; set; } =
            new(19, ViewingCreateError.None);
        public Task<ViewingCreateResult> CreateAsync(int actorId, ViewingRequestInput input,
            DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            Calls++;
            ActorId = actorId;
            NowUtc = nowUtc;
            return Task.FromResult(NextResult);
        }
    }
}
