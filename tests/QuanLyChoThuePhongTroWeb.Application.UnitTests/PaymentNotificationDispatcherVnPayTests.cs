using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Gửi Telegram và email biên lai cho kết quả của kênh VNPay (spec VNPay §7.1, §7.2).
    public class PaymentNotificationDispatcherVnPayTests
    {
        private const string DefaultChat = "-100100";
        private const string BranchChat = "-100200";
        private static readonly DateTime PayTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

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

        private static IncomingBankTransfer Transfer(decimal amount = 2_000_000m) => new(
            0, "NCB", PayTime, null, null, null, "Thanh toan hoa don HD-7", "in", amount, "14123456", "HD7_1")
        {
            Kenh = OnlinePaymentChannel.VnPay,
            CardType = "ATM"
        };

        private TransferOutcome RecordVnPayRow(decimal amount = 2_000_000m)
        {
            _h.AddSentInvoice();
            var ledger = _h.AddVnPayLedger(7, amount, "14123456", PayTime);
            return new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded,
                Transfer = Transfer(amount),
                Code = "TTPTHD7",
                HoaDonId = 7,
                MaHoaDon = "HD-7",
                LichSuThanhToanId = ledger.LichSuThanhToanId,
                TenantUserId = PaymentTestHarness.TenantUserId,
                SoTienGhiNhan = ledger.SoTienThanhToan,
                TongTien = 2_000_000m,
                DaThu = amount,
                ConLai = 2_000_000m - amount,
                TrangThaiThanhToan = "Đã thanh toán"
            };
        }

        [Fact]
        public async Task Recorded_SendsTheVnPayTelegram_AndTheReceiptWithTheTransactionNumber()
        {
            var outcome = RecordVnPayRow();

            await DispatchAsync(outcome);

            Assert.Equal(new[] { DefaultChat, BranchChat }, _telegram.Messages.Select(m => m.ChatId).ToArray());
            Assert.All(_telegram.Messages, m =>
            {
                Assert.Contains("THU TIỀN TRỌ THÀNH CÔNG", m.Text);
                Assert.Contains("Kênh: Thẻ ATM / Visa / MasterCard (VNPay, NCB)", m.Text);
                Assert.Contains("Mã GD: 14123456", m.Text);
            });

            var call = Assert.Single(_email.ReceiptCalls);
            Assert.Equal("khach@test.vn", call.ToEmail);
            Assert.Equal("Thẻ ATM / Visa / MasterCard (VNPay)", call.Data.PhuongThuc);
            Assert.Equal("14123456", call.Data.MaThamChieu);
            Assert.Equal(outcome.LichSuThanhToanId, call.Data.LichSuThanhToanId);
            Assert.Equal($"https://tro.test/KhachThue/HoaDon/BienLai/{outcome.LichSuThanhToanId}", call.Data.LinkXemBienLai);
            Assert.True(call.Data.DaThuDu);
            Assert.Empty(_log.Entries);
        }

        [Fact]
        public async Task Recorded_VoidedVnPayRow_SendsNoReceiptEmail()
        {
            _h.AddSentInvoice();
            var voided = _h.AddVnPayLedger(7, 2_000_000m, "14123456", PayTime, isDeleted: true);
            var outcome = new TransferOutcome
            {
                Kind = TransferOutcomeKind.Recorded,
                Transfer = Transfer(),
                HoaDonId = 7,
                MaHoaDon = "HD-7",
                LichSuThanhToanId = voided.LichSuThanhToanId
            };

            await DispatchAsync(outcome);

            Assert.Empty(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Recorded_WithoutAValidTenantEmail_LogsAWarning_AndSendsNoEmail()
        {
            var outcome = RecordVnPayRow();
            _h.Store.Meta[7] = _h.Store.Meta[7] with { EmailKhachThue = null };

            await DispatchAsync(outcome);

            Assert.Empty(_email.ReceiptCalls);
            Assert.Equal(2, _telegram.Messages.Count);
            Assert.Equal(LogLevel.Warning, Assert.Single(_log.Entries).Level);
        }

        [Theory]
        [InlineData(TransferOutcomeKind.AlreadyPaid)]
        [InlineData(TransferOutcomeKind.InvoiceNotPayable)]
        public async Task Alert_UsesTheVnPayRefundTitle_AndSendsNoEmail(TransferOutcomeKind kind)
        {
            _h.AddSentInvoice();
            var outcome = new TransferOutcome
            {
                Kind = kind,
                Transfer = Transfer(),
                Code = "TTPTHD7",
                HoaDonId = 7,
                MaHoaDon = "HD-7",
                LyDo = "Hóa đơn đã hủy"
            };

            await DispatchAsync(outcome);

            Assert.Equal(new[] { DefaultChat, BranchChat }, _telegram.Messages.Select(m => m.ChatId).ToArray());
            Assert.All(_telegram.Messages, m =>
            {
                Assert.StartsWith("\U0001F514 THANH TOÁN VNPAY CẦN HOÀN TIỀN\n", m.Text);
                Assert.Contains("Mã GD VNPay: 14123456 · Mã đơn: HD7_1", m.Text);
                Assert.DoesNotContain("CHUYỂN KHOẢN CẦN XỬ LÝ TAY", m.Text);
            });
            Assert.Empty(_email.ReceiptCalls);
        }
    }
}
