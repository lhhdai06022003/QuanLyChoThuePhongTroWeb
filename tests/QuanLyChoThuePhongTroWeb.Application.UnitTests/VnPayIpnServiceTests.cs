using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // IPN VNPay (spec VNPay §5.2, §6, §7): chữ ký, mã đơn, kết quả giao dịch, số tiền, ghi sổ trên store giả và mã trả cho VNPay.
    public class VnPayIpnServiceTests
    {
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private const string TransactionNo = "14123456";
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly DateTime PayTimeUtc = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);
        private static readonly string TxnRef = VnPayProtocol.BuildTxnRef(7, Now);

        private readonly PaymentTestHarness _h = new();
        private readonly RecordingPaymentNotificationQueue _queue = new();
        private readonly FakeTransferAlertDeduplicator _dedup = new();
        private readonly CapturingLogger<VnPayPaymentService> _log = new();
        private readonly VnPayPaymentService _service;

        public VnPayIpnServiceTests()
        {
            _service = Create(new PaymentOutcomePublisher(_h.Notifier, _queue, _dedup, NullLogger<PaymentOutcomePublisher>.Instance));
        }

        private VnPayPaymentService Create(IPaymentOutcomePublisher publisher)
        {
            var options = new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = Secret };
            return new VnPayPaymentService(options, new InvoiceTransferHandler(_h.Transaction), publisher, _h.Time, _log);
        }

        // Query IPN hợp lệ của hóa đơn 7 (2.000.000đ); tweak sửa tham số trước khi ký.
        private static Dictionary<string, string> Query(Action<Dictionary<string, string>>? tweak = null, bool sign = true)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_Amount"] = "200000000",
                ["vnp_BankCode"] = "NCB",
                ["vnp_BankTranNo"] = "VNP14123456",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan hoa don HD-7",
                ["vnp_PayDate"] = "20261003093000",
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = TransactionNo,
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = TxnRef
            };
            tweak?.Invoke(query);
            if (sign)
            {
                query["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(query), Secret);
            }

            return query;
        }

        private void AssertNothingHappened()
        {
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Empty(_h.Store.Ledger);
            Assert.Empty(_h.Store.Notifications);
            Assert.Empty(_queue.Items);
        }

        // ---------- Bước 1-2: chữ ký và merchant ----------

        [Fact]
        public async Task MissingSignature_Returns97_AndRecordsNothing()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(sign: false));

            Assert.Equal(VnPayIpnResult.InvalidSignature, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task WrongSignature_Returns97_RecordsNothing_AndLogsNoSecretOrSignature()
        {
            _h.AddSentInvoice();
            var query = Query();
            var signature = query["vnp_SecureHash"];
            query["vnp_SecureHash"] = (signature[0] == '0' ? '1' : '0') + signature[1..];

            var result = await _service.HandleIpnAsync(query);

            Assert.Equal("97", result.RspCode);
            AssertNothingHappened();
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.DoesNotContain(query["vnp_SecureHash"], entry.Message);
            Assert.DoesNotContain(signature, entry.Message);
            Assert.DoesNotContain(Secret, entry.Message);
            Assert.DoesNotContain(TransactionNo, entry.Message);
        }

        [Fact]
        public async Task AmountChangedAfterSigning_Returns97()
        {
            _h.AddSentInvoice();
            var query = Query();
            query["vnp_Amount"] = "100";

            var result = await _service.HandleIpnAsync(query);

            Assert.Equal("97", result.RspCode);
            AssertNothingHappened();
        }

        [Fact]
        public async Task DifferentTmnCode_EvenWhenCorrectlySigned_Returns97()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TmnCode"] = "OTHERTMN"));

            Assert.Equal(VnPayIpnResult.InvalidSignature, result);
            AssertNothingHappened();
            Assert.DoesNotContain(_log.Entries, e => e.Message.Contains("OTHERTMN"));
        }

        // ---------- Bước 3: mã đơn ----------

        [Theory]
        [InlineData("HD0_1")]
        [InlineData("ABC")]
        [InlineData("")]
        public async Task BadTxnRef_Returns01_AndRecordsNothing(string txnRef)
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TxnRef"] = txnRef));

            Assert.Equal(VnPayIpnResult.OrderNotFound, result);
            AssertNothingHappened();
        }

        // ---------- Bước 4: giao dịch không thành công ----------

        [Fact]
        public async Task FailedResponseCode_Returns00_WithoutRecordingOrNotifying()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_ResponseCode"] = "24"));

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            AssertNothingHappened();
            Assert.Empty(_h.Notifier.PaymentUpdates);
        }

        [Fact]
        public async Task SuccessfulResponseCode_ButFailedTransactionStatus_Returns00_WithoutRecording()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionStatus"] = "02"));

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task MissingTransactionStatus_IsNotASuccess()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q.Remove("vnp_TransactionStatus")));

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            AssertNothingHappened();
        }

        // ---------- Bước 5: số tiền ----------

        [Theory]
        [InlineData("200000050")]
        [InlineData("0")]
        [InlineData("abc")]
        [InlineData("-100")]
        [InlineData("")]
        [InlineData("2000000.00")]
        [InlineData("99999999999999999999")]
        public async Task InvalidAmount_Returns04_AndRecordsNothing(string amount)
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_Amount"] = amount));

            Assert.Equal(VnPayIpnResult.InvalidAmount, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task MissingAmount_Returns04()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q.Remove("vnp_Amount")));

            Assert.Equal("04", result.RspCode);
            AssertNothingHappened();
        }

        [Fact]
        public async Task ArabicIndicDigitsInTheAmount_Return04()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_Amount"] = "٢٠٠٠٠٠٠٠٠"));

            Assert.Equal("04", result.RspCode);
            AssertNothingHappened();
        }

        // ---------- Bước 5b: khóa chống trùng ----------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task BlankTransactionNo_Returns99_AndRecordsNothing(string transactionNo)
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = transactionNo));

            Assert.Equal(VnPayIpnResult.UnknownError, result);
            AssertNothingHappened();
            Assert.Equal(LogLevel.Warning, Assert.Single(_log.Entries).Level);
        }

        [Fact]
        public async Task MissingTransactionNo_Returns99()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q.Remove("vnp_TransactionNo")));

            Assert.Equal("99", result.RspCode);
            AssertNothingHappened();
        }

        [Fact]
        public async Task TooLongTransactionNo_Returns99_AndRecordsNothing()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = new string('9', 95)));

            Assert.Equal(VnPayIpnResult.UnknownError, result);
            AssertNothingHappened();
        }

        // ---------- Ghi sổ ----------

        [Fact]
        public async Task ValidIpn_RecordsTheLedgerRow_PaysTheInvoice_AndReturns00()
        {
            var hoaDon = _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", System.Text.Json.JsonSerializer.Serialize(new { result.RspCode, result.Message }));

            var ledger = Assert.Single(_h.Store.Ledger);
            Assert.Equal("VNPAY-14123456", ledger.MaGiaoDich);
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, ledger.PhuongThucThanhToan);
            Assert.Null(ledger.NguoiXacNhanId);
            Assert.Equal(PayTimeUtc, ledger.NgayThanhToan);
            Assert.Equal(Now, ledger.NgayXacNhan);
            Assert.Equal($"[VNPAY] NCB · ATM · TXN 14123456 · REF {TxnRef}", ledger.GhiChu);

            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.Equal("[TU_DONG] VNPay ghi nhận 2.000.000đ, TXN 14123456", Assert.Single(hoaDon.LichSuTrangThaiHoaDons).LyDo);
            Assert.True(_h.LastTx.Committed);
        }

        [Fact]
        public async Task ValidIpn_NotifiesInApp_ShowsTheVnPayWording_AndQueuesTheRecordedOutcome()
        {
            _h.AddSentInvoice();

            await _service.HandleIpnAsync(Query());

            Assert.All(_h.Store.Notifications, n => Assert.Equal("Đã nhận thanh toán VNPay 2.000.000đ — P101", n.TieuDe));
            var staff = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId);
            Assert.Contains("qua VNPay (TXN 14123456)", staff.NoiDung);
            Assert.DoesNotContain("chuyển khoản", staff.NoiDung);
            Assert.Equal(3, _h.Store.Notifications.Count);

            var item = Assert.Single(_queue.Items);
            Assert.Equal(TransferOutcomeKind.Recorded, item.Outcome.Kind);
            Assert.Equal("VNPAY-14123456", item.Outcome.DedupKey);
            var update = Assert.Single(_h.Notifier.PaymentUpdates);
            Assert.Equal(PaymentTestHarness.TenantUserId, update.UserId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not-a-date")]
        [InlineData("2026-10-03")]
        public async Task MissingOrBadPayDate_UsesTheServerTime(string? payDate)
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q =>
            {
                if (payDate == null)
                {
                    q.Remove("vnp_PayDate");
                }
                else
                {
                    q["vnp_PayDate"] = payDate;
                }
            }));

            Assert.Equal("00", result.RspCode);
            Assert.Equal(Now, Assert.Single(_h.Store.Ledger).NgayThanhToan);
        }

        [Fact]
        public async Task RemainingBalanceBelowTheAmountPaid_RecordsTheBalance_AndTagsTheSurplus()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT-EARLIER", Now.AddHours(-1));

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal("00", result.RspCode);
            var added = _h.Store.Ledger.Last();
            Assert.Equal("VNPAY-14123456", added.MaGiaoDich);
            Assert.Equal(1_500_000m, added.SoTienThanhToan);
            Assert.Equal(500_000m, PaymentNoteTags.Parse(added.GhiChu).TienThua);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
            var admin = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.AdminId);
            Assert.EndsWith(" Khách trả dư 500.000đ qua VNPay, cần xử lý ngoài hệ thống.", admin.NoiDung);
            Assert.Equal(500_000m, Assert.Single(_queue.Items).Outcome.TienThua);
        }

        // ---------- Gọi lại ----------

        [Fact]
        public async Task RepeatedIpn_Returns02_AndDoesNotRecordOrNotifyAgain()
        {
            _h.AddSentInvoice();
            await _service.HandleIpnAsync(Query());

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, result);
            Assert.Single(_h.Store.Ledger);
            Assert.Single(_queue.Items);
            Assert.Equal(3, _h.Store.Notifications.Count);
        }

        [Fact]
        public async Task VoidedVnPayRow_StillBlocksTheIpn_WithoutANewRow()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 2_000_000m, TransactionNo, Now.AddHours(-1), isDeleted: true);

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, result);
            Assert.Single(_h.Store.Ledger);
            Assert.Empty(_queue.Items);
        }

        // ---------- Hóa đơn không nhận được tiền ----------

        [Fact]
        public async Task AlreadyPaidInvoice_Returns02_QueuesOneAlert_AndDoesNotRepeatIt()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m);

            var first = await _service.HandleIpnAsync(Query());
            var second = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, first);
            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, second);
            Assert.Single(_h.Store.Ledger);
            var item = Assert.Single(_queue.Items);
            Assert.Equal(TransferOutcomeKind.AlreadyPaid, item.Outcome.Kind);
            Assert.Equal(OnlinePaymentChannel.VnPay, item.Outcome.Transfer.Kenh);
        }

        public static IEnumerable<object[]> NotPayableInvoices()
        {
            yield return new object[] { "cancelled", "Hóa đơn đã hủy" };
            yield return new object[] { "unpublished", "Hóa đơn chưa công bố" };
            yield return new object[] { "fractional", "Hóa đơn có số tiền lẻ" };
        }

        [Theory]
        [MemberData(nameof(NotPayableInvoices))]
        public async Task InvoiceThatCannotReceiveMoney_Returns02_AndQueuesAnAlert(string scenario, string reason)
        {
            var hoaDon = _h.AddSentInvoice();
            switch (scenario)
            {
                case "cancelled":
                    hoaDon.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                    break;
                case "unpublished":
                    hoaDon.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet;
                    break;
                default:
                    hoaDon.TongTien = 2_000_000.5m;
                    break;
            }

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, result);
            Assert.Empty(_h.Store.Ledger);
            var outcome = Assert.Single(_queue.Items).Outcome;
            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal(reason, outcome.LyDo);
        }

        [Fact]
        public async Task UnknownInvoice_Returns02_AndQueuesAnAlert()
        {
            _h.AddSentInvoice();

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TxnRef"] = VnPayProtocol.BuildTxnRef(999, Now)));

            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, result);
            var outcome = Assert.Single(_queue.Items).Outcome;
            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal("Không tìm thấy hóa đơn", outcome.LyDo);
            Assert.Empty(_h.Store.Ledger);
        }

        // ---------- Lỗi hệ thống ----------

        [Fact]
        public async Task SaveFailure_Returns99_RollsBack_AndPublishesNothing()
        {
            _h.AddSentInvoice();
            _h.UnitOfWork.FailOnSave = _ => new InvalidOperationException("boom");

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.UnknownError, result);
            Assert.True(_h.LastTx.RolledBack);
            Assert.False(_h.LastTx.Committed);
            Assert.Empty(_queue.Items);
            Assert.Empty(_h.Notifier.PaymentUpdates);
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task UniqueIndexViolation_Returns02()
        {
            _h.AddSentInvoice();
            _h.UnitOfWork.FailOnSave = _ => new UniqueConstraintViolationException("IX_lich_su_thanh_toan_MaGiaoDich", new Exception());

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, result);
            Assert.True(_h.LastTx.RolledBack);
            Assert.Empty(_queue.Items);
        }

        [Fact]
        public async Task PublisherThrows_StillReturns00_AndLogsAWarning()
        {
            _h.AddSentInvoice();
            var publisher = new RecordingOutcomePublisher { Throw = true };
            var service = Create(publisher);

            var result = await service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TransferOutcomeKind.Recorded, Assert.Single(publisher.Outcomes).Kind);
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task LogsNeverContainTheSignatureTheSecretOrTheOrderInfo()
        {
            _h.AddSentInvoice();
            var query = Query();

            await _service.HandleIpnAsync(query);

            Assert.NotEmpty(_log.Entries);
            Assert.All(_log.Entries, e =>
            {
                Assert.DoesNotContain(query["vnp_SecureHash"], e.Message);
                Assert.DoesNotContain(Secret, e.Message);
                Assert.DoesNotContain("Thanh toan hoa don", e.Message);
            });
        }
    }
}
