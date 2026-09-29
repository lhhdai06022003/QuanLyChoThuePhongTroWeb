using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public class ReservationExpiryServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ExpiresOnlyCandidatesAcceptedByStore()
    {
        var store = new FakeStore();
        var now = new DateTime(2026, 9, 25, 7, 5, 0, DateTimeKind.Utc);

        var count = await new HetHanGiuChoUseCase(store).ExecuteAsync(now);

        Assert.Equal(1, count);
        Assert.Equal(now, store.Now);
        Assert.Equal(new[] { 3, 4 }, store.Processed);
    }

    private sealed class FakeStore : IHetHanGiuChoStore
    {
        public DateTime Now { get; private set; }
        public List<int> Processed { get; } = new();
        public Task<IReadOnlyList<int>> GetCandidatesAsync(DateTime nowUtc)
        {
            Now = nowUtc;
            return Task.FromResult<IReadOnlyList<int>>(new[] { 3, 4 });
        }
        public Task<bool> TryExpireAsync(int id, DateTime nowUtc)
        {
            Processed.Add(id);
            return Task.FromResult(id == 3);
        }
    }
}
