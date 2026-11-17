using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.State;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    // BackgroundJobs:StatePath đưa file "đã gửi nhắc/cảnh báo" ra ổ bền (Azure: /home/...), để deploy lại không gửi trùng email.
    public class BackgroundJobStatePathTests : IDisposable
    {
        private readonly string _contentRoot;
        private readonly string _stateDir;
        private readonly TestHostEnvironment _environment;

        public BackgroundJobStatePathTests()
        {
            var root = Path.Combine(Path.GetTempPath(), "BackgroundJobStatePathTests_" + Guid.NewGuid().ToString("N"));
            _contentRoot = Path.Combine(root, "app");
            _stateDir = Path.Combine(root, "home", "state");
            Directory.CreateDirectory(_contentRoot);
            _environment = new TestHostEnvironment { ContentRootPath = _contentRoot };
        }

        public void Dispose()
        {
            try
            {
                var root = Directory.GetParent(_contentRoot)!.FullName;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        private static IConfiguration Config(string? statePath) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["BackgroundJobs:StatePath"] = statePath })
                .Build();

        [Fact]
        public async Task InvoiceReminder_WithStatePath_WritesIntoThatDirectory_AndCreatesIt()
        {
            var store = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance, Config(_stateDir));

            await store.MarkAsSentAsync(42);

            Assert.True(File.Exists(Path.Combine(_stateDir, "sent_invoice_reminders.json")));
            Assert.False(File.Exists(Path.Combine(_contentRoot, "sent_invoice_reminders.json")));
            var reopened = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance, Config(_stateDir));
            Assert.True(await reopened.HasBeenSentAsync(42));
        }

        [Fact]
        public async Task ContractAlert_WithStatePath_WritesIntoThatDirectory_AndCreatesIt()
        {
            var store = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance, Config(_stateDir));

            await store.MarkAsSentAsync("7_2026-11-30");

            Assert.True(File.Exists(Path.Combine(_stateDir, "sent_contract_alerts.json")));
            Assert.False(File.Exists(Path.Combine(_contentRoot, "sent_contract_alerts.json")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task WithoutStatePath_KeepsContentRoot(string? statePath)
        {
            var invoices = new JsonInvoiceReminderStateStore(_environment, NullLogger<JsonInvoiceReminderStateStore>.Instance, Config(statePath));
            var alerts = new JsonContractExpiryAlertStateStore(_environment, NullLogger<JsonContractExpiryAlertStateStore>.Instance, Config(statePath));

            await invoices.MarkAsSentAsync(1);
            await alerts.MarkAsSentAsync("1_2026-11-30");

            Assert.True(File.Exists(Path.Combine(_contentRoot, "sent_invoice_reminders.json")));
            Assert.True(File.Exists(Path.Combine(_contentRoot, "sent_contract_alerts.json")));
            Assert.False(Directory.Exists(_stateDir));
        }

        private class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Testing";
            public string ApplicationName { get; set; } = "Test";
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }
    }
}
