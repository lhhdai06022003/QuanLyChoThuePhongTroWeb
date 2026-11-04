using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class RoomListingDepositTests
{
    [Theory]
    [InlineData(-1d)]
    [InlineData(0.5)]
    [InlineData(1000000000001d)]
    public async Task Save_RejectsInvalidDepositBeforeSaving(double amount)
    {
        var (service, store) = Create();

        var result = await service.SaveAsync(7, Request(false, (decimal)amount));

        Assert.False(result.Success);
        Assert.Null(store.Saved);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(null)]
    public async Task Publish_RequiresDepositAboveZero(double? amount)
    {
        // Phòng đang lưu tiền cọc 0, nên bỏ trống ô cọc cũng không được đăng công khai.
        var (service, store) = Create(currentDeposit: 0);

        var result = await service.SaveAsync(7, Request(true, amount is { } value ? (decimal)value : null));

        Assert.False(result.Success);
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task Publish_SavesDepositWithListing()
    {
        var (service, store) = Create();

        var result = await service.SaveAsync(7, Request(true, 850000));

        Assert.True(result.Success);
        Assert.Equal("Tin phòng", store.Saved!.Title);
        Assert.True(store.Saved.Publish);
        Assert.Equal(850000m, store.Saved.DepositAmount);
    }

    [Fact]
    public async Task Publish_WithoutDepositField_KeepsCurrentPositiveDeposit()
    {
        var (service, store) = Create(currentDeposit: 500000);

        var result = await service.SaveAsync(7, Request(true, null));

        Assert.True(result.Success);
        Assert.Null(store.Saved!.DepositAmount);
    }

    [Fact]
    public async Task HiddenListing_CanSaveZeroDeposit()
    {
        var (service, store) = Create();

        var result = await service.SaveAsync(7, Request(false, 0));

        Assert.True(result.Success);
        Assert.False(store.Saved!.Publish);
        Assert.Equal(0m, store.Saved.DepositAmount);
    }

    [Fact]
    public async Task OutsideBranch_CannotChangeListingOrDeposit()
    {
        var (service, store) = Create(branchId: 99);

        var result = await service.SaveAsync(7, Request(true, 850000));

        Assert.False(result.Success);
        Assert.Null(store.Saved);
    }

    private static SaveRoomListing Request(bool publish, decimal? amount) =>
        new(10, " Tin phòng ", " Mô tả ", publish, [], amount);

    private static (RoomListingService Service, Listings Store) Create(int branchId = 3, decimal currentDeposit = 500000)
    {
        var store = new Listings(branchId, currentDeposit);
        var access = new FakeEmployeeAccessService { ScopeToReturn = new EmployeeAccessScope(7, false, [3]) };
        return (new RoomListingService(store, access, new NoStorage(),
            NullLogger<RoomListingService>.Instance), store);
    }

    private sealed class Listings(int branchId, decimal deposit) : IRoomListingStore
    {
        private readonly RoomListingDto room = new(10, branchId, "103", "Chi nhánh", 2500000,
            20, 0, true, true, "Tin cũ", "", 0, null, false, deposit);
        public SaveRoomListing? Saved { get; private set; }
        public Task<IReadOnlyList<RoomListingDto>> ListAsync(IReadOnlyCollection<int>? branchIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RoomListingDto>>([room]);
        public Task<RoomListingDto?> GetAsync(int roomId, CancellationToken ct) => Task.FromResult<RoomListingDto?>(room);
        public Task<bool> SaveAsync(int actorId, int branch, SaveRoomListing request,
            IReadOnlyList<ListingPhoto> photos, CancellationToken ct)
        {
            Saved = request;
            return Task.FromResult(true);
        }
    }

    private sealed class NoStorage : IMeterImageStorageService
    {
        public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No upload expected.");
        public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No delete expected.");
    }
}
