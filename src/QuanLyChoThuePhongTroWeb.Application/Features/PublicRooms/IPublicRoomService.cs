namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public interface IPublicRoomService
{
    Task<IReadOnlyList<PublicRoomDto>> ListAsync(IReadOnlyCollection<int>? allowedRoomIds,
        CancellationToken cancellationToken = default);
    Task<PublicRoomDto?> GetAsync(int id, IReadOnlyCollection<int>? allowedRoomIds,
        CancellationToken cancellationToken = default);
}
