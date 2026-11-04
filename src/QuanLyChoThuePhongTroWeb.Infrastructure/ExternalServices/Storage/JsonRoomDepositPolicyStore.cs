using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Storage;

public sealed class JsonRoomDepositPolicyStore : IRoomDepositPolicyStore
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true
    };
    private readonly string _storagePath;
    private readonly string _lockPath;

    public JsonRoomDepositPolicyStore(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        var root = Path.GetFullPath(environment.ContentRootPath);
        var configuredPath = configuration["RoomDeposits:StoragePath"];
        _storagePath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(root, "App_Data", "room-deposits.json")
            : Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(root, configuredPath));
        if (_storagePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Room deposit policies must be stored outside wwwroot.", nameof(configuration));
        _lockPath = _storagePath + ".lock";
        EnsurePrivateFilePaths();
    }

    public async Task<RoomDepositPolicy?> GetAsync(int roomId, CancellationToken ct = default)
    {
        ValidateRoomId(roomId);
        await using var fileLock = await AcquireLockAsync(ct);
        var snapshot = await ReadAsync(ct);
        return snapshot.Policies.GetValueOrDefault(roomId);
    }

    public async Task<IReadOnlyDictionary<int, RoomDepositPolicy>> GetAllAsync(CancellationToken ct = default)
    {
        await using var fileLock = await AcquireLockAsync(ct);
        return (await ReadAsync(ct)).Policies;
    }

    public async Task SetAsync(int roomId, RoomDepositPolicy policy, CancellationToken ct = default)
    {
        Validate(roomId, policy);
        await using var fileLock = await AcquireLockAsync(ct);
        var snapshot = await ReadAsync(ct);
        snapshot.Policies[roomId] = policy;
        await CommitAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot.Policies, JsonOptions), ct);
    }

    public async Task<bool> UpdateListingAsync(int roomId, RoomDepositPolicy policy,
        Func<CancellationToken, Task<bool>> saveListing, CancellationToken ct = default)
    {
        Validate(roomId, policy);
        ArgumentNullException.ThrowIfNull(saveListing);
        await using var fileLock = await AcquireLockAsync(ct);
        var snapshot = await ReadAsync(ct);
        snapshot.Policies[roomId] = policy;
        await CommitAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot.Policies, JsonOptions), ct);

        bool saved;
        try
        {
            ct.ThrowIfCancellationRequested();
            saved = await saveListing(ct);
        }
        catch (Exception saveError)
        {
            try
            {
                await RestoreAsync(snapshot.OriginalBytes);
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("Listing save and deposit policy rollback both failed.",
                    saveError, rollbackError);
            }
            throw;
        }

        if (!saved) await RestoreAsync(snapshot.OriginalBytes);
        return saved;
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsurePrivateFilePaths();
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
        var watch = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Keep the lock file in place: deleting it could let another process lock a different file.
                return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException error)
            {
                var remaining = LockTimeout - watch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException("Timed out waiting for the room deposit policy file lock.", error);
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50)
                    ? remaining : TimeSpan.FromMilliseconds(50), ct);
            }
        }
    }

    private void EnsurePrivateFilePaths()
    {
        // A private-looking path must not be redirected into the public tree by a link or junction.
        foreach (var filePath in new[] { _storagePath, _lockPath })
        {
            for (string? path = filePath; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new ArgumentException("Room deposit storage paths must not contain filesystem links.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
    }

    private async Task<Snapshot> ReadAsync(CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_storagePath, ct);
        }
        catch (FileNotFoundException)
        {
            return new Snapshot(new Dictionary<int, RoomDepositPolicy>(), null);
        }

        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Room deposit policies must be a JSON object keyed by room ID.");
        var policies = new Dictionary<int, RoomDepositPolicy>();
        foreach (var entry in json.RootElement.EnumerateObject())
        {
            if (!int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var roomId) || roomId <= 0)
                throw new JsonException("A room deposit policy has an invalid room ID.");
            var policy = entry.Value.Deserialize<RoomDepositPolicy>();
            if (policy is null || !policy.IsValid || !policies.TryAdd(roomId, policy))
                throw new JsonException("A room deposit policy is invalid or its room ID is duplicated.");
        }
        ct.ThrowIfCancellationRequested();
        return new Snapshot(policies, bytes);
    }

    private async Task CommitAsync(byte[] bytes, CancellationToken ct)
    {
        var tempPath = _storagePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await temp.WriteAsync(bytes, ct);
                await temp.FlushAsync(ct);
                temp.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            // The temp file is on the same filesystem; replacement never exposes a partial JSON document.
            File.Move(tempPath, _storagePath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private Task RestoreAsync(byte[]? previousBytes)
    {
        // A cancelled metadata request must still finish rolling back while it owns the lock.
        if (previousBytes is not null) return CommitAsync(previousBytes, CancellationToken.None);
        File.Delete(_storagePath);
        return Task.CompletedTask;
    }

    private static void ValidateRoomId(int roomId)
    {
        if (roomId <= 0) throw new ArgumentOutOfRangeException(nameof(roomId));
    }

    private static void Validate(int roomId, RoomDepositPolicy policy)
    {
        ValidateRoomId(roomId);
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.IsValid) throw new ArgumentException("The room deposit policy is invalid.", nameof(policy));
    }

    private sealed record Snapshot(Dictionary<int, RoomDepositPolicy> Policies, byte[]? OriginalBytes);
}
