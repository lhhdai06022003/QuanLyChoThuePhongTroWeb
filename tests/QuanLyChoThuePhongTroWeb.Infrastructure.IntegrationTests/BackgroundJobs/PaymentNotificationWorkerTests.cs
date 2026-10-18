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
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.PaymentNotifications;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    // Không dùng PostgreSqlFixture: host chạy thật với dispatcher giả, không gọi Telegram, SMTP hay DB
    // (spec thanh toán trực tuyến §6.1: worker luôn đăng ký, không phụ thuộc BackgroundJobs:Enabled).
    public class PaymentNotificationWorkerTests
    {
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);
        private static readonly string WorkerCategory = typeof(PaymentNotificationWorker).FullName!;

        private sealed class ScopeMarker
        {
        }

        private sealed class RecordingDispatcher : IPaymentNotificationDispatcher
        {
            private readonly ScopeMarker _marker;
            private readonly Shared _shared;

            public sealed class Shared
            {
                public ConcurrentQueue<(PaymentNotificationWorkItem Item, ScopeMarker Scope)> Received { get; } = new();
                public Func<PaymentNotificationWorkItem, Exception?>? Failure { get; set; }
                public SemaphoreSlim Signal { get; } = new(0);
                // Việc đang xử lý chờ mãi đến khi host dừng (mô phỏng gọi Telegram/SMTP đang treo).
                public bool BlockUntilCancelled { get; set; }
            }

            public RecordingDispatcher(ScopeMarker marker, Shared shared)
            {
                _marker = marker;
                _shared = shared;
            }

            public async Task DispatchAsync(PaymentNotificationWorkItem item, CancellationToken ct = default)
            {
                _shared.Received.Enqueue((item, _marker));
                _shared.Signal.Release();
                if (_shared.BlockUntilCancelled)
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }

                var failure = _shared.Failure?.Invoke(item);
                if (failure != null)
                {
                    throw failure;
                }
            }
        }

        private sealed class InMemoryLogSink
        {
            private readonly ConcurrentBag<(LogLevel Level, string Category, string Message)> _entries = new();
            public void Add(LogLevel level, string category, string message) => _entries.Add((level, category, message));
            public IReadOnlyList<(LogLevel Level, string Category, string Message)> Entries => _entries.ToList();
        }

        private sealed class InMemoryLogProvider : ILoggerProvider
        {
            private readonly InMemoryLogSink _sink;
            public InMemoryLogProvider(InMemoryLogSink sink) => _sink = sink;
            public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _sink);
            public void Dispose() { }
        }

        private sealed class InMemoryLogger : ILogger
        {
            private readonly string _category;
            private readonly InMemoryLogSink _sink;
            public InMemoryLogger(string category, InMemoryLogSink sink) { _category = category; _sink = sink; }
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _sink.Add(logLevel, _category, formatter(state, exception));
        }

        private static Dictionary<string, string?> BaseConfiguration(bool backgroundJobsEnabled) => new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused_test;Username=unused;Password=unused",
            ["BackgroundJobs:Enabled"] = backgroundJobsEnabled ? "true" : "false"
        };

        private static IHost BuildHost(
            RecordingDispatcher.Shared shared, InMemoryLogSink logSink, bool backgroundJobsEnabled = false, Dictionary<string, string?>? extraConfiguration = null)
        {
            return new HostBuilder()
                .ConfigureAppConfiguration((_, config) =>
                {
                    var values = BaseConfiguration(backgroundJobsEnabled);
                    foreach (var pair in extraConfiguration ?? new Dictionary<string, string?>())
                    {
                        values[pair.Key] = pair.Value;
                    }

                    config.AddInMemoryCollection(values);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration);
                    services.AddScoped<ScopeMarker>();
                    services.RemoveAll<IPaymentNotificationDispatcher>();
                    services.AddScoped<IPaymentNotificationDispatcher>(sp => new RecordingDispatcher(sp.GetRequiredService<ScopeMarker>(), shared));
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(new InMemoryLogProvider(logSink));
                        logging.SetMinimumLevel(LogLevel.Trace);
                    });
                })
                .Build();
        }

        private static PaymentNotificationWorkItem Item(int hoaDonId, int lichSuThanhToanId) => new(new TransferOutcome
        {
            Kind = TransferOutcomeKind.Recorded,
            Transfer = new IncomingBankTransfer(
                92704, "MBBank", TransferTime, "0123456789", null, null, "TTPTHD" + hoaDonId, "in", 1_000_000m, "FT26ABC", null),
            HoaDonId = hoaDonId,
            LichSuThanhToanId = lichSuThanhToanId
        });

        private static async Task WaitForAsync(SemaphoreSlim signal, int count)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            for (var i = 0; i < count; i++)
            {
                await signal.WaitAsync(timeout.Token);
            }
        }

        // ---------- Đăng ký ----------

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheWorker_IsRegistered_WhetherOrNotThePeriodicJobsAreEnabled(bool backgroundJobsEnabled)
        {
            using var host = BuildHost(new RecordingDispatcher.Shared(), new InMemoryLogSink(), backgroundJobsEnabled);

            var hostedServices = host.Services.GetServices<IHostedService>().ToList();

            Assert.Single(hostedServices, s => s is PaymentNotificationWorker);
            Assert.Equal(backgroundJobsEnabled ? 3 : 0, hostedServices.Count(s => s.GetType().Namespace == typeof(ContractAutoCloseJob).Namespace));
            Assert.Equal(backgroundJobsEnabled, hostedServices.Any(s => s is ContractAutoCloseJob));
            Assert.Equal(backgroundJobsEnabled, hostedServices.Any(s => s is ContractExpiryAlertJob));
            Assert.Equal(backgroundJobsEnabled, hostedServices.Any(s => s is InvoiceReminderJob));
        }

        [Fact]
        public void TheQueue_IsASingleSharedInstance_WhoseReaderTheWorkerConsumes()
        {
            using var host = BuildHost(new RecordingDispatcher.Shared(), new InMemoryLogSink());

            var asInterface = host.Services.GetRequiredService<IPaymentNotificationQueue>();
            var asConcrete = host.Services.GetRequiredService<ChannelPaymentNotificationQueue>();

            Assert.Same(asConcrete, asInterface);
            using var scope = host.Services.CreateScope();
            Assert.Same(asConcrete, scope.ServiceProvider.GetRequiredService<IPaymentNotificationQueue>());
        }

        [Fact]
        public void TheOptionsAndServices_AreRegisteredByAddInfrastructure()
        {
            using var host = BuildHost(new RecordingDispatcher.Shared(), new InMemoryLogSink(), extraConfiguration: new()
            {
                ["Telegram:Enabled"] = "true",
                ["Telegram:BotToken"] = "123456:token",
                ["Telegram:DefaultChatId"] = "-100100",
                ["Telegram:BranchChatIds:1"] = "-100200",
                ["OnlinePayment:PublicBaseUrl"] = "https://tro.test"
            });

            var telegram = host.Services.GetRequiredService<TelegramOptions>();
            var online = host.Services.GetRequiredService<OnlinePaymentOptions>();

            Assert.True(telegram.IsConfigured);
            Assert.Equal(new[] { "-100100", "-100200" }, telegram.ResolveChatIds(1));
            Assert.Equal("https://tro.test", online.PublicBaseUrl);
            Assert.IsType<QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments.MemoryCacheTransferAlertDeduplicator>(
                host.Services.GetRequiredService<ITransferAlertDeduplicator>());
            Assert.IsType<QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Telegram.TelegramBotNotifier>(
                host.Services.GetRequiredService<ITelegramNotifier>());
        }

        [Fact]
        public void DefaultConfiguration_LeavesTelegramOffAndHasNoPublicUrl()
        {
            using var host = BuildHost(new RecordingDispatcher.Shared(), new InMemoryLogSink());

            Assert.False(host.Services.GetRequiredService<TelegramOptions>().IsConfigured);
            Assert.Equal(string.Empty, host.Services.GetRequiredService<OnlinePaymentOptions>().PublicBaseUrl);
        }

        [Fact]
        public void EnabledTelegramWithoutAToken_FailsFast_WhenComposingTheHost()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                BuildHost(new RecordingDispatcher.Shared(), new InMemoryLogSink(), extraConfiguration: new()
                {
                    ["Telegram:Enabled"] = "true",
                    ["Telegram:DefaultChatId"] = "-100100"
                }));

            Assert.Contains("Telegram:BotToken", ex.Message);
        }

        [Fact]
        public void APublicUrlThatIsNotHttp_FailsFast_WhenComposingTheHost()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                BuildHost(new RecordingDispatcher.Shared(), new InMemoryLogSink(), extraConfiguration: new()
                {
                    ["OnlinePayment:PublicBaseUrl"] = "tro.test"
                }));

            Assert.Contains("OnlinePayment:PublicBaseUrl", ex.Message);
        }

        // ---------- Xử lý hàng đợi ----------

        [Fact]
        public async Task QueuedItems_AreDispatchedInOrder_EachInItsOwnScope()
        {
            var shared = new RecordingDispatcher.Shared();
            var logSink = new InMemoryLogSink();
            using var host = BuildHost(shared, logSink);
            await host.StartAsync();
            var queue = host.Services.GetRequiredService<IPaymentNotificationQueue>();
            var first = Item(7, 70);
            var second = Item(8, 80);

            Assert.True(await queue.EnqueueAsync(first));
            Assert.True(await queue.EnqueueAsync(second));
            await WaitForAsync(shared.Signal, 2);
            await host.StopAsync();

            var received = shared.Received.ToList();
            Assert.Equal(2, received.Count);
            Assert.Same(first, received[0].Item);
            Assert.Same(second, received[1].Item);
            Assert.NotSame(received[0].Scope, received[1].Scope);
            Assert.DoesNotContain(logSink.Entries, e => e.Category == WorkerCategory && e.Level >= LogLevel.Warning);
            Assert.DoesNotContain(logSink.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task ItemsQueuedBeforeTheHostStarts_AreDispatchedAfterwards()
        {
            var shared = new RecordingDispatcher.Shared();
            using var host = BuildHost(shared, new InMemoryLogSink());
            var queue = host.Services.GetRequiredService<IPaymentNotificationQueue>();
            await queue.EnqueueAsync(Item(7, 70));

            await host.StartAsync();
            await WaitForAsync(shared.Signal, 1);
            await host.StopAsync();

            Assert.Single(shared.Received);
        }

        [Fact]
        public async Task WhenOneItemThrows_AWarningNamesTheInvoiceAndEntry_AndTheNextItemIsStillDispatched()
        {
            var shared = new RecordingDispatcher.Shared
            {
                Failure = item => item.Outcome.HoaDonId == 7 ? new InvalidOperationException("SMTP down") : null
            };
            var logSink = new InMemoryLogSink();
            using var host = BuildHost(shared, logSink);
            await host.StartAsync();
            var queue = host.Services.GetRequiredService<IPaymentNotificationQueue>();

            await queue.EnqueueAsync(Item(7, 70));
            await queue.EnqueueAsync(Item(8, 80));
            await WaitForAsync(shared.Signal, 2);
            await host.StopAsync();

            Assert.Equal(new int?[] { 7, 8 }, shared.Received.Select(r => r.Item.Outcome.HoaDonId).ToArray());
            var warning = Assert.Single(logSink.Entries, e => e.Category == WorkerCategory && e.Level == LogLevel.Warning);
            Assert.Contains("hóa đơn 7", warning.Message);
            Assert.Contains("khoản 70", warning.Message);
            Assert.DoesNotContain(logSink.Entries, e => e.Level >= LogLevel.Error);
        }

        // ---------- Khởi động và dừng ----------

        [Fact]
        public async Task StoppingAnIdleHost_LogsNoErrors()
        {
            var logSink = new InMemoryLogSink();
            using var host = BuildHost(new RecordingDispatcher.Shared(), logSink);
            await host.StartAsync();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await host.StopAsync(timeout.Token);

            Assert.DoesNotContain(logSink.Entries, e => e.Level >= LogLevel.Error);
            Assert.DoesNotContain(logSink.Entries, e => e.Category == WorkerCategory && e.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task StoppingTheHostWhileAnItemIsBeingSent_LogsNoWarningOrError_AndStopsPromptly()
        {
            var shared = new RecordingDispatcher.Shared { BlockUntilCancelled = true };
            var logSink = new InMemoryLogSink();
            using var host = BuildHost(shared, logSink);
            await host.StartAsync();
            var queue = host.Services.GetRequiredService<IPaymentNotificationQueue>();
            await queue.EnqueueAsync(Item(7, 70));
            await queue.EnqueueAsync(Item(8, 80));
            await WaitForAsync(shared.Signal, 1);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await host.StopAsync(timeout.Token);

            Assert.Single(shared.Received);
            Assert.DoesNotContain(logSink.Entries, e => e.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task ATimeoutInsideTheDispatcher_WhileTheHostKeepsRunning_IsOnlyAWarning_AndTheNextItemIsSent()
        {
            var shared = new RecordingDispatcher.Shared
            {
                Failure = item => item.Outcome.HoaDonId == 7 ? new TaskCanceledException("HttpClient.Timeout") : null
            };
            var logSink = new InMemoryLogSink();
            using var host = BuildHost(shared, logSink);
            await host.StartAsync();
            var queue = host.Services.GetRequiredService<IPaymentNotificationQueue>();

            await queue.EnqueueAsync(Item(7, 70));
            await queue.EnqueueAsync(Item(8, 80));
            await WaitForAsync(shared.Signal, 2);
            await host.StopAsync();

            Assert.Equal(new int?[] { 7, 8 }, shared.Received.Select(r => r.Item.Outcome.HoaDonId).ToArray());
            Assert.Single(logSink.Entries, e => e.Category == WorkerCategory && e.Level == LogLevel.Warning);
            Assert.DoesNotContain(logSink.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task WithTelegramOff_LogsOneInformationLine_AtStartup()
        {
            var logSink = new InMemoryLogSink();
            using var host = BuildHost(new RecordingDispatcher.Shared(), logSink);

            await host.StartAsync();
            await host.StopAsync();

            var info = Assert.Single(logSink.Entries, e => e.Category == WorkerCategory);
            Assert.Equal(LogLevel.Information, info.Level);
            Assert.Contains("Telegram chưa bật hoặc thiếu BotToken/DefaultChatId", info.Message);
        }

        [Fact]
        public async Task WithTelegramOn_LogsNothingAtStartup()
        {
            var logSink = new InMemoryLogSink();
            using var host = BuildHost(new RecordingDispatcher.Shared(), logSink, extraConfiguration: new()
            {
                ["Telegram:Enabled"] = "true",
                ["Telegram:BotToken"] = "123456:token",
                ["Telegram:DefaultChatId"] = "-100100"
            });

            await host.StartAsync();
            await host.StopAsync();

            Assert.DoesNotContain(logSink.Entries, e => e.Category == WorkerCategory);
        }

        // ---------- Hàng đợi ----------

        [Fact]
        public async Task TheQueue_RefusesNewItems_WhenFull()
        {
            var queue = new ChannelPaymentNotificationQueue();

            for (var i = 0; i < 1000; i++)
            {
                Assert.True(await queue.EnqueueAsync(Item(7, i)));
            }

            Assert.False(await queue.EnqueueAsync(Item(7, 1000)));
            Assert.True(queue.Reader.TryRead(out _));
            Assert.True(await queue.EnqueueAsync(Item(7, 1001)));
        }
    }
}
