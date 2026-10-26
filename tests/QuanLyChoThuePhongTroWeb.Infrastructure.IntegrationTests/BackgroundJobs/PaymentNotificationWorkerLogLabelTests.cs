using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.PaymentNotifications;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    // Q1 (VNPay phần 2): log lỗi của PaymentNotificationWorker ghi đúng kênh và khóa chống trùng; kênh VNPay không còn
    // ra "SePay 0", kênh SePay vẫn có SePayId. Không dùng DB: worker chạy với dispatcher giả luôn ném lỗi.
    public class PaymentNotificationWorkerLogLabelTests
    {
        private static readonly DateTime PayTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);
        private const string Content = "NOI-DUNG-CHUYEN-KHOAN-BI-MAT";
        private const string Description = "MO-TA-BI-MAT";

        private sealed class ThrowingDispatcher(SemaphoreSlim signal) : IPaymentNotificationDispatcher
        {
            public Task DispatchAsync(PaymentNotificationWorkItem item, CancellationToken ct = default)
            {
                signal.Release();
                throw new InvalidOperationException("SMTP down");
            }
        }

        private sealed class CapturingLogger : ILogger<PaymentNotificationWorker>
        {
            public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Enqueue((logLevel, formatter(state, exception)));
        }

        private static IncomingBankTransfer SePayTransfer() => new(
            92704, "MBBank", PayTime, "0123456789", null, null, Content, "in", 2_000_000m, "FT26ABC", Description);

        private static IncomingBankTransfer VnPayTransfer() => new(
            0, "NCB", PayTime, null, null, null, Content, "in", 2_000_000m, "14123456", Description)
        {
            Kenh = OnlinePaymentChannel.VnPay,
            CardType = "ATM"
        };

        private static async Task<List<(LogLevel Level, string Message)>> RunAsync(IncomingBankTransfer transfer)
        {
            var signal = new SemaphoreSlim(0);
            var services = new ServiceCollection();
            services.AddScoped<IPaymentNotificationDispatcher>(_ => new ThrowingDispatcher(signal));
            using var provider = services.BuildServiceProvider();
            var queue = new ChannelPaymentNotificationQueue();
            var logger = new CapturingLogger();
            var worker = new PaymentNotificationWorker(queue, provider.GetRequiredService<IServiceScopeFactory>(), new TelegramOptions(), logger);

            await worker.StartAsync(CancellationToken.None);
            Assert.True(await queue.EnqueueAsync(new PaymentNotificationWorkItem(new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded,
                Transfer = transfer,
                HoaDonId = 7,
                LichSuThanhToanId = 77
            })));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await signal.WaitAsync(timeout.Token);
            // Chờ worker ghi log rồi mới dừng (log nằm ngay sau lúc dispatcher ném lỗi).
            for (var i = 0; i < 100 && !logger.Entries.Any(e => e.Level == LogLevel.Warning); i++)
            {
                await Task.Delay(50, timeout.Token);
            }

            await worker.StopAsync(timeout.Token);
            return logger.Entries.ToList();
        }

        [Fact]
        public async Task VnPayItem_WhoseSendingFails_IsLoggedWithTheVnPayKey_NotSePayZero()
        {
            var entries = await RunAsync(VnPayTransfer());

            var warning = Assert.Single(entries, e => e.Level == LogLevel.Warning);
            Assert.Contains("VNPay VNPAY-14123456", warning.Message);
            Assert.DoesNotContain("SePay 0", warning.Message);
            Assert.Contains("hóa đơn 7", warning.Message);
            Assert.Contains("khoản 77", warning.Message);
            Assert.DoesNotContain(Content, warning.Message);
            Assert.DoesNotContain(Description, warning.Message);
        }

        [Fact]
        public async Task SePayItem_WhoseSendingFails_IsStillLoggedWithTheSePayIdAndKey()
        {
            var entries = await RunAsync(SePayTransfer());

            var warning = Assert.Single(entries, e => e.Level == LogLevel.Warning);
            Assert.Contains("SePay 92704 (SEPAY-FT26ABC)", warning.Message);
            Assert.Contains("hóa đơn 7", warning.Message);
            Assert.Contains("khoản 77", warning.Message);
            Assert.DoesNotContain(Content, warning.Message);
            Assert.DoesNotContain(Description, warning.Message);
        }
    }
}
