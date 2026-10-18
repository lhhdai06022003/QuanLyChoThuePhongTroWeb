using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Gửi Telegram (chat chung + chat chi nhánh) và email biên lai cho một việc trong hàng đợi (spec thanh toán trực tuyến §6.2, §6.3).
    public class PaymentNotificationDispatcherTests
    {
        private const string DefaultChat = "-100100";
        private const string BranchChat = "-100200";
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly PaymentTestHarness _h = new();
        private readonly FakeTelegramNotifier _telegram = new();
        private readonly RecordingPaymentReceiptEmailService _email = new();
        private readonly TelegramOptions _telegramOptions = new()
        {
            Enabled = true,
            BotToken = "123456:test-token",
            DefaultChatId = DefaultChat,
            BranchChatIds = { [PaymentTestHarness.Branch] = BranchChat }
        };
        private readonly OnlinePaymentOptions _onlineOptions = new() { PublicBaseUrl = "https://tro.test" };
        private readonly CapturingLogger<PaymentNotificationDispatcher> _log = new();

        private Task DispatchAsync(TransferOutcome outcome)
        {
            var dispatcher = new PaymentNotificationDispatcher(_h.Store, _telegram, _email, _telegramOptions, _onlineOptions, _log);
            return dispatcher.DispatchAsync(new PaymentNotificationWorkItem(outcome));
        }

        private static IncomingBankTransfer Transfer(decimal amount = 2_000_000m, string? reference = "FT26ABC", string? content = "TTPTHD7") => new(
            92704, "MBBank", TransferTime, "0123456789", null, null, content, "in", amount, reference, content);

        // Hóa đơn 7 (2.000.000đ) đã gửi, kèm một dòng sổ SePay vừa ghi.
        private TransferOutcome RecordSePayRow(decimal amount = 2_000_000m, string reference = "FT26ABC")
        {
            if (!_h.Store.Invoices.ContainsKey(7))
            {
                _h.AddSentInvoice();
            }

            var ledger = _h.AddSePayLedger(7, amount, reference, TransferTime);
            return RecordedOutcome(ledger, Transfer(amount, reference));
        }

        private TransferOutcome RecordedOutcome(LichSuThanhToan ledger, IncomingBankTransfer transfer)
        {
            var daThu = _h.Store.Ledger.Where(l => l.HoaDonId == 7 && !l.IsDeleted).Sum(l => l.SoTienThanhToan);
            return new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded,
                Transfer = transfer,
                Code = "TTPTHD7",
                HoaDonId = 7,
                MaHoaDon = "HD-7",
                LichSuThanhToanId = ledger.LichSuThanhToanId,
                TenantUserId = PaymentTestHarness.TenantUserId,
                SoTienGhiNhan = ledger.SoTienThanhToan,
                TongTien = 2_000_000m,
                DaThu = daThu,
                ConLai = 2_000_000m - daThu,
                TrangThaiThanhToan = "Đã thanh toán"
            };
        }

        private static TransferOutcome AlertOutcome(TransferOutcomeKind kind, int? hoaDonId = null, string? maHoaDon = null, string? lyDo = null) => new()
        {
            Kind = kind,
            Transfer = Transfer(content: "chuyen tien tro"),
            Code = kind == TransferOutcomeKind.Unmatched ? null : "TTPTHD7",
            HoaDonId = hoaDonId,
            MaHoaDon = maHoaDon,
            LyDo = lyDo
        };

        // ---------- Recorded ----------

        [Fact]
        public async Task Recorded_SendsTelegramToTheDefaultAndBranchChat_AndTheReceiptToTheTenant()
        {
            var outcome = RecordSePayRow();

            await DispatchAsync(outcome);

            Assert.Equal(new[] { DefaultChat, BranchChat }, _telegram.Messages.Select(m => m.ChatId).ToArray());
            Assert.All(_telegram.Messages, m => Assert.Contains("THU TIỀN TRỌ THÀNH CÔNG", m.Text));

            var call = Assert.Single(_email.ReceiptCalls);
            Assert.Equal("khach@test.vn", call.ToEmail);
            Assert.Equal(7, call.Data.HoaDonId);
            Assert.Equal(outcome.LichSuThanhToanId, call.Data.LichSuThanhToanId);
            Assert.Equal("HD-7", call.Data.MaHoaDon);
            Assert.Equal(1, call.Data.DotThu);
            Assert.True(call.Data.DaThuDu);
            Assert.Equal(0m, call.Data.ConNo);
            Assert.Equal("FT26ABC", call.Data.MaThamChieu);
            Assert.Equal($"https://tro.test/KhachThue/HoaDon/BienLai/{outcome.LichSuThanhToanId}", call.Data.LinkXemBienLai);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Recorded_SecondInstalment_CarriesTheInstalmentNumberAndTheRunningTotal()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 1_200_000m, "FT-A", TransferTime.AddDays(-1));
            var second = _h.AddSePayLedger(7, 800_000m, "FT-B", TransferTime);

            await DispatchAsync(RecordedOutcome(second, Transfer(800_000m, "FT-B")));

            var data = Assert.Single(_email.ReceiptCalls).Data;
            Assert.Equal(2, data.DotThu);
            Assert.Equal(800_000m, data.SoTienLanNay);
            Assert.Equal(2_000_000m, data.LuyKeDaThanhToan);
            Assert.Equal(0m, data.ConNo);
            Assert.True(data.DaThuDu);
        }

        [Fact]
        public async Task Recorded_PartialPayment_SendsThePartialMessage_AndAnInstalmentReceipt()
        {
            var outcome = RecordSePayRow(amount: 1_500_000m);

            await DispatchAsync(outcome);

            Assert.All(_telegram.Messages, m => Assert.Contains("ĐÓNG MỘT PHẦN", m.Text));
            var data = Assert.Single(_email.ReceiptCalls).Data;
            Assert.False(data.DaThuDu);
            Assert.Equal(500_000m, data.ConNo);
            Assert.Equal(1, data.DotThu);
        }

        [Fact]
        public async Task Recorded_TelegramDisabled_SendsNoTelegram_ButStillEmailsTheReceipt()
        {
            _telegramOptions.Enabled = false;

            await DispatchAsync(RecordSePayRow());

            Assert.Empty(_telegram.Messages);
            Assert.Single(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Recorded_BranchWithoutItsOwnChat_SendsToTheDefaultChatOnly()
        {
            _telegramOptions.BranchChatIds.Clear();

            await DispatchAsync(RecordSePayRow());

            Assert.Equal(DefaultChat, Assert.Single(_telegram.Messages).ChatId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("khong-phai-email")]
        public async Task Recorded_WithoutAValidTenantEmail_LogsAWarning_AndSendsNoEmail(string? emailAddress)
        {
            var outcome = RecordSePayRow();
            _h.Store.Meta[7] = _h.Store.Meta[7] with { EmailKhachThue = emailAddress };

            await DispatchAsync(outcome);

            Assert.Empty(_email.ReceiptCalls);
            Assert.Equal(2, _telegram.Messages.Count);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("email hợp lệ", entry.Message);
            Assert.Contains("7", entry.Message);
            if (!string.IsNullOrWhiteSpace(emailAddress))
            {
                Assert.DoesNotContain(emailAddress, entry.Message);
            }
        }

        [Fact]
        public async Task Recorded_WhenTheEmailFails_LogsAWarningWithInvoiceAndEntryIds_AndTelegramIsUnaffected()
        {
            var outcome = RecordSePayRow();
            _email.Result = (false, "Không gửi được email biên lai.");

            await DispatchAsync(outcome);

            Assert.Equal(2, _telegram.Messages.Count);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("email biên lai", entry.Message);
            Assert.Contains($"hóa đơn 7, khoản {outcome.LichSuThanhToanId}", entry.Message);
            Assert.DoesNotContain("khach@test.vn", entry.Message);
        }

        [Fact]
        public async Task Recorded_WhenTheEmailThrows_LogsAWarningWithInvoiceAndEntryIds_WithoutThrowing()
        {
            var outcome = RecordSePayRow();
            _email.Throw = true;

            await DispatchAsync(outcome);

            Assert.Equal(2, _telegram.Messages.Count);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains($"hóa đơn 7, khoản {outcome.LichSuThanhToanId}", entry.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Recorded_WhenOneChatFails_TheOtherChatAndTheEmailAreStillSent(bool throws)
        {
            var outcome = RecordSePayRow();
            (throws ? _telegram.ThrowChatIds : _telegram.FailChatIds).Add(DefaultChat);

            await DispatchAsync(outcome);

            Assert.Equal(new[] { DefaultChat, BranchChat }, _telegram.Messages.Select(m => m.ChatId).ToArray());
            Assert.Single(_email.ReceiptCalls);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains($"hóa đơn 7, khoản {outcome.LichSuThanhToanId}", entry.Message);
        }

        [Fact]
        public async Task Recorded_ManualLedgerRow_SendsNoReceiptEmail()
        {
            _h.AddSentInvoice();
            var manual = _h.AddLedger(7, 2_000_000m, "GD-TAY");

            await DispatchAsync(RecordedOutcome(manual, Transfer()));

            Assert.Empty(_email.ReceiptCalls);
            Assert.Equal(2, _telegram.Messages.Count);
        }

        [Fact]
        public async Task Recorded_VoidedLedgerRow_SendsNoReceiptEmail()
        {
            _h.AddSentInvoice();
            var voided = _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", TransferTime, isDeleted: true);

            await DispatchAsync(RecordedOutcome(voided, Transfer()));

            Assert.Empty(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Recorded_WhenTheLedgerRowCannotBeRead_LogsAWarning_AndSendsNothing()
        {
            _h.AddSentInvoice();
            var outcome = new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded,
                Transfer = Transfer(),
                HoaDonId = 7,
                LichSuThanhToanId = 999_999
            };

            await DispatchAsync(outcome);

            Assert.Empty(_telegram.Messages);
            Assert.Empty(_email.ReceiptCalls);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("999999", entry.Message);
        }

        [Fact]
        public async Task Recorded_WithoutAPublicBaseUrl_StillEmailsTheReceipt_WithoutALink_AndWithoutAWarning()
        {
            _onlineOptions.PublicBaseUrl = string.Empty;

            await DispatchAsync(RecordSePayRow());

            Assert.Null(Assert.Single(_email.ReceiptCalls).Data.LinkXemBienLai);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Recorded_ReferenceCodeThatLooksLikeMarkup_IsEscapedInTelegram_ButNotInTheEmailData()
        {
            var outcome = RecordSePayRow(reference: "FT<b>&1");

            await DispatchAsync(outcome);

            Assert.All(_telegram.Messages, m => Assert.Contains("Mã GD: FT&lt;b&gt;&amp;1", m.Text));
            Assert.Equal("FT<b>&1", Assert.Single(_email.ReceiptCalls).Data.MaThamChieu);
        }

        // ---------- Cảnh báo ----------

        [Theory]
        [InlineData(TransferOutcomeKind.Unmatched)]
        [InlineData(TransferOutcomeKind.WrongAccount)]
        public async Task Alert_WithoutAnInvoice_GoesToTheDefaultChatOnly_AndNeverEmails(TransferOutcomeKind kind)
        {
            await DispatchAsync(AlertOutcome(kind));

            var message = Assert.Single(_telegram.Messages);
            Assert.Equal(DefaultChat, message.ChatId);
            Assert.Contains("CHUYỂN KHOẢN CẦN XỬ LÝ TAY", message.Text);
            Assert.Empty(_email.ReceiptCalls);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Alert_AlreadyPaid_WithAnInvoice_GoesToTheDefaultAndBranchChat()
        {
            _h.AddSentInvoice();

            await DispatchAsync(AlertOutcome(TransferOutcomeKind.AlreadyPaid, hoaDonId: 7, maHoaDon: "HD-7"));

            Assert.Equal(new[] { DefaultChat, BranchChat }, _telegram.Messages.Select(m => m.ChatId).ToArray());
            Assert.All(_telegram.Messages, m => Assert.Contains("cho hóa đơn HD-7 đã trả đủ", m.Text));
            Assert.Empty(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Alert_InvoiceNotPayable_ForAMissingInvoice_GoesToTheDefaultChatOnly()
        {
            await DispatchAsync(AlertOutcome(TransferOutcomeKind.InvoiceNotPayable, hoaDonId: 99, lyDo: "Không tìm thấy hóa đơn"));

            var message = Assert.Single(_telegram.Messages);
            Assert.Equal(DefaultChat, message.ChatId);
            Assert.Contains("Hóa đơn #HD99 không nhận tiền được: Không tìm thấy hóa đơn", message.Text);
        }

        [Fact]
        public async Task Alert_InvoiceNotPayable_ForACancelledInvoice_NamesTheReplacementInvoice()
        {
            var cancelled = _h.AddSentInvoice();
            cancelled.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            cancelled.IsDeleted = true;
            var replacement = _h.AddSentInvoice(hoaDonId: 8);
            replacement.HopDongId = cancelled.HopDongId;
            replacement.MaHoaDon = "HD-7-R1";

            await DispatchAsync(AlertOutcome(TransferOutcomeKind.InvoiceNotPayable, hoaDonId: 7, maHoaDon: "HD-7", lyDo: "Hóa đơn đã hủy"));

            Assert.Equal(2, _telegram.Messages.Count);
            Assert.All(_telegram.Messages, m =>
                Assert.Contains("Hóa đơn HD-7 không nhận tiền được: Hóa đơn đã hủy; hóa đơn thay thế: HD-7-R1 (#HD8)", m.Text));
        }

        [Theory]
        [InlineData(TransferOutcomeKind.Unmatched)]
        [InlineData(TransferOutcomeKind.AlreadyPaid)]
        public async Task Alert_TelegramDisabled_SendsNothing(TransferOutcomeKind kind)
        {
            _telegramOptions.Enabled = false;
            _h.AddSentInvoice();

            await DispatchAsync(AlertOutcome(kind, hoaDonId: 7, maHoaDon: "HD-7"));

            Assert.Empty(_telegram.Messages);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Alert_WhenAChatFails_LogsAWarning_AndTheOtherChatIsStillSent()
        {
            _h.AddSentInvoice();
            _telegram.ThrowChatIds.Add(DefaultChat);

            await DispatchAsync(AlertOutcome(TransferOutcomeKind.AlreadyPaid, hoaDonId: 7, maHoaDon: "HD-7"));

            Assert.Equal(2, _telegram.Messages.Count);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
        }

        // ---------- Không làm gì ----------

        [Theory]
        [InlineData(TransferOutcomeKind.Duplicate)]
        [InlineData(TransferOutcomeKind.Ignored)]
        [InlineData(TransferOutcomeKind.Failed)]
        public async Task OtherKinds_DoNothing(TransferOutcomeKind kind)
        {
            _h.AddSentInvoice();

            await DispatchAsync(AlertOutcome(kind, hoaDonId: 7, maHoaDon: "HD-7"));

            Assert.Empty(_telegram.Messages);
            Assert.Empty(_email.ReceiptCalls);
            Assert.Empty(_log.Entries);
        }

        // ---------- Chat chung và chat chi nhánh ----------

        [Fact]
        public async Task Recorded_BranchChatSameAsTheDefaultChat_IsSentOnlyOnce()
        {
            _telegramOptions.BranchChatIds[PaymentTestHarness.Branch] = $" {DefaultChat} ";

            await DispatchAsync(RecordSePayRow());

            Assert.Equal(DefaultChat, Assert.Single(_telegram.Messages).ChatId);
        }

        [Fact]
        public async Task Recorded_AnotherBranchsChat_IsNeverUsed()
        {
            _telegramOptions.BranchChatIds.Clear();
            _telegramOptions.BranchChatIds[PaymentTestHarness.OtherBranch] = "-100999";

            await DispatchAsync(RecordSePayRow());

            Assert.Equal(DefaultChat, Assert.Single(_telegram.Messages).ChatId);
        }

        [Fact]
        public async Task Recorded_TheSameTextGoesToEveryChat()
        {
            await DispatchAsync(RecordSePayRow());

            Assert.Equal(2, _telegram.Messages.Count);
            Assert.Single(_telegram.Messages.Select(m => m.Text).Distinct());
        }

        [Fact]
        public async Task Alert_BranchChatSameAsTheDefaultChat_IsSentOnlyOnce()
        {
            _telegramOptions.BranchChatIds[PaymentTestHarness.Branch] = DefaultChat;
            _h.AddSentInvoice();

            await DispatchAsync(AlertOutcome(TransferOutcomeKind.AlreadyPaid, hoaDonId: 7, maHoaDon: "HD-7"));

            Assert.Equal(DefaultChat, Assert.Single(_telegram.Messages).ChatId);
        }

        [Fact]
        public async Task Alert_WrongAccountAndUnmatched_GoOnlyToTheDefaultChat_EvenWhenABranchChatExists()
        {
            // Hai loại này không xác định được chi nhánh: chỉ chat chung (spec §6.2).
            _h.AddSentInvoice();

            await DispatchAsync(AlertOutcome(TransferOutcomeKind.Unmatched));
            await DispatchAsync(AlertOutcome(TransferOutcomeKind.WrongAccount));

            Assert.Equal(new[] { DefaultChat, DefaultChat }, _telegram.Messages.Select(m => m.ChatId).ToArray());
        }

        [Fact]
        public async Task Alert_TextIsEscaped_WhenTheBankContentLooksLikeMarkup()
        {
            var outcome = AlertOutcome(TransferOutcomeKind.Unmatched) with
            {
                Transfer = Transfer(content: "<b>chuyen</b> & tien")
            };

            await DispatchAsync(outcome);

            var text = Assert.Single(_telegram.Messages).Text;
            Assert.Contains("Nội dung: &lt;b&gt;chuyen&lt;/b&gt; &amp; tien", text);
            Assert.DoesNotContain("<b>", text);
        }

        // ---------- Huỷ khi dừng host ----------

        private sealed class CancellationAwareTelegram : ITelegramNotifier
        {
            public int Calls { get; private set; }

            public Task<(bool IsSuccess, string ErrorMessage)> SendMessageAsync(string chatId, string htmlText, CancellationToken ct = default)
            {
                Calls++;
                ct.ThrowIfCancellationRequested();
                return Task.FromResult((true, string.Empty));
            }
        }

        [Fact]
        public async Task WhenTheHostIsStopping_TheCancellationPropagates_AndNoWarningIsLogged()
        {
            var outcome = RecordSePayRow();
            var telegram = new CancellationAwareTelegram();
            var dispatcher = new PaymentNotificationDispatcher(_h.Store, telegram, _email, _telegramOptions, _onlineOptions, _log);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                dispatcher.DispatchAsync(new PaymentNotificationWorkItem(outcome), cts.Token));

            Assert.Equal(1, telegram.Calls);
            Assert.Empty(_email.ReceiptCalls);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task ATimeoutThatIsNotAShutdown_IsOnlyAWarning_AndTheOtherChatIsStillSent()
        {
            var outcome = RecordSePayRow();
            var telegram = new TimeoutOnFirstChatTelegram();
            var dispatcher = new PaymentNotificationDispatcher(_h.Store, telegram, _email, _telegramOptions, _onlineOptions, _log);

            await dispatcher.DispatchAsync(new PaymentNotificationWorkItem(outcome));

            Assert.Equal(new[] { DefaultChat, BranchChat }, telegram.Chats.ToArray());
            Assert.Single(_email.ReceiptCalls);
            Assert.Equal(LogLevel.Warning, Assert.Single(_log.Entries).Level);
        }

        private sealed class TimeoutOnFirstChatTelegram : ITelegramNotifier
        {
            public List<string> Chats { get; } = new();

            public Task<(bool IsSuccess, string ErrorMessage)> SendMessageAsync(string chatId, string htmlText, CancellationToken ct = default)
            {
                Chats.Add(chatId);
                if (Chats.Count == 1)
                {
                    throw new TaskCanceledException("HttpClient.Timeout");
                }

                return Task.FromResult((true, string.Empty));
            }
        }

        // ---------- Email biên lai ----------

        [Fact]
        public async Task Recorded_ReceiptEmailIsSentToTheTrimmedAddress()
        {
            var outcome = RecordSePayRow();
            _h.Store.Meta[7] = _h.Store.Meta[7] with { EmailKhachThue = "  khach@test.vn  " };

            await DispatchAsync(outcome);

            Assert.Equal("khach@test.vn", Assert.Single(_email.ReceiptCalls).ToEmail);
        }

        [Fact]
        public async Task Recorded_OverpaidRow_ReceiptCarriesTheSurplusFromTheLedgerNote()
        {
            _h.AddSentInvoice();
            var ledger = _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", TransferTime);
            ledger.GhiChu = "[TIEN-THUA:300000] [SEPAY] MBBank · REF FT26ABC · ND: TTPTHD7";
            var outcome = RecordedOutcome(ledger, Transfer(2_300_000m)) with { TienThua = 300_000m };

            await DispatchAsync(outcome);

            var data = Assert.Single(_email.ReceiptCalls).Data;
            Assert.Equal(300_000m, data.TienThua);
            Assert.True(data.DaThuDu);
            Assert.All(_telegram.Messages, m => Assert.Contains("Khách đóng dư +300.000 VNĐ", m.Text));
        }

        [Fact]
        public async Task Recorded_ThirdInstalment_IgnoresAVoidedRowInTheRunningTotal()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 300_000m, "FT-A", TransferTime.AddDays(-2));
            _h.AddSePayLedger(7, 999_999m, "FT-HUY", TransferTime.AddDays(-1), isDeleted: true);
            _h.AddSePayLedger(7, 700_000m, "FT-B", TransferTime.AddDays(-0.5));
            var third = _h.AddSePayLedger(7, 500_000m, "FT-C", TransferTime);

            await DispatchAsync(RecordedOutcome(third, Transfer(500_000m, "FT-C")));

            var data = Assert.Single(_email.ReceiptCalls).Data;
            Assert.Equal(3, data.DotThu);
            Assert.Equal(1_500_000m, data.LuyKeDaThanhToan);
            Assert.Equal(500_000m, data.ConNo);
            Assert.False(data.DaThuDu);
        }

        [Fact]
        public async Task Logs_NeverContainTheBotTokenOrTheTransferText()
        {
            var outcome = RecordSePayRow();
            _telegram.FailChatIds.Add(DefaultChat);
            _telegram.ThrowChatIds.Add(BranchChat);
            _email.Throw = true;

            await DispatchAsync(outcome);

            Assert.NotEmpty(_log.Entries);
            Assert.All(_log.Entries, e =>
            {
                Assert.DoesNotContain("test-token", e.Message);
                Assert.DoesNotContain("TTPTHD7", e.Message);
                Assert.DoesNotContain("khach@test.vn", e.Message);
            });
        }
    }
}
