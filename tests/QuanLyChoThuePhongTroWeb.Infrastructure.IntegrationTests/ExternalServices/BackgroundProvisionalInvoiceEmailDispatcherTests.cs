using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Emails;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    public class BackgroundProvisionalInvoiceEmailDispatcherTests
    {
        private sealed class ScopeProbe
        {
            public Guid ScopeId { get; } = Guid.NewGuid();
        }

        private sealed class GatedEmailService : IEmailService
        {
            private readonly ScopeProbe _probe;
            private readonly Gate _gate;

            public GatedEmailService(ScopeProbe probe, Gate gate)
            {
                _probe = probe;
                _gate = gate;
            }

            public async Task<(bool IsSuccess, string ErrorMessage)> SendProvisionalInvoiceEmailAsync(string toEmail, ProvisionalInvoiceEmailData data)
            {
                _gate.Attempts.Enqueue((toEmail, _probe.ScopeId));
                await _gate.Release.Task;
                if (toEmail.StartsWith("throw", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("SMTP lỗi");
                }

                return toEmail.StartsWith("fail", StringComparison.Ordinal) ? (false, "SMTP từ chối") : (true, string.Empty);
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
                => throw new NotSupportedException();

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
                => throw new NotSupportedException();
        }

        private sealed class Gate
        {
            public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public ConcurrentQueue<(string To, Guid ScopeId)> Attempts { get; } = new();
        }

        private sealed record Entry(LogLevel Level, string Message);

        private sealed class Sink : ILoggerProvider
        {
            public ConcurrentQueue<Entry> Entries { get; } = new();

            public ILogger CreateLogger(string categoryName) => new SinkLogger(Entries);

            public void Dispose()
            {
            }

            private sealed class SinkLogger : ILogger
            {
                private readonly ConcurrentQueue<Entry> _entries;

                public SinkLogger(ConcurrentQueue<Entry> entries) => _entries = entries;

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                    => _entries.Enqueue(new Entry(logLevel, formatter(state, exception)));
            }
        }

        private static PendingProvisionalEmail Pending(int id, string email) =>
            new(id, email, new ProvisionalInvoiceEmailData { MaHoaDon = $"HD-{id}" });

        [Fact]
        public async Task Enqueue_ReturnsBeforeSending_UsesOwnScope_AndTriesEveryEmailWithoutErrorLogs()
        {
            var gate = new Gate();
            var sink = new Sink();
            var services = new ServiceCollection();
            services.AddLogging(b => b.ClearProviders().AddProvider(sink).SetMinimumLevel(LogLevel.Trace));
            services.AddScoped<ScopeProbe>();
            services.AddSingleton(gate);
            services.AddScoped<IEmailService, GatedEmailService>();
            services.AddSingleton<IProvisionalInvoiceEmailDispatcher, BackgroundProvisionalInvoiceEmailDispatcher>();
            using var provider = services.BuildServiceProvider(validateScopes: true);

            using var callerScope = provider.CreateScope();
            var callerScopeId = callerScope.ServiceProvider.GetRequiredService<ScopeProbe>().ScopeId;
            var dispatcher = provider.GetRequiredService<IProvisionalInvoiceEmailDispatcher>();

            dispatcher.Enqueue(new[] { Pending(1, "throw@x.vn"), Pending(2, "fail@x.vn"), Pending(3, "ok@x.vn") });

            // Enqueue đã trả về trong khi email đầu tiên còn bị chặn ở cổng chờ.
            await WaitUntilAsync(() => gate.Attempts.Count >= 1);
            Assert.Single(gate.Attempts);
            Assert.False(gate.Release.Task.IsCompleted);

            gate.Release.SetResult(true);
            await WaitUntilAsync(() => gate.Attempts.Count == 3 && sink.Entries.Any(e => e.Message.Contains("Đã gửi nền")));

            Assert.Equal(new[] { "throw@x.vn", "fail@x.vn", "ok@x.vn" }, gate.Attempts.Select(a => a.To).ToArray());
            Assert.All(gate.Attempts, a => Assert.NotEqual(callerScopeId, a.ScopeId));
            Assert.Single(gate.Attempts.Select(a => a.ScopeId).Distinct());
            Assert.Equal(2, sink.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(sink.Entries, e => e.Level >= LogLevel.Error);
            Assert.Contains(sink.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("1/3"));
        }

        [Fact]
        public void Enqueue_NullOrEmpty_DoesNothing()
        {
            var sink = new Sink();
            var services = new ServiceCollection();
            services.AddLogging(b => b.ClearProviders().AddProvider(sink));
            services.AddSingleton<IProvisionalInvoiceEmailDispatcher, BackgroundProvisionalInvoiceEmailDispatcher>();
            using var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<IProvisionalInvoiceEmailDispatcher>();

            dispatcher.Enqueue(null!);
            dispatcher.Enqueue(Array.Empty<PendingProvisionalEmail>());

            Assert.Empty(sink.Entries);
        }

        [Fact]
        public async Task Enqueue_WhenEmailServiceCannotBeResolved_LogsWarningOnly()
        {
            var sink = new Sink();
            var services = new ServiceCollection();
            services.AddLogging(b => b.ClearProviders().AddProvider(sink));
            services.AddSingleton<IProvisionalInvoiceEmailDispatcher, BackgroundProvisionalInvoiceEmailDispatcher>();
            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IProvisionalInvoiceEmailDispatcher>().Enqueue(new[] { Pending(1, "a@x.vn") });

            await WaitUntilAsync(() => sink.Entries.Any(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(sink.Entries, e => e.Level >= LogLevel.Error);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!condition())
            {
                await Task.Delay(10, cts.Token);
            }
        }
    }
}
