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
    public class JsonInvoiceReminderStateStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly TestHostEnvironment _environment;

        public JsonInvoiceReminderStateStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "InvoiceReminderStateStoreTests_" + Guid.NewGuid().ToString("N"));
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
            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);

            var sentIds = await store.GetSentInvoiceIdsAsync();
            var hasSent = await store.HasBeenSentAsync(999);

            Assert.Empty(sentIds);
            Assert.False(hasSent);
        }

        [Fact]
        public async Task LoadExistingLegacyJson_ShouldParseSuccessfully()
        {
            var filePath = Path.Combine(_tempDir, "sent_invoice_reminders.json");
            var legacyData = new[] { 101, 102, 103 };
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(legacyData));

            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            var sentIds = await store.GetSentInvoiceIdsAsync();

            Assert.Equal(3, sentIds.Count);
            Assert.Contains(101, sentIds);
            Assert.Contains(102, sentIds);
            Assert.Contains(103, sentIds);
            Assert.True(await store.HasBeenSentAsync(101));
            Assert.False(await store.HasBeenSentAsync(999));
        }

        [Fact]
        public async Task MarkAsSent_ShouldPersist_AndBeReadableFromNewInstance()
        {
            var store1 = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            await store1.MarkAsSentAsync(201);
            await store1.MarkAsSentAsync(202);

            // Create brand new instance to verify physical persistence
            var store2 = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            Assert.True(await store2.HasBeenSentAsync(201));
            Assert.True(await store2.HasBeenSentAsync(202));
            Assert.False(await store2.HasBeenSentAsync(203));

            var sentIds = await store2.GetSentInvoiceIdsAsync();
            Assert.Equal(2, sentIds.Count);
        }

        [Fact]
        public async Task DuplicateMark_ShouldNotDuplicateRecords()
        {
            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            await store.MarkAsSentAsync(301);
            await store.MarkAsSentAsync(301);
            await store.MarkAsSentAsync(301);

            var filePath = Path.Combine(_tempDir, "sent_invoice_reminders.json");
            Assert.True(File.Exists(filePath));

            var json = await File.ReadAllTextAsync(filePath);
            var deserialized = JsonSerializer.Deserialize<int[]>(json);

            Assert.NotNull(deserialized);
            Assert.Single(deserialized);
            Assert.Equal(301, deserialized[0]);
        }

        [Fact]
        public async Task ConcurrentMarks_ShouldNotLoseKeys_AndResultInValidJson()
        {
            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            const int concurrency = 20;

            var tasks = Enumerable.Range(1, concurrency)
                .Select(id => store.MarkAsSentAsync(id))
                .ToArray();

            await Task.WhenAll(tasks);

            var storeVerify = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            var sentIds = await storeVerify.GetSentInvoiceIdsAsync();

            Assert.Equal(concurrency, sentIds.Count);
            for (int i = 1; i <= concurrency; i++)
            {
                Assert.Contains(i, sentIds);
            }

            // Verify raw file is valid JSON
            var filePath = Path.Combine(_tempDir, "sent_invoice_reminders.json");
            var json = await File.ReadAllTextAsync(filePath);
            var parsed = JsonSerializer.Deserialize<int[]>(json);
            Assert.NotNull(parsed);
            Assert.Equal(concurrency, parsed.Length);
        }

        [Fact]
        public async Task AtomicReplacement_ShouldNotLeaveTmpFiles_AfterSuccess()
        {
            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            await store.MarkAsSentAsync(501);
            await store.MarkAsSentAsync(502);

            var tmpFiles = Directory.GetFiles(_tempDir, "*.tmp");
            Assert.Empty(tmpFiles);
        }

        [Fact]
        public async Task CancellationToken_CancelledBeforeOperation_ShouldThrowOperationCanceledException()
        {
            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetSentInvoiceIdsAsync(cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.HasBeenSentAsync(101, cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.MarkAsSentAsync(101, cts.Token));
        }

        [Fact]
        public async Task MalformedJson_ShouldThrowJsonException_AndNotSwallowError()
        {
            var filePath = Path.Combine(_tempDir, "sent_invoice_reminders.json");
            await File.WriteAllTextAsync(filePath, "{ this is invalid json content }");

            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);

            await Assert.ThrowsAnyAsync<JsonException>(() => store.GetSentInvoiceIdsAsync());
            await Assert.ThrowsAnyAsync<JsonException>(() => store.HasBeenSentAsync(101));
            await Assert.ThrowsAnyAsync<JsonException>(() => store.MarkAsSentAsync(101));
        }

        [Fact]
        public async Task ReadError_WhenFileIsLocked_ShouldPropagateIOException()
        {
            var filePath = Path.Combine(_tempDir, "sent_invoice_reminders.json");
            await File.WriteAllTextAsync(filePath, "[1, 2, 3]");

            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await Assert.ThrowsAnyAsync<IOException>(() => store.GetSentInvoiceIdsAsync());
                await Assert.ThrowsAnyAsync<IOException>(() => store.HasBeenSentAsync(1));
                await Assert.ThrowsAnyAsync<IOException>(() => store.MarkAsSentAsync(4));
            }
        }

        [Fact]
        public async Task WriteFailure_ShouldCleanUpTmpFile_AndPropagateException()
        {
            var filePath = Path.Combine(_tempDir, "sent_invoice_reminders.json");
            await File.WriteAllTextAsync(filePath, "[10, 20]");

            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);

            // Lock destination file so File.Move fails when trying to overwrite
            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => store.MarkAsSentAsync(30));
                Assert.True(ex is IOException || ex is UnauthorizedAccessException);
            }

            // Temp files should have been cleaned up in finally block
            var tmpFiles = Directory.GetFiles(_tempDir, "*.tmp");
            Assert.Empty(tmpFiles);

            // Verify original file state remains untouched
            var verifyStore = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance);
            Assert.False(await verifyStore.HasBeenSentAsync(30));
            var sentIds = await verifyStore.GetSentInvoiceIdsAsync();
            Assert.Equal(2, sentIds.Count);
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
