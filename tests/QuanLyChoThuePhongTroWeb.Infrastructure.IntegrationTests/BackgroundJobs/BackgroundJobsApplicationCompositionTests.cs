using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    public class BackgroundJobsApplicationCompositionTests
    {
        [Fact]
        public async Task EnabledBackgroundJobs_WithProductionApplicationComposition_ShouldExecuteRealUseCasesWithoutExternalSideEffects()
        {
            var autoCloseQueried = NewSignal();
            var expiryAlertQueried = NewSignal();
            var invoiceReminderQueried = NewSignal();
            var logSink = new InMemoryLogSink();
            var emailService = new RejectingEmailService();

            var hopDongStore = CreateProxy<IHopDongStore>((method, _) => method.Name switch
            {
                nameof(IHopDongStore.GetExpiredActiveContractsAsync) => SignalEmptyAsync<HopDong>(autoCloseQueried),
                nameof(IHopDongStore.GetActiveExpiringContractsAsync) => SignalEmptyAsync<HopDong>(expiryAlertQueried),
                _ => throw new InvalidOperationException($"Unexpected IHopDongStore call: {method.Name}")
            });

            var hoaDonStore = CreateProxy<IHoaDonStore>((method, _) => method.Name switch
            {
                nameof(IHoaDonStore.GetOverdueInvoicesAsync) => SignalEmptyAsync<HoaDon>(invoiceReminderQueried),
                _ => throw new InvalidOperationException($"Unexpected IHoaDonStore call: {method.Name}")
            });

            var unitOfWork = CreateProxy<IUnitOfWork>((method, _) =>
                throw new InvalidOperationException($"Unexpected IUnitOfWork call: {method.Name}"));

            var hostBuilder = new HostBuilder()
                .ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused_test;Username=unused;Password=unused",
                        ["BackgroundJobs:Enabled"] = "true",
                        ["AutoReminderSettings:Enabled"] = "true",
                        ["AutoReminderSettings:OverdueDays"] = "5",
                        ["ContractAlertSettings:Enabled"] = "true",
                        ["ContractAlertSettings:AlertDays:0"] = "30"
                    });
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration);
                    services.AddApplication();

                    services.RemoveAll<IHopDongStore>();
                    services.AddSingleton(hopDongStore);
                    services.RemoveAll<IHoaDonStore>();
                    services.AddSingleton(hoaDonStore);
                    services.RemoveAll<IUnitOfWork>();
                    services.AddSingleton(unitOfWork);
                    services.RemoveAll<IInvoiceReminderStateStore>();
                    services.AddSingleton<IInvoiceReminderStateStore, EmptyInvoiceReminderStateStore>();
                    services.RemoveAll<IContractExpiryAlertStateStore>();
                    services.AddSingleton<IContractExpiryAlertStateStore, EmptyContractExpiryAlertStateStore>();
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton<IEmailService>(emailService);

                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(new InMemoryLogProvider(logSink));
                        logging.SetMinimumLevel(LogLevel.Trace);
                    });
                });

            using var host = hostBuilder.Build();

            using (var scope = host.Services.CreateScope())
            {
                Assert.IsType<ContractAutoCloseUseCase>(scope.ServiceProvider.GetRequiredService<IContractAutoCloseUseCase>());
                Assert.IsType<ContractExpiryAlertUseCase>(scope.ServiceProvider.GetRequiredService<IContractExpiryAlertUseCase>());
                Assert.IsType<InvoiceReminderUseCase>(scope.ServiceProvider.GetRequiredService<IInvoiceReminderUseCase>());
            }

            await host.StartAsync();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Task.WhenAll(
                autoCloseQueried.Task.WaitAsync(timeout.Token),
                expiryAlertQueried.Task.WaitAsync(timeout.Token),
                invoiceReminderQueried.Task.WaitAsync(timeout.Token));

            await host.StopAsync(timeout.Token);

            Assert.Equal(0, emailService.CallCount);
            Assert.DoesNotContain(logSink.Entries, entry => entry.Level >= LogLevel.Error);
            AssertStartupLogFor<ContractAutoCloseJob>(logSink);
            AssertStartupLogFor<ContractExpiryAlertJob>(logSink);
            AssertStartupLogFor<InvoiceReminderJob>(logSink);
        }

        private static TaskCompletionSource<bool> NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static Task<IReadOnlyList<T>> SignalEmptyAsync<T>(TaskCompletionSource<bool> signal)
        {
            signal.TrySetResult(true);
            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
        }

        private static T CreateProxy<T>(Func<MethodInfo, object?[]?, object?> handler)
            where T : class
        {
            var instance = DispatchProxy.Create<T, StrictProxy<T>>();
            ((StrictProxy<T>)(object)instance).Handler = handler;
            return instance;
        }

        private static void AssertStartupLogFor<TJob>(InMemoryLogSink logSink)
        {
            Assert.Contains(logSink.Entries, entry =>
                entry.Level == LogLevel.Information &&
                entry.Category == typeof(TJob).FullName &&
                entry.Message.Contains("đã khởi động", StringComparison.Ordinal));
        }

        public class StrictProxy<T> : DispatchProxy
            where T : class
        {
            public Func<MethodInfo, object?[]?, object?> Handler { get; set; } =
                (_, _) => throw new InvalidOperationException("Proxy handler is not configured.");

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                return Handler(targetMethod ?? throw new InvalidOperationException("Missing target method."), args);
            }
        }

        private sealed class EmptyInvoiceReminderStateStore : IInvoiceReminderStateStore
        {
            private static readonly IReadOnlySet<int> Empty = new HashSet<int>();

            public Task<IReadOnlySet<int>> GetSentInvoiceIdsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(Empty);

            public Task<bool> HasBeenSentAsync(int hoaDonId, CancellationToken cancellationToken = default) =>
                Task.FromResult(false);

            public Task MarkAsSentAsync(int hoaDonId, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("No invoice should be marked in an empty-cycle composition test.");
        }

        private sealed class EmptyContractExpiryAlertStateStore : IContractExpiryAlertStateStore
        {
            private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

            public Task<IReadOnlySet<string>> GetSentAlertKeysAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(Empty);

            public Task<bool> HasBeenSentAsync(string alertKey, CancellationToken cancellationToken = default) =>
                Task.FromResult(false);

            public Task MarkAsSentAsync(string alertKey, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("No alert should be marked in an empty-cycle composition test.");
        }

        private sealed class RejectingEmailService : IEmailService
        {
            public int CallCount { get; private set; }

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(
                string toEmail,
                Application.Features.HoaDons.DTOs.HoaDonChiTietRes hoaDon,
                byte[] pdfBytes)
            {
                CallCount++;
                throw new InvalidOperationException("External email must not be called by this test.");
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(
                string toEmail,
                string tenNguoiNhan,
                Application.Features.Emails.DTOs.ContractExpiryAlertData alertData)
            {
                CallCount++;
                throw new InvalidOperationException("External email must not be called by this test.");
            }
        }

        private sealed class InMemoryLogSink
        {
            private readonly ConcurrentBag<LogEntry> _entries = new();

            public void Add(LogLevel level, string category, string message) =>
                _entries.Add(new LogEntry(level, category, message));

            public IReadOnlyCollection<LogEntry> Entries => _entries.ToList();
        }

        private sealed record LogEntry(LogLevel Level, string Category, string Message);

        private sealed class InMemoryLogProvider : ILoggerProvider
        {
            private readonly InMemoryLogSink _sink;

            public InMemoryLogProvider(InMemoryLogSink sink) => _sink = sink;

            public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _sink);

            public void Dispose()
            {
            }
        }

        private sealed class InMemoryLogger : ILogger
        {
            private readonly string _category;
            private readonly InMemoryLogSink _sink;

            public InMemoryLogger(string category, InMemoryLogSink sink)
            {
                _category = category;
                _sink = sink;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull =>
                null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
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
