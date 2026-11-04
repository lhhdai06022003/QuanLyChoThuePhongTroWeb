namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public sealed class PublicRoomService(IPublicRoomStore store) : IPublicRoomService
{
    public async Task<IReadOnlyList<PublicRoomDto>> ListAsync(IReadOnlyCollection<int>? allowedRoomIds,
        CancellationToken cancellationToken = default)
    {
        var allowed = allowedRoomIds is null ? null : AllowedIds(allowedRoomIds);
        if (allowed is { Count: 0 })
            return [];

        var available = await store.ListAvailableAsync(cancellationToken);
        return allowed is null ? available : available.Where(room => allowed.Contains(room.PhongTroId)).ToArray();
    }

    public Task<PublicRoomDto?> GetAsync(int id, IReadOnlyCollection<int>? allowedRoomIds,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0 || (allowedRoomIds is not null && !AllowedIds(allowedRoomIds).Contains(id)))
            return Task.FromResult<PublicRoomDto?>(null);

        return store.GetAvailableAsync(id, cancellationToken);
    }

    private static HashSet<int> AllowedIds(IReadOnlyCollection<int>? ids) =>
        ids is null ? [] : ids.Where(id => id > 0).ToHashSet();
}
