using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    public class BackgroundJobsRuntimeSmokeTests
    {
        [Fact]
        public async Task EnabledBackgroundJobs_WhenStoppedDuringUseCaseExecution_ShouldExitWithoutErrorLogs()
        {
            var autoClose = new BlockingContractAutoCloseUseCase();
            var expiryAlert = new BlockingContractExpiryAlertUseCase();
            var invoiceReminder = new BlockingInvoiceReminderUseCase();
            var logSink = new InMemoryLogSink();

            var hostBuilder = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused_test;Username=unused;Password=unused",
                        ["BackgroundJobs:Enabled"] = "true"
                    });
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration);
                    services.RemoveAll<IContractAutoCloseUseCase>();
                    services.AddScoped<IContractAutoCloseUseCase>(_ => autoClose);
                    services.RemoveAll<IContractExpiryAlertUseCase>();
                    services.AddScoped<IContractExpiryAlertUseCase>(_ => expiryAlert);
                    services.RemoveAll<IInvoiceReminderUseCase>();
                    services.AddScoped<IInvoiceReminderUseCase>(_ => invoiceReminder);
                    AddTestLogging(services, logSink);
                });

            using var host = hostBuilder.Build();
            await host.StartAsync();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Task.WhenAll(
                autoClose.Entered.WaitAsync(timeout.Token),
                expiryAlert.Entered.WaitAsync(timeout.Token),
                invoiceReminder.Entered.WaitAsync(timeout.Token));

            await host.StopAsync(timeout.Token);

            AssertNoErrorLogs(logSink);
            AssertStartupLogFor<ContractAutoCloseJob>(logSink);
            AssertStartupLogFor<ContractExpiryAlertJob>(logSink);
            AssertStartupLogFor<InvoiceReminderJob>(logSink);
            AssertShutdownLogFor<ContractAutoCloseJob>(logSink);
            AssertShutdownLogFor<ContractExpiryAlertJob>(logSink);
            AssertShutdownLogFor<InvoiceReminderJob>(logSink);
        }

        [Fact]
        public async Task EnabledBackgroundJobs_InSafeRuntimeHost_ShouldRegister_ExecuteFirstCycle_AndStopCleanly()
        {
            // 1. Synchronization primitives to observe first cycle without waiting for long production delays
            var autoCloseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var expiryAlertTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var invoiceReminderTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var logSink = new InMemoryLogSink();

            // 2. Build isolated test-safe host with BackgroundJobs:Enabled = true
            var hostBuilder = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=quanlyphongtro_dummy_test;Username=test;Password=test",
                        ["BackgroundJobs:Enabled"] = "true",
                        ["Logging:LogLevel:Default"] = "Information"
                    });
                })
                .ConfigureServices((context, services) =>
                {
                    // Register infrastructure layer
                    services.AddInfrastructure(context.Configuration);

                    // Replace use cases with observable test doubles to avoid hitting database while proving job execution
                    services.RemoveAll<IContractAutoCloseUseCase>();
                    services.AddScoped<IContractAutoCloseUseCase>(_ => new ObservableContractAutoCloseUseCase(autoCloseTcs));

                    services.RemoveAll<IContractExpiryAlertUseCase>();
                    services.AddScoped<IContractExpiryAlertUseCase>(_ => new ObservableContractExpiryAlertUseCase(expiryAlertTcs));

                    services.RemoveAll<IInvoiceReminderUseCase>();
                    services.AddScoped<IInvoiceReminderUseCase>(_ => new ObservableInvoiceReminderUseCase(invoiceReminderTcs));

                    // Replace external services with fakes to prevent any real side-effects
                    services.RemoveAll<IEmailService>();
                    services.AddScoped<IEmailService, FakeEmailService>();

                    services.RemoveAll<IImageStorageService>();
                    services.AddScoped<IImageStorageService, FakeImageStorageService>();

                    // Add log sink
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(new InMemoryLogProvider(logSink));
                        logging.SetMinimumLevel(LogLevel.Trace);
                    });
                });

            using var host = hostBuilder.Build();

            // 3. Assertion 1: All 3 background hosted jobs must be registered
            var hostedServices = host.Services.GetServices<IHostedService>().ToList();
            Assert.Contains(hostedServices, s => s is ContractAutoCloseJob);
            Assert.Contains(hostedServices, s => s is ContractExpiryAlertJob);
            Assert.Contains(hostedServices, s => s is InvoiceReminderJob);

            // 4. Start host safely
            await host.StartAsync();

            // 5. Assertion 2: All 3 background jobs must execute their first cycle
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var completed = await Task.WhenAll(
                autoCloseTcs.Task.WaitAsync(cts.Token),
                expiryAlertTcs.Task.WaitAsync(cts.Token),
                invoiceReminderTcs.Task.WaitAsync(cts.Token)
            );

            Assert.True(completed[0], "ContractAutoCloseJob first cycle did not execute.");
            Assert.True(completed[1], "ContractExpiryAlertJob first cycle did not execute.");
            Assert.True(completed[2], "InvoiceReminderJob first cycle did not execute.");

            await Task.WhenAll(
                WaitForLogAsync<ContractAutoCloseJob>(logSink, "sẽ chạy lại", cts.Token),
                WaitForLogAsync<ContractExpiryAlertJob>(logSink, "sẽ chạy lại", cts.Token),
                WaitForLogAsync<InvoiceReminderJob>(logSink, "sẽ chạy lại", cts.Token));

            // 6. Stop host cleanly after all jobs have entered their production delay
            await host.StopAsync();

            // 7. Assertion 3: No Error or Critical logs captured, and no unhandled TaskCanceledException
            var errors = logSink.Entries.Where(e => e.Level >= LogLevel.Error).ToList();
            if (errors.Count > 0)
            {
                var errorMessages = string.Join(Environment.NewLine, errors.Select(e => $"[{e.Level}] {e.Category}: {e.Message}"));
                Assert.Fail($"Background jobs host logged unexpected Error/Critical logs:{Environment.NewLine}{errorMessages}");
            }

            // 8. Assertion 4: Information logs confirm startup of all 3 jobs and no errors on stop
            AssertStartupLogFor<ContractAutoCloseJob>(logSink);
            AssertStartupLogFor<ContractExpiryAlertJob>(logSink);
            AssertStartupLogFor<InvoiceReminderJob>(logSink);
            AssertShutdownLogFor<ContractAutoCloseJob>(logSink);
            AssertShutdownLogFor<ContractExpiryAlertJob>(logSink);
            AssertShutdownLogFor<InvoiceReminderJob>(logSink);
        }

        private static void AddTestLogging(IServiceCollection services, InMemoryLogSink logSink)
        {
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new InMemoryLogProvider(logSink));
                logging.SetMinimumLevel(LogLevel.Trace);
            });
        }

        private static void AssertNoErrorLogs(InMemoryLogSink logSink)
        {
            var errors = logSink.Entries.Where(e => e.Level >= LogLevel.Error).ToList();
            if (errors.Count > 0)
            {
                var errorMessages = string.Join(Environment.NewLine, errors.Select(e => $"[{e.Level}] {e.Category}: {e.Message}"));
                Assert.Fail($"Background jobs host logged unexpected Error/Critical logs:{Environment.NewLine}{errorMessages}");
            }
        }

        private static void AssertStartupLogFor<TJob>(InMemoryLogSink logSink)
        {
            AssertLogFor<TJob>(logSink, "đã khởi động");
        }

        private static void AssertShutdownLogFor<TJob>(InMemoryLogSink logSink)
        {
            AssertLogFor<TJob>(logSink, "đang dừng");
        }

        private static void AssertLogFor<TJob>(InMemoryLogSink logSink, string messageFragment)
        {
            Assert.Contains(logSink.Entries, entry =>
                entry.Level == LogLevel.Information &&
                entry.Category == typeof(TJob).FullName &&
                entry.Message.Contains(messageFragment, StringComparison.Ordinal));
        }

        private static async Task WaitForLogAsync<TJob>(
            InMemoryLogSink logSink,
            string messageFragment,
            CancellationToken cancellationToken)
        {
            while (!logSink.Entries.Any(entry =>
                entry.Category == typeof(TJob).FullName &&
                entry.Message.Contains(messageFragment, StringComparison.Ordinal)))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            }
        }

        private abstract class BlockingUseCase
        {
            private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task Entered => _entered.Task;

            protected async Task<int> BlockUntilCancelledAsync(CancellationToken cancellationToken)
            {
                _entered.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 0;
            }
        }

        private sealed class BlockingContractAutoCloseUseCase : BlockingUseCase, IContractAutoCloseUseCase
        {
            public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
                BlockUntilCancelledAsync(cancellationToken);
        }

        private sealed class BlockingContractExpiryAlertUseCase : BlockingUseCase, IContractExpiryAlertUseCase
        {
            public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
                BlockUntilCancelledAsync(cancellationToken);
        }

        private sealed class BlockingInvoiceReminderUseCase : BlockingUseCase, IInvoiceReminderUseCase
        {
            public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) =>
                BlockUntilCancelledAsync(cancellationToken);
        }

        private class ObservableContractAutoCloseUseCase : IContractAutoCloseUseCase
        {
            private readonly TaskCompletionSource<bool> _tcs;
            public ObservableContractAutoCloseUseCase(TaskCompletionSource<bool> tcs) => _tcs = tcs;

            public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
            {
                _tcs.TrySetResult(true);
                return Task.FromResult(0);
            }
        }

        private class ObservableContractExpiryAlertUseCase : IContractExpiryAlertUseCase
        {
            private readonly TaskCompletionSource<bool> _tcs;
            public ObservableContractExpiryAlertUseCase(TaskCompletionSource<bool> tcs) => _tcs = tcs;

            public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
            {
                _tcs.TrySetResult(true);
                return Task.FromResult(0);
            }
        }

        private class ObservableInvoiceReminderUseCase : IInvoiceReminderUseCase
        {
            private readonly TaskCompletionSource<bool> _tcs;
            public ObservableInvoiceReminderUseCase(TaskCompletionSource<bool> tcs) => _tcs = tcs;

            public Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
            {
                _tcs.TrySetResult(true);
                return Task.FromResult(0);
            }
        }

        private class FakeEmailService : IEmailService
        {
            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes) =>
                Task.FromResult((true, string.Empty));

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData) =>
                Task.FromResult((true, string.Empty));
        }

        private class FakeImageStorageService : IImageStorageService
        {
            public Task<string> UploadImageAsync(UploadFile file, string folderName) =>
                Task.FromResult("https://fake.storage/test.png");
        }

        private class InMemoryLogSink
        {
            private readonly ConcurrentBag<LogEntry> _entries = new();
            public void Add(LogLevel level, string category, string message) =>
                _entries.Add(new LogEntry(level, category, message));
            public IReadOnlyCollection<LogEntry> Entries => _entries.ToList();
        }

        private record LogEntry(LogLevel Level, string Category, string Message);

        private class InMemoryLogProvider : ILoggerProvider
        {
            private readonly InMemoryLogSink _sink;
            public InMemoryLogProvider(InMemoryLogSink sink) => _sink = sink;
            public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _sink);
            public void Dispose() { }
        }

        private class InMemoryLogger : ILogger
        {
            private readonly string _category;
            private readonly InMemoryLogSink _sink;
            public InMemoryLogger(string category, InMemoryLogSink sink)
            {
                _category = category;
                _sink = sink;
            }
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                if (exception != null)
                {
                    message += $" (Exception: {exception.Message})";
                }
                _sink.Add(logLevel, _category, message);
            }
        }
    }
}
