using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Telegram và email báo khách khi tiền giữ chỗ được ghi tự động (spec giữ chỗ §8).
    public class PaymentNotificationDispatcherReservationTests
    {
        private const string DefaultChat = "-100100";
        private const string BranchChat = "-100200";
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly PaymentTestHarness _h = new();
        private readonly FakeTelegramNotifier _telegram = new();
        private readonly RecordingReservationNoticeEmailService _email = new();
        private readonly TelegramOptions _telegramOptions = new()
        {
            Enabled = true,
            BotToken = "123456:test-token",
            DefaultChatId = DefaultChat,
            BranchChatIds = { [1] = BranchChat }
        };
        private readonly CapturingLogger<PaymentNotificationDispatcher> _log = new();

        private Task DispatchAsync(TransferOutcome outcome)
        {
            var dispatcher = new PaymentNotificationDispatcher(
                _h.Store, _telegram, _email, _telegramOptions, new OnlinePaymentOptions(), _log);
            return dispatcher.DispatchAsync(new PaymentNotificationWorkItem(outcome));
        }

        private static TransferOutcome Outcome(string? email = "khach@example.com") => new()
        {
            Kind = TransferOutcomeKind.Recorded,
            Transfer = new IncomingBankTransfer(92704, "MBBank", TransferTime, "0123456789", null, null, "TTPTGC5", "in", 1_000_000m, "FT26ABC", "TTPTGC5"),
            Code = "TTPTGC5",
            SoTienGhiNhan = 1_000_000m,
            TongTien = 1_000_000m,
            DaThu = 1_000_000m,
            ConLai = 0m,
            GiuCho = FakeReservationAutoPaymentStore.Recorded(email: email)
        };

        [Fact]
        public async Task Recorded_SendsTelegramToTheBranchChats_AndOneEmailToTheGuest()
        {
            await DispatchAsync(Outcome());

            Assert.Equal(new[] { DefaultChat, BranchChat }, _telegram.Messages.Select(m => m.ChatId).ToArray());
            Assert.All(_telegram.Messages, m => Assert.Contains("THU TIỀN GIỮ CHỖ — GIỮ CHỖ ĐÃ CÓ HIỆU LỰC", m.Text));
            var call = Assert.Single(_email.NoticeCalls);
            Assert.Equal("khach@example.com", call.ToEmail);
            Assert.Equal("Nguyễn Văn A", call.Data.TenNguoiNhan);
            Assert.Equal("Giữ chỗ phòng P101 đã có hiệu lực", call.Data.TieuDe);
            Assert.Empty(_log.Entries);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("khong-phai-email")]
        public async Task Recorded_WithoutAValidGuestEmail_SendsNoEmail_AndLogsAWarningWithoutTheAddress(string? email)
        {
            await DispatchAsync(Outcome(email));

            Assert.Empty(_email.NoticeCalls);
            Assert.Equal(2, _telegram.Messages.Count);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            if (!string.IsNullOrWhiteSpace(email))
            {
                Assert.DoesNotContain(email.Trim(), entry.Message);
            }
        }

        [Fact]
        public async Task Recorded_TelegramDisabled_StillSendsTheEmail()
        {
            _telegramOptions.Enabled = false;

            await DispatchAsync(Outcome());

            Assert.Empty(_telegram.Messages);
            Assert.Single(_email.NoticeCalls);
        }

        [Fact]
        public async Task Recorded_OneChatThrows_TheOtherChatAndTheEmailAreStillSent()
        {
            _telegram.ThrowChatIds.Add(DefaultChat);

            await DispatchAsync(Outcome());

            Assert.Equal(2, _telegram.Messages.Count);
            Assert.Single(_email.NoticeCalls);
            Assert.Single(_log.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task Recorded_EmailFailureOrException_OnlyLogsAWarning()
        {
            _email.Result = (false, "SMTP lỗi");
            await DispatchAsync(Outcome());
            _email.Throw = true;
            await DispatchAsync(Outcome());

            Assert.Equal(2, _log.Entries.Count(e => e.Level == LogLevel.Warning));
        }

        [Fact]
        public async Task Recorded_DoesNotLookUpAnInvoiceReceipt()
        {
            await DispatchAsync(Outcome());

            Assert.DoesNotContain(_log.Entries, e => e.Message.Contains("khoản thanh toán"));
            Assert.Empty(_h.Store.Ledger);
        }
    }
}
