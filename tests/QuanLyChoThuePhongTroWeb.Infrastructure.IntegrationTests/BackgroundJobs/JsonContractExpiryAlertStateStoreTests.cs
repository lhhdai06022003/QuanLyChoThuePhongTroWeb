using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.State;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    public class JsonContractExpiryAlertStateStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly TestHostEnvironment _environment;

        public JsonContractExpiryAlertStateStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ContractExpiryAlertStateStoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _environment = new TestHostEnvironment { ContentRootPath = _tempDir };
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        [Fact]
        public async Task MissingFile_ShouldReturnEmptySet_WithoutThrowing()
        {
            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);

            var sentKeys = await store.GetSentAlertKeysAsync();
            var hasSent = await store.HasBeenSentAsync("HD001_30d");

            Assert.Empty(sentKeys);
            Assert.False(hasSent);
        }

        [Fact]
        public async Task LoadExistingLegacyJson_ShouldParseSuccessfully()
        {
            var filePath = Path.Combine(_tempDir, "sent_contract_alerts.json");
            var legacyData = new[] { "HD001_30", "HD002_15", "HD003_30" };
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(legacyData));

            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            var sentKeys = await store.GetSentAlertKeysAsync();

            Assert.Equal(3, sentKeys.Count);
            Assert.Contains("HD001_30", sentKeys);
            Assert.Contains("HD002_15", sentKeys);
            Assert.Contains("HD003_30", sentKeys);
            Assert.True(await store.HasBeenSentAsync("HD001_30"));
            Assert.False(await store.HasBeenSentAsync("HD999_30"));
        }

        [Fact]
        public async Task MarkAsSent_ShouldPersist_AndBeReadableFromNewInstance()
        {
            var store1 = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            await store1.MarkAsSentAsync("HD_ALPHA_30");
            await store1.MarkAsSentAsync("HD_BETA_15");

            // Create brand new instance to verify physical persistence
            var store2 = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            Assert.True(await store2.HasBeenSentAsync("HD_ALPHA_30"));
            Assert.True(await store2.HasBeenSentAsync("HD_BETA_15"));
            Assert.False(await store2.HasBeenSentAsync("HD_GAMMA_30"));

            var sentKeys = await store2.GetSentAlertKeysAsync();
            Assert.Equal(2, sentKeys.Count);
        }

        [Fact]
        public async Task DuplicateMark_ShouldNotDuplicateRecords()
        {
            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            await store.MarkAsSentAsync("HD_DUP_30");
            await store.MarkAsSentAsync("HD_DUP_30");
            await store.MarkAsSentAsync("HD_DUP_30");

            var filePath = Path.Combine(_tempDir, "sent_contract_alerts.json");
            Assert.True(File.Exists(filePath));

            var json = await File.ReadAllTextAsync(filePath);
            var deserialized = JsonSerializer.Deserialize<string[]>(json);

            Assert.NotNull(deserialized);
            Assert.Single(deserialized);
            Assert.Equal("HD_DUP_30", deserialized[0]);
        }

        [Fact]
        public async Task ConcurrentMarks_ShouldNotLoseKeys_AndResultInValidJson()
        {
            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            const int concurrency = 20;

            var tasks = Enumerable.Range(1, concurrency)
                .Select(i => store.MarkAsSentAsync($"HD_KEY_{i:D3}_30"))
                .ToArray();

            await Task.WhenAll(tasks);

            var storeVerify = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            var sentKeys = await storeVerify.GetSentAlertKeysAsync();

            Assert.Equal(concurrency, sentKeys.Count);
            for (int i = 1; i <= concurrency; i++)
            {
                Assert.Contains($"HD_KEY_{i:D3}_30", sentKeys);
            }

            // Verify raw file is valid JSON
            var filePath = Path.Combine(_tempDir, "sent_contract_alerts.json");
            var json = await File.ReadAllTextAsync(filePath);
            var parsed = JsonSerializer.Deserialize<string[]>(json);
            Assert.NotNull(parsed);
            Assert.Equal(concurrency, parsed.Length);
        }

        [Fact]
        public async Task AtomicReplacement_ShouldNotLeaveTmpFiles_AfterSuccess()
        {
            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            await store.MarkAsSentAsync("KEY1");
            await store.MarkAsSentAsync("KEY2");

            var tmpFiles = Directory.GetFiles(_tempDir, "*.tmp");
            Assert.Empty(tmpFiles);
        }

        [Fact]
        public async Task CancellationToken_CancelledBeforeOperation_ShouldThrowOperationCanceledException()
        {
            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetSentAlertKeysAsync(cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.HasBeenSentAsync("KEY1", cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.MarkAsSentAsync("KEY1", cts.Token));
        }

        [Fact]
        public async Task MalformedJson_ShouldThrowJsonException_AndNotSwallowError()
        {
            var filePath = Path.Combine(_tempDir, "sent_contract_alerts.json");
            await File.WriteAllTextAsync(filePath, "{ this is invalid json content }");

            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);

            await Assert.ThrowsAnyAsync<JsonException>(() => store.GetSentAlertKeysAsync());
            await Assert.ThrowsAnyAsync<JsonException>(() => store.HasBeenSentAsync("KEY1"));
            await Assert.ThrowsAnyAsync<JsonException>(() => store.MarkAsSentAsync("KEY1"));
        }

        [Fact]
        public async Task ReadError_WhenFileIsLocked_ShouldPropagateIOException()
        {
            var filePath = Path.Combine(_tempDir, "sent_contract_alerts.json");
            await File.WriteAllTextAsync(filePath, "[\"HD1\", \"HD2\"]");

            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await Assert.ThrowsAnyAsync<IOException>(() => store.GetSentAlertKeysAsync());
                await Assert.ThrowsAnyAsync<IOException>(() => store.HasBeenSentAsync("HD1"));
                await Assert.ThrowsAnyAsync<IOException>(() => store.MarkAsSentAsync("HD3"));
            }
        }

        [Fact]
        public async Task WriteFailure_ShouldCleanUpTmpFile_AndPropagateException()
        {
            var filePath = Path.Combine(_tempDir, "sent_contract_alerts.json");
            await File.WriteAllTextAsync(filePath, "[\"KEY_EXISTING\"]");

            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);

            // Lock destination file so File.Move fails when trying to overwrite
            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => store.MarkAsSentAsync("KEY_NEW"));
                Assert.True(ex is IOException || ex is UnauthorizedAccessException);
            }

            // Temp files should have been cleaned up in finally block
            var tmpFiles = Directory.GetFiles(_tempDir, "*.tmp");
            Assert.Empty(tmpFiles);

            // Verify original file state remains untouched
            var verifyStore = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance);
            Assert.False(await verifyStore.HasBeenSentAsync("KEY_NEW"));
            var sentKeys = await verifyStore.GetSentAlertKeysAsync();
            Assert.Single(sentKeys);
        }

        private class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Testing";
            public string ApplicationName { get; set; } = "TestApp";
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }
    }
}
