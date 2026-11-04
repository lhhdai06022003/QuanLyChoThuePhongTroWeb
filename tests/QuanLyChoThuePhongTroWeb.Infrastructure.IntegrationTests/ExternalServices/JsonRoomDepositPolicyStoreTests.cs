using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices;

public sealed class JsonRoomDepositPolicyStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "RoomDepositPolicyStoreTests_" + Guid.NewGuid().ToString("N"));

    private string StoragePath => Path.Combine(_root, "App_Data", "room-deposits.json");

    public JsonRoomDepositPolicyStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Missing_file_has_no_default_deposit()
    {
        var store = CreateStore();

        Assert.Null(await store.GetAsync(12));
        Assert.Empty(await store.GetAllAsync());
        Assert.False(File.Exists(StoragePath));
    }

    [Fact]
    public async Task Set_persists_amount_and_windows_for_a_new_instance()
    {
        await CreateStore().SetAsync(12, new RoomDepositPolicy(1_500_000m, 60, 72));
        await CreateStore().SetAsync(34, new RoomDepositPolicy(1_000_000_000_000m, 1440, 720));

        var reopened = CreateStore();
        Assert.Equal(new RoomDepositPolicy(1_500_000m, 60, 72), await reopened.GetAsync(12));
        Assert.Equal(new RoomDepositPolicy(1_000_000_000_000m, 1440, 720), await reopened.GetAsync(34));
        Assert.Null(await reopened.GetAsync(99));
        Assert.Equal(2, (await reopened.GetAllAsync()).Count);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(StoragePath)!, "*.tmp"));
    }

    [Fact]
    public async Task Separate_instances_updating_different_rooms_do_not_lose_updates()
    {
        await Task.WhenAll(Enumerable.Range(1, 24).Select(id =>
            CreateStore().SetAsync(id, new RoomDepositPolicy(id * 100_000m))));

        var policies = await CreateStore().GetAllAsync();
        Assert.Equal(24, policies.Count);
        for (var id = 1; id <= 24; id++)
            Assert.Equal(id * 100_000m, policies[id].Amount);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(StoragePath));
        Assert.Equal(24, json.RootElement.EnumerateObject().Count());
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"0\":{\"Amount\":500000}}")]
    [InlineData("{\"abc\":{\"Amount\":500000}}")]
    [InlineData("{\"12\":null}")]
    [InlineData("{\"12\":{\"Amount\":0}}")]
    [InlineData("{\"12\":{\"Amount\":100.5}}")]
    [InlineData("{\"12\":{\"Amount\":1000000000001}}")]
    [InlineData("{\"12\":{\"Amount\":500000,\"PaymentWindowMinutes\":0}}")]
    [InlineData("{\"12\":{\"Amount\":500000,\"ContractWindowHours\":721}}")]
    [InlineData("{\"12\":{\"Amount\":500000},\"12\":{\"Amount\":600000}}")]
    public async Task Corrupt_or_invalid_policy_file_fails_closed_without_overwriting(string original)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        await File.WriteAllTextAsync(StoragePath, original);
        var store = CreateStore();
        var callbackCalled = false;

        await Assert.ThrowsAnyAsync<JsonException>(() => store.GetAsync(12));
        await Assert.ThrowsAnyAsync<JsonException>(() => store.GetAllAsync());
        await Assert.ThrowsAnyAsync<JsonException>(() => store.SetAsync(12, new RoomDepositPolicy(700_000m)));
        await Assert.ThrowsAnyAsync<JsonException>(() => store.UpdateListingAsync(12,
            new RoomDepositPolicy(700_000m), _ => { callbackCalled = true; return Task.FromResult(true); }));

        Assert.False(callbackCalled);
        Assert.Equal(original, await File.ReadAllTextAsync(StoragePath));
    }

    [Theory]
    [InlineData(0, 30, 48)]
    [InlineData(-1, 30, 48)]
    [InlineData(0.5, 30, 48)]
    [InlineData(1000000000001, 30, 48)]
    [InlineData(100000, 0, 48)]
    [InlineData(100000, 1441, 48)]
    [InlineData(100000, 30, 0)]
    [InlineData(100000, 30, 721)]
    public async Task Invalid_policy_is_rejected_before_metadata_is_saved(
        decimal amount, int paymentMinutes, int contractHours)
    {
        var store = CreateStore();
        var policy = new RoomDepositPolicy(amount, paymentMinutes, contractHours);
        var callbackCalled = false;

        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.SetAsync(12, policy));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.UpdateListingAsync(12, policy,
            _ => { callbackCalled = true; return Task.FromResult(true); }));

        Assert.False(callbackCalled);
        Assert.False(File.Exists(StoragePath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_room_id_is_rejected(int roomId)
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetAsync(roomId));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SetAsync(roomId, new RoomDepositPolicy(1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.UpdateListingAsync(roomId,
            new RoomDepositPolicy(1), _ => Task.FromResult(true)));
    }

    [Fact]
    public async Task Configured_relative_private_path_is_resolved_under_content_root()
    {
        var store = CreateStore("private/room-deposits.json");

        await store.SetAsync(12, new RoomDepositPolicy(500_000m));

        Assert.True(File.Exists(Path.Combine(_root, "private", "room-deposits.json")));
        Assert.Equal(500_000m, (await CreateStore("private/room-deposits.json").GetAsync(12))!.Amount);
    }

    [Theory]
    [InlineData("wwwroot/room-deposits.json")]
    [InlineData("wwwroot/private/room-deposits.json")]
    [InlineData("App_Data/../wwwroot/room-deposits.json")]
    [InlineData("WWWROOT/room-deposits.json")]
    public void Public_storage_path_is_rejected(string path)
    {
        Assert.Throws<ArgumentException>(() => CreateStore(path));
    }

    [Fact]
    public async Task Private_path_linked_into_public_directory_is_rejected()
    {
        var link = await CreatePublicDirectoryLinkAsync();
        try
        {
            Assert.Throws<ArgumentException>(() => CreateStore("private-link/room-deposits.json"));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task Private_directory_replaced_with_public_link_after_construction_is_rejected()
    {
        var store = CreateStore("private-link/room-deposits.json");
        var link = await CreatePublicDirectoryLinkAsync();
        try
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => store.SetAsync(12, new RoomDepositPolicy(500_000m)));
            Assert.False(File.Exists(Path.Combine(_root, "wwwroot", "private", "room-deposits.json")));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task Successful_listing_update_persists_price_before_saving_metadata()
    {
        var store = CreateStore();
        await store.SetAsync(12, new RoomDepositPolicy(500_000m));

        var saved = await store.UpdateListingAsync(12, new RoomDepositPolicy(700_000m), async _ =>
        {
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(StoragePath));
            Assert.Equal(700_000m, json.RootElement.GetProperty("12").GetProperty("Amount").GetDecimal());
            return true;
        });

        Assert.True(saved);
        Assert.Equal(700_000m, (await CreateStore().GetAsync(12))!.Amount);
    }

    [Fact]
    public async Task Failed_listing_save_restores_exact_previous_file()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        const string original = "{ \"12\": { \"Amount\": 500000 }, \"34\": { \"Amount\": 600000 } }";
        await File.WriteAllTextAsync(StoragePath, original);

        var saved = await CreateStore().UpdateListingAsync(12, new RoomDepositPolicy(700_000m),
            _ => Task.FromResult(false));

        Assert.False(saved);
        Assert.Equal(original, await File.ReadAllTextAsync(StoragePath));
        Assert.Equal(600_000m, (await CreateStore().GetAsync(34))!.Amount);
    }

    [Fact]
    public async Task Throwing_listing_save_restores_previous_price_and_rethrows()
    {
        await CreateStore().SetAsync(12, new RoomDepositPolicy(500_000m));
        var original = await File.ReadAllBytesAsync(StoragePath);
        var failure = new InvalidOperationException("Metadata save failed.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateStore().UpdateListingAsync(
            12, new RoomDepositPolicy(700_000m), _ => throw failure));

        Assert.Same(failure, thrown);
        Assert.Equal(original, await File.ReadAllBytesAsync(StoragePath));
    }

    [Fact]
    public async Task Failed_first_listing_save_restores_absent_file()
    {
        Assert.False(await CreateStore().UpdateListingAsync(12, new RoomDepositPolicy(700_000m),
            _ => Task.FromResult(false)));

        Assert.False(File.Exists(StoragePath));
        Assert.Null(await CreateStore().GetAsync(12));
    }

    [Fact]
    public async Task Listing_save_holds_lock_until_rollback_before_a_waiting_writer_runs()
    {
        var first = CreateStore();
        await first.SetAsync(12, new RoomDepositPolicy(500_000m));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var update = first.UpdateListingAsync(12, new RoomDepositPolicy(700_000m), async _ =>
        {
            entered.SetResult();
            await release.Task;
            return false;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateStore().SetAsync(34, new RoomDepositPolicy(600_000m), cancellation.Token));
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.False(await update);
        await CreateStore().SetAsync(34, new RoomDepositPolicy(600_000m));
        Assert.Equal(500_000m, (await CreateStore().GetAsync(12))!.Amount);
        Assert.Equal(600_000m, (await CreateStore().GetAsync(34))!.Amount);
    }

    [Fact]
    public async Task Cancelled_metadata_save_still_restores_previous_price()
    {
        await CreateStore().SetAsync(12, new RoomDepositPolicy(500_000m));
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateStore().UpdateListingAsync(12,
            new RoomDepositPolicy(700_000m), ct =>
            {
                cancellation.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            }, cancellation.Token));

        Assert.Equal(500_000m, (await CreateStore().GetAsync(12))!.Amount);
    }

    [Fact]
    public async Task Cancelled_operations_do_not_create_deposit_file()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetAsync(12, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetAllAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SetAsync(12,
            new RoomDepositPolicy(500_000m), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpdateListingAsync(12,
            new RoomDepositPolicy(500_000m), _ => Task.FromResult(true), cancellation.Token));

        Assert.False(File.Exists(StoragePath));
    }

    [Fact]
    public async Task External_file_lock_blocks_read_and_wait_is_cancellable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        await using var held = new FileStream(StoragePath + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateStore().GetAsync(12, cancellation.Token));
    }

    [Fact]
    public async Task External_file_lock_has_a_bounded_wait_without_cancellation()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        await using var held = new FileStream(StoragePath + ".lock", FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() => CreateStore().SetAsync(12, new RoomDepositPolicy(1)));

        Assert.InRange(watch.Elapsed, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(15));
        Assert.False(File.Exists(StoragePath));
    }

    private JsonRoomDepositPolicyStore CreateStore(string? storagePath = null)
    {
        var values = new Dictionary<string, string?>();
        if (storagePath is not null) values["RoomDeposits:StoragePath"] = storagePath;
        return new JsonRoomDepositPolicyStore(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestHostEnvironment { ContentRootPath = _root });
    }

    private async Task<string> CreatePublicDirectoryLinkAsync()
    {
        var destination = Path.Combine(_root, "wwwroot", "private");
        var link = Path.Combine(_root, "private-link");
        Directory.CreateDirectory(destination);
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, destination })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.ExitCode == 0, await process.StandardError.ReadToEndAsync());
        }
        else
        {
            Directory.CreateSymbolicLink(link, destination);
        }
        return link;
    }

    public void Dispose()
    {
        var target = Path.GetFullPath(_root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(target).StartsWith("RoomDepositPolicyStoreTests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to clean up outside the test's temporary directory.");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "RoomDepositPolicyStoreTests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
