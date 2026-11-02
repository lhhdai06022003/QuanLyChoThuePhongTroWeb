namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public sealed class PublicRoomService(IPublicRoomStore store, IRoomDepositPolicyStore policies) : IPublicRoomService
{
    public async Task<IReadOnlyList<PublicRoomDto>> ListAsync(IReadOnlyCollection<int>? allowedRoomIds,
        CancellationToken cancellationToken = default)
    {
        var allowed = allowedRoomIds is null ? null : AllowedIds(allowedRoomIds);
        if (allowed is { Count: 0 })
            return [];

        var available = await store.ListAvailableAsync(cancellationToken);
        var deposits = await policies.GetAllAsync(cancellationToken);
        return available.Where(room => allowed is null || allowed.Contains(room.PhongTroId))
            .Select(room => room with {
                DepositAmount = deposits.TryGetValue(room.PhongTroId, out var policy) && policy.IsValid
                    ? policy.Amount : null
            }).ToArray();
    }

    public async Task<PublicRoomDto?> GetAsync(int id, IReadOnlyCollection<int>? allowedRoomIds,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0 || (allowedRoomIds is not null && !AllowedIds(allowedRoomIds).Contains(id)))
            return null;

        var room = await store.GetAvailableAsync(id, cancellationToken);
        if (room is null) return null;
        var policy = await policies.GetAsync(id, cancellationToken);
        return room with { DepositAmount = policy is { IsValid: true } ? policy.Amount : null };
    }

    private static HashSet<int> AllowedIds(IReadOnlyCollection<int>? ids) =>
        ids is null ? [] : ids.Where(id => id > 0).ToHashSet();
}
