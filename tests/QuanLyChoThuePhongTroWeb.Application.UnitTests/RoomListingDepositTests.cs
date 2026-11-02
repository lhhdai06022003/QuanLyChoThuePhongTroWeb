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
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(0.5)]
    [InlineData(1000000000001d)]
    public async Task Publish_RejectsMissingOrInvalidDepositBeforeSaving(double? amount)
    {
        var (service, store, _) = Create();

        var result = await service.SaveAsync(7, Request(true, amount is { } value ? (decimal)value : null));

        Assert.False(result.Success);
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task Publish_SavesMetadataAndCanonicalDepositTogether()
    {
        var (service, store, policies) = Create();

        var result = await service.SaveAsync(7, Request(true, 850000));

        Assert.True(result.Success);
        Assert.Equal("Tin phòng", store.Saved!.Title);
        Assert.True(store.Saved.Publish);
        Assert.Equal(850000m, (await policies.GetAsync(10))!.Amount);
    }

    [Fact]
    public async Task FailedMetadataSave_RetainsPreviousPublicDeposit()
    {
        var (service, store, policies) = Create();
        store.SaveResult = false;

        var result = await service.SaveAsync(7, Request(true, 850000));

        Assert.False(result.Success);
        Assert.Equal(500000m, (await policies.GetAsync(10))!.Amount);
    }

    [Fact]
    public async Task DraftWithoutDeposit_CanSaveAndRetainsPreviousQuote()
    {
        var (service, store, policies) = Create();

        var result = await service.SaveAsync(7, Request(false, null));

        Assert.True(result.Success);
        Assert.False(store.Saved!.Publish);
        Assert.Equal(500000m, (await policies.GetAsync(10))!.Amount);
    }

    [Fact]
    public async Task List_EnrichesListingsWithCanonicalDeposit()
    {
        var (service, _, _) = Create();

        Assert.Equal(500000m, (await service.ListAsync(7)).Single().DepositAmount);
    }

    [Fact]
    public async Task OutsideBranch_CannotChangeListingOrDeposit()
    {
        var (service, store, policies) = Create(branchId: 99);

        var result = await service.SaveAsync(7, Request(true, 850000));

        Assert.False(result.Success);
        Assert.Null(store.Saved);
        Assert.Equal(500000m, (await policies.GetAsync(10))!.Amount);
    }

    private static SaveRoomListing Request(bool publish, decimal? amount) =>
        new(10, " Tin phòng ", " Mô tả ", publish, [], amount);

    private static (RoomListingService Service, Listings Store, Policies Policies) Create(int branchId = 3)
    {
        var store = new Listings(branchId);
        var policies = new Policies();
        var access = new FakeEmployeeAccessService { ScopeToReturn = new EmployeeAccessScope(7, false, [3]) };
        return (new RoomListingService(store, access, new NoStorage(),
            NullLogger<RoomListingService>.Instance, policies), store, policies);
    }

    private sealed class Listings(int branchId) : IRoomListingStore
    {
        private readonly RoomListingDto room = new(10, branchId, "103", "Chi nhánh", 2500000,
            20, 0, true, true, "Tin cũ", "", 0, null, false);
        public bool SaveResult { get; set; } = true;
        public SaveRoomListing? Saved { get; private set; }
        public Task<IReadOnlyList<RoomListingDto>> ListAsync(IReadOnlyCollection<int>? branchIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RoomListingDto>>([room]);
        public Task<RoomListingDto?> GetAsync(int roomId, CancellationToken ct) => Task.FromResult<RoomListingDto?>(room);
        public Task<bool> SaveAsync(int actorId, int branch, SaveRoomListing request,
            IReadOnlyList<ListingPhoto> photos, CancellationToken ct)
        {
            Saved = request;
            return Task.FromResult(SaveResult);
        }
    }

    private sealed class Policies : IRoomDepositPolicyStore
    {
        private readonly Dictionary<int, RoomDepositPolicy> values = new() { [10] = new(500000) };
        public Task<RoomDepositPolicy?> GetAsync(int roomId, CancellationToken ct = default) =>
            Task.FromResult(values.GetValueOrDefault(roomId));
        public Task<IReadOnlyDictionary<int, RoomDepositPolicy>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, RoomDepositPolicy>>(values);
        public Task SetAsync(int roomId, RoomDepositPolicy policy, CancellationToken ct = default)
        { values[roomId] = policy; return Task.CompletedTask; }
        public async Task<bool> UpdateListingAsync(int roomId, RoomDepositPolicy policy,
            Func<CancellationToken, Task<bool>> saveListing, CancellationToken ct = default)
        {
            if (!await saveListing(ct)) return false;
            values[roomId] = policy;
            return true;
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
