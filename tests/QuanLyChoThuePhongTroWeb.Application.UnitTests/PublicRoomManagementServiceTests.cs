using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public class PublicRoomManagementServiceTests
{
    [Fact]
    public async Task UpdateAsync_RejectsBlankPublicTitle()
    {
        var store = new FakeStore();
        var result = await new TinPhongService(store).UpdateAsync(1, 7, true, "  ");
        Assert.False(result.Success);
        Assert.False(store.Called);
    }

    [Fact]
    public async Task UpdateAsync_PassesActorToStoreForBranchCheck()
    {
        var store = new FakeStore();
        var result = await new TinPhongService(store).UpdateAsync(1, 7, true, "Phòng thoáng");
        Assert.True(result.Success);
        Assert.Equal(7, store.ActorId);
    }

    private sealed class FakeStore : ITinPhongStore
    {
        public bool Called { get; private set; }
        public int ActorId { get; private set; }
        public Task<IReadOnlyList<ManagedRoomDto>> GetManageableAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<ManagedRoomDto>>(Array.Empty<ManagedRoomDto>());
        public Task<bool> UpdateAsync(int roomId, int actorId, bool published, string? title)
        {
            Called = true;
            ActorId = actorId;
            return Task.FromResult(true);
        }
        public Task<bool> CanManageAsync(int roomId, int actorId) => Task.FromResult(true);
        public Task<IReadOnlyList<ManagedRoomImageDto>> GetImagesAsync(int roomId, int actorId) =>
            Task.FromResult<IReadOnlyList<ManagedRoomImageDto>>(Array.Empty<ManagedRoomImageDto>());
        public Task<bool> AddImageAsync(int roomId, int actorId, StoredRoomPhoto photo, string? caption) =>
            Task.FromResult(true);
        public Task<bool> SetCoverAsync(int roomId, int imageId, int actorId) => Task.FromResult(true);
        public Task<bool> HideImageAsync(int roomId, int imageId, int actorId) => Task.FromResult(true);
    }
}
