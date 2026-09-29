namespace QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

public interface IPublicRoomStore
{
    Task<IReadOnlyList<PublicRoomDto>> ListAvailableAsync(CancellationToken cancellationToken = default);
    Task<PublicRoomDto?> GetAvailableAsync(int id, CancellationToken cancellationToken = default);
}
