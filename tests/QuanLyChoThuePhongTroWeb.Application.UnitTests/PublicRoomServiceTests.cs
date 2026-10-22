using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class PublicRoomServiceTests
{
    [Fact]
    public async Task ListAsync_OnlyReturnsConfiguredRooms()
    {
        var store = new FakeStore();
        var service = new PublicRoomService(store);

        var rooms = await service.ListAsync([10]);

        Assert.Single(rooms);
        Assert.Equal(10, rooms[0].PhongTroId);
    }

    [Fact]
    public async Task GetAsync_DoesNotReadAnUnconfiguredRoom()
    {
        var store = new FakeStore();
        var service = new PublicRoomService(store);

        var room = await service.GetAsync(28, [10]);

        Assert.Null(room);
        Assert.Equal(0, store.GetCalls);
    }

    [Fact]
    public async Task EmptyConfiguration_HidesAllRooms()
    {
        var store = new FakeStore();
        var service = new PublicRoomService(store);

        Assert.Empty(await service.ListAsync([]));
        Assert.Null(await service.GetAsync(10, []));
        Assert.Equal(0, store.ListCalls);
        Assert.Equal(0, store.GetCalls);
    }

    [Fact]
    public async Task MissingLegacyAllowlist_UsesPublishedRoomsFromStore()
    {
        var store = new FakeStore();
        var service = new PublicRoomService(store);

        var rooms = await service.ListAsync(null);

        Assert.Equal(2, rooms.Count);
        Assert.Equal(1, store.ListCalls);
    }

    private sealed class FakeStore : IPublicRoomStore
    {
        private readonly PublicRoomDto[] rooms =
        [
            new(10, "103", "Chi nhánh A", "Địa chỉ A", 2500000, 20, 2, 1, "Phòng 103"),
            new(28, "919", "Chi nhánh B", "Địa chỉ B", 3000000, 25, 3, 9, "Phòng 919")
        ];

        public int ListCalls { get; private set; }
        public int GetCalls { get; private set; }

        public Task<IReadOnlyList<PublicRoomDto>> ListAvailableAsync(CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult<IReadOnlyList<PublicRoomDto>>(rooms);
        }

        public Task<PublicRoomDto?> GetAvailableAsync(int id, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult<PublicRoomDto?>(rooms.FirstOrDefault(room => room.PhongTroId == id));
        }
    }
}
