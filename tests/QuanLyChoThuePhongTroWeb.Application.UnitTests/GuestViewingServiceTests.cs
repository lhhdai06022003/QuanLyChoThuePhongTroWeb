using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public class GuestViewingServiceTests
{
    [Fact]
    public async Task CreateAsync_SelectedSlotOverridesOldCustomTime()
    {
        var when = DateTime.UtcNow.AddDays(1);
        var store = new FakeViewingStore { Slots = [new(7, when, when.AddHours(1), 1)] };
        var service = new LichXemPhongService(RoomService(), store);
        var result = await service.CreateAsync(Request(DateTimeOffset.UtcNow.AddDays(-1)) with { KhungGioXemPhongId = 7 });
        Assert.True(result.Success);
        Assert.Equal(when, store.LastCommand?.ThoiGianMongMuonUtc);
    }

    [Fact]
    public async Task CreateAsync_AllowsAnonymousVisitorWithDesiredTime()
    {
        var store = new FakeViewingStore();
        var service = new LichXemPhongService(RoomService(), store);
        var request = Request(DateTimeOffset.UtcNow.AddDays(2));

        var result = await service.CreateAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Nguyễn An", store.LastCommand?.HoTen);
        Assert.Null(store.LastCommand?.KhungGioXemPhongId);
        Assert.Null(store.LastCommand?.NguoiDungId);
    }

    [Fact]
    public async Task CreateAsync_PassesAuthenticatedUserForOwnershipCheck()
    {
        var store = new FakeViewingStore();
        var service = new LichXemPhongService(RoomService(), store);

        Assert.True((await service.CreateAsync(Request(DateTimeOffset.UtcNow.AddDays(1)), 42)).Success);
        Assert.Equal(42, store.LastCommand?.NguoiDungId);
    }

    [Fact]
    public async Task CreateAsync_RejectsPastTimeAndHiddenRoom()
    {
        var store = new FakeViewingStore();
        var service = new LichXemPhongService(RoomService(), store);

        Assert.False((await service.CreateAsync(Request(DateTimeOffset.UtcNow.AddMinutes(-1)))).Success);
        Assert.False((await service.CreateAsync(Request(DateTimeOffset.UtcNow.AddDays(1)) with { MaCongKhai = "HIDDEN" })).Success);
        Assert.Null(store.LastCommand);
    }

    [Fact]
    public async Task CreateAsync_RejectsSlotNotAvailableForRoom()
    {
        var store = new FakeViewingStore();
        var service = new LichXemPhongService(RoomService(), store);

        var result = await service.CreateAsync(Request(DateTimeOffset.UtcNow.AddDays(1)) with { KhungGioXemPhongId = 99 });

        Assert.False(result.Success);
        Assert.Null(store.LastCommand);
    }

    [Fact]
    public async Task CreateAsync_ReportsCapacityRaceFromStore()
    {
        var store = new FakeViewingStore { Accept = false };
        var service = new LichXemPhongService(RoomService(), store);

        var result = await service.CreateAsync(Request(DateTimeOffset.UtcNow.AddDays(1)));

        Assert.False(result.Success);
    }

    private static CreateViewingRequest Request(DateTimeOffset time) =>
        new("OPEN", null, time, "Nguyễn An", "0900000000", null, null);

    private static PhongCongKhaiService RoomService() => new(new FakeRoomStore());

    private sealed class FakeRoomStore : IPhongCongKhaiStore
    {
        public Task<IReadOnlyList<PublicRoomRecord>> GetCandidatesAsync() =>
            Task.FromResult<IReadOnlyList<PublicRoomRecord>>(Array.Empty<PublicRoomRecord>());

        public Task<PublicRoomRecord?> GetByCodeAsync(string code) => Task.FromResult<PublicRoomRecord?>(code == "OPEN" ? new()
        {
            PhongTroId = 1, MaCongKhai = code, DuocDangTin = true, PhongTrong = true
        } : null);
    }

    private sealed class FakeViewingStore : ILichXemPhongStore
    {
        public Task<IReadOnlyList<ViewingSlotDto>> GetRescheduleSlotsAsync(int requestId, int actorId, DateTime nowUtc) =>
            Task.FromResult<IReadOnlyList<ViewingSlotDto>>(Array.Empty<ViewingSlotDto>());
        public bool Accept { get; set; } = true;
        public IReadOnlyList<ViewingSlotDto> Slots { get; set; } = [];
        public CreateViewingCommand? LastCommand { get; private set; }

        public Task<IReadOnlyList<ViewingSlotDto>> GetOpenSlotsAsync(string code, DateTime nowUtc) =>
            Task.FromResult(Slots);

        public Task<bool> CreateAsync(CreateViewingCommand command)
        {
            LastCommand = command;
            return Task.FromResult(Accept);
        }

        public Task<IReadOnlyList<StaffViewingDto>> GetStaffQueueAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<StaffViewingDto>>(Array.Empty<StaffViewingDto>());

        public Task<IReadOnlyList<GuestViewingDto>> GetMineAsync(int actorId) =>
            Task.FromResult<IReadOnlyList<GuestViewingDto>>(Array.Empty<GuestViewingDto>());

        public Task<bool> DecideAsync(int requestId, int actorId, bool confirm) => Task.FromResult(false);
        public Task<bool> ManageConfirmedAsync(int requestId, int actorId, ManageViewingRequest request) => Task.FromResult(false);
    }
}
