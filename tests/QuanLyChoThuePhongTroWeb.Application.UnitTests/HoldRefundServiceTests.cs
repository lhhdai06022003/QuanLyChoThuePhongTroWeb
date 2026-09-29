using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class HoldRefundServiceTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(1.5)]
    public async Task DecisionRejectsInvalidAmountBeforeStore(decimal amount)
    {
        var store = new FakeStore();
        var result = await new HoanTienGiuChoService(store).DecideAsync(1,
            new RefundDecisionRequest(amount, "Khách hủy"), 3);
        Assert.False(result.Success);
        Assert.False(store.DecisionCalled);
    }

    [Fact]
    public async Task ZeroRefundDecisionIsAllowed()
    {
        var store = new FakeStore();
        var result = await new HoanTienGiuChoService(store).DecideAsync(1,
            new RefundDecisionRequest(0, "Không hoàn theo thỏa thuận"), 3);
        Assert.True(result.Success);
        Assert.True(store.DecisionCalled);
    }

    [Fact]
    public async Task RefundPaymentRequiresPositiveWholeDongAndBankReference()
    {
        var store = new FakeStore();
        var service = new HoanTienGiuChoService(store);
        Assert.False((await service.RecordAsync(1,
            new RecordRefundRequest("", 100, DateTimeOffset.UtcNow, null), 3)).Success);
        Assert.False((await service.RecordAsync(1,
            new RecordRefundRequest("REF1", 0, DateTimeOffset.UtcNow, null), 3)).Success);
        Assert.False((await service.RecordAsync(1,
            new RecordRefundRequest("REF1", 1.25m, DateTimeOffset.UtcNow, null), 3)).Success);
        Assert.False(store.RecordCalled);
        Assert.True((await service.RecordAsync(1,
            new RecordRefundRequest("REF1", 100, DateTimeOffset.UtcNow, null), 3)).Success);
    }

    private sealed class FakeStore : IHoanTienGiuChoStore
    {
        public bool DecisionCalled { get; private set; }
        public bool RecordCalled { get; private set; }
        public Task<IReadOnlyList<RefundCandidateDto>> GetQueueAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<RefundCandidateDto>>([]);
        public Task<IReadOnlyList<RefundCandidateDto>> GetMineAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<RefundCandidateDto>>([]);
        public Task<bool> DecideAsync(int reservationId, RefundDecisionRequest request, int actorId)
        {
            DecisionCalled = true;
            return Task.FromResult(true);
        }
        public Task<bool> RecordAsync(int decisionId, RecordRefundRequest request, int actorId)
        {
            RecordCalled = true;
            return Task.FromResult(true);
        }
    }
}
