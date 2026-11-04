using System.Collections.Concurrent;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

public sealed class FakeRoomDepositPolicyStore : IRoomDepositPolicyStore
{
    private readonly ConcurrentDictionary<int, RoomDepositPolicy> policies = new();
    public Task<RoomDepositPolicy?> GetAsync(int roomId, CancellationToken ct = default) =>
        Task.FromResult(policies.TryGetValue(roomId, out var policy) ? policy : null);
    public Task<IReadOnlyDictionary<int, RoomDepositPolicy>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, RoomDepositPolicy>>(policies.ToDictionary(p => p.Key, p => p.Value));
    public Task SetAsync(int roomId, RoomDepositPolicy policy, CancellationToken ct = default)
    {
        if (roomId <= 0 || !policy.IsValid) throw new ArgumentException("Invalid deposit policy");
        policies[roomId] = policy;
        return Task.CompletedTask;
    }
    public async Task<bool> UpdateListingAsync(int roomId, RoomDepositPolicy policy,
        Func<CancellationToken, Task<bool>> saveListing, CancellationToken ct = default)
    {
        if (roomId <= 0 || !policy.IsValid) throw new ArgumentException("Invalid deposit policy");
        if (!await saveListing(ct)) return false;
        await SetAsync(roomId, policy, ct);
        return true;
    }
    public void Remove(int roomId) => policies.TryRemove(roomId, out _);
}
