using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public class GuestReservationServiceTests
{
    [Fact]
    public async Task CreateAsync_AcceptsDirectRequestForPublicRoom()
    {
        var store = new FakeHoldStore();
        var service = new GiuChoService(new PhongCongKhaiService(new FakeRooms()), store);

        var result = await service.CreateAsync(new CreateReservationRequest("OPEN", null), 7);

        Assert.True(result.Success);
        Assert.Equal(23, result.Id);
        Assert.Equal(7, store.ActorId);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnonymousHiddenRoomAndForeignViewing()
    {
        var store = new FakeHoldStore();
        var service = new GiuChoService(new PhongCongKhaiService(new FakeRooms()), store);

        Assert.False((await service.CreateAsync(new("OPEN", null), 0)).Success);
        Assert.False((await service.CreateAsync(new("HIDDEN", null), 7)).Success);
        store.CreatedId = null;
        Assert.False((await service.CreateAsync(new("OPEN", 123), 7)).Success);
    }

    [Fact]
    public async Task ApproveAsync_RejectsInvalidDeadlines()
    {
        var store = new FakeHoldStore();
        var service = new GiuChoService(new PhongCongKhaiService(new FakeRooms()), store);
        var tomorrow = DateTimeOffset.UtcNow.AddDays(1);

        Assert.False((await service.ApproveAsync(1, new(DateTimeOffset.UtcNow.AddMinutes(-1), tomorrow), 9)).Success);
        Assert.False((await service.ApproveAsync(1, new(tomorrow.AddDays(2), tomorrow), 9)).Success);
        Assert.Null(store.ApprovedId);
    }

    [Fact]
    public async Task CloseUnpaidRequiresReasonBeforeStore()
    {
        var store = new FakeHoldStore();
        var service = new GiuChoService(new PhongCongKhaiService(new FakeRooms()), store);
        Assert.False((await service.CloseUnpaidAsync(1, 3, " ")).Success);
        Assert.False(store.Closed);
        Assert.True((await service.CloseUnpaidAsync(1, 3, "Khách đổi ý trước thanh toán")).Success);
        Assert.True(store.Closed);
    }

    private sealed class FakeRooms : IPhongCongKhaiStore
    {
        public Task<IReadOnlyList<PublicRoomRecord>> GetCandidatesAsync() =>
            Task.FromResult<IReadOnlyList<PublicRoomRecord>>(Array.Empty<PublicRoomRecord>());
        public Task<PublicRoomRecord?> GetByCodeAsync(string code) => Task.FromResult<PublicRoomRecord?>(code == "OPEN" ? new()
        {
            PhongTroId = 1, MaCongKhai = code, DuocDangTin = true, PhongTrong = true
        } : null);
    }

    private sealed class FakeHoldStore : IGiuChoStore
    {
        public int? CreatedId { get; set; } = 23;
        public int ActorId { get; private set; }
        public int? ApprovedId { get; private set; }
        public bool Closed { get; private set; }
        public Task<bool> TryCloseUnpaidAsync(int id, int actorId, string reason)
        {
            Closed = true;
            return Task.FromResult(true);
        }
        public Task<int?> TryCreateAsync(string code, int? viewingId, int actorId)
        {
            ActorId = actorId;
            return Task.FromResult(CreatedId);
        }
        public Task<bool> TryApproveAsync(int id, DateTime deadlineUtc, DateTime contractDeadlineUtc, int actorId)
        {
            ApprovedId = id;
            return Task.FromResult(true);
        }
        public Task<IReadOnlyList<ReservationDto>> GetMineAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<ReservationDto>>(Array.Empty<ReservationDto>());
        public Task<IReadOnlyList<ReservationDto>> GetQueueAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<ReservationDto>>(Array.Empty<ReservationDto>());
    }
}
