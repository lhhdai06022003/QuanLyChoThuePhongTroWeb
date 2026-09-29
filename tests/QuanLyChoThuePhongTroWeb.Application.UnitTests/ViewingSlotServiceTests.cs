using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class ViewingSlotServiceTests
{
    [Fact]
    public async Task InvalidSlotNeverReachesStore()
    {
        var store = new FakeStore();
        var service = new KhungGioXemPhongService(store);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        Assert.False(await service.CreateAsync(new(1, start, start.AddHours(-1), 1), 2));
        Assert.False(await service.CreateAsync(new(1, start, start.AddHours(1), 0), 2));
        Assert.False(await service.CreateAsync(new(1, start.AddDays(-2), start, 1), 2));
        Assert.False(store.Called);
    }

    [Fact]
    public async Task ValidSlotStillRequiresStoreApproval()
    {
        var store = new FakeStore();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        Assert.False(await new KhungGioXemPhongService(store)
            .CreateAsync(new(1, start, start.AddHours(1), 2), 2));
        Assert.True(store.Called);
    }

    private sealed class FakeStore : IKhungGioXemPhongStore
    {
        public bool Called { get; private set; }
        public Task<IReadOnlyList<ManagedViewingSlotDto>> GetManagedAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<ManagedViewingSlotDto>>([]);
        public Task<bool> CreateAsync(CreateViewingSlotRequest request, int actorId)
        {
            Called = true;
            return Task.FromResult(false);
        }
        public Task<bool> CloseAsync(int id, int actorId) => Task.FromResult(false);
    }
}
