using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // IPN VNPay cho tiền giữ chỗ (spec giữ chỗ §6.2): thứ tự bước, mã trả cho VNPay, ghi qua ReservationTransferHandler thật.
    public class ReservationVnPayIpnServiceTests
    {
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private const string TransactionNo = "14123456";
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly DateTime PayTimeUtc = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);
        private static readonly string TxnRef = VnPayProtocol.BuildReservationTxnRef(5, Now);

        private readonly FakeReservationAutoPaymentStore _store = new();
        private readonly FakeThongBaoService _thongBao = new();
        private readonly RecordingOutcomePublisher _publisher = new();
        private readonly OnlinePaymentOptions _onlineOptions = new() { ReservationEnabled = true };
        private readonly FixedTimeProvider _time = new(new DateTimeOffset(Now));
        private readonly CapturingLogger<ReservationVnPayService> _log = new();
        private readonly ReservationVnPayService _service;

        public ReservationVnPayIpnServiceTests()
        {
            var handler = new ReservationTransferHandler(
                _store, _thongBao, _onlineOptions, _time, new CapturingLogger<ReservationTransferHandler>());
            var vnPay = new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = Secret };
            _service = new ReservationVnPayService(vnPay, _onlineOptions, _store, handler, _publisher, _time, _log);
        }

        // Query IPN hợp lệ của giữ chỗ 5 (1.000.000đ); tweak sửa tham số trước khi ký.
        private static Dictionary<string, string> Query(Action<Dictionary<string, string>>? tweak = null, bool sign = true)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_Amount"] = "100000000",
                ["vnp_BankCode"] = "NCB",
                ["vnp_BankTranNo"] = "VNP14123456",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan giu cho TTPTGC5",
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
            Assert.Empty(_store.RecordCalls);
            Assert.Empty(_thongBao.Sent);
            Assert.Empty(_publisher.Outcomes);
        }

        [Fact]
        public async Task MissingSignature_Returns97()
        {
            var result = await _service.HandleIpnAsync(Query(sign: false));

            Assert.Equal(VnPayIpnResult.InvalidSignature, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task WrongSignature_Returns97_AndLogsNoSecretOrSignature()
        {
            var query = Query();
            var signature = query["vnp_SecureHash"];
            query["vnp_SecureHash"] = (signature[0] == '0' ? '1' : '0') + signature[1..];

            var result = await _service.HandleIpnAsync(query);

            Assert.Equal("97", result.RspCode);
            AssertNothingHappened();
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.DoesNotContain(query["vnp_SecureHash"], entry.Message);
            Assert.DoesNotContain(Secret, entry.Message);
        }

        [Fact]
        public async Task DifferentTmnCode_EvenWhenCorrectlySigned_Returns97()
        {
            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TmnCode"] = "OTHERTMN"));

            Assert.Equal(VnPayIpnResult.InvalidSignature, result);
            AssertNothingHappened();
        }

        [Theory]
        [InlineData("HD5_1")]
        [InlineData("GC0_1")]
        [InlineData("ABC")]
        [InlineData("")]
        public async Task BadTxnRef_Returns01(string txnRef)
        {
            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TxnRef"] = txnRef));

            Assert.Equal(VnPayIpnResult.OrderNotFound, result);
            AssertNothingHappened();
            Assert.Empty(_store.ExistsQueries);
        }

        [Fact]
        public async Task FailedResponseCode_Returns00_WithoutRecordingOrPublishing()
        {
            var result = await _service.HandleIpnAsync(Query(q => q["vnp_ResponseCode"] = "24"));

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            AssertNothingHappened();
            Assert.Empty(_store.ExistsQueries);
        }

        [Fact]
        public async Task FailedTransactionStatus_Returns00_WithoutRecording()
        {
            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionStatus"] = "02"));

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task FlagOff_Returns01_AndLogsAWarning()
        {
            _onlineOptions.ReservationEnabled = false;

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.OrderNotFound, result);
            AssertNothingHappened();
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("ReservationEnabled"));
        }

        [Fact]
        public async Task NoPaymentRequestForTheReservation_Returns01_BeforeCheckingTheAmount()
        {
            _store.Exists = false;

            var result = await _service.HandleIpnAsync(Query(q => q["vnp_Amount"] = "abc"));

            Assert.Equal(VnPayIpnResult.OrderNotFound, result);
            Assert.Equal("TTPTGC5", Assert.Single(_store.ExistsQueries));
            AssertNothingHappened();
        }

        [Theory]
        [InlineData("100000050")]
        [InlineData("0")]
        [InlineData("abc")]
        [InlineData("-100")]
        [InlineData("")]
        public async Task InvalidAmount_Returns04(string amount)
        {
            var result = await _service.HandleIpnAsync(Query(q => q["vnp_Amount"] = amount));

            Assert.Equal(VnPayIpnResult.InvalidAmount, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task MissingAmount_Returns04()
        {
            var result = await _service.HandleIpnAsync(Query(q => q.Remove("vnp_Amount")));

            Assert.Equal("04", result.RspCode);
            AssertNothingHappened();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task BlankTransactionNo_Returns99(string transactionNo)
        {
            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = transactionNo));

            Assert.Equal(VnPayIpnResult.UnknownError, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task TooLongTransactionNo_Returns99()
        {
            var result = await _service.HandleIpnAsync(Query(q => q["vnp_TransactionNo"] = new string('9', 95)));

            Assert.Equal(VnPayIpnResult.UnknownError, result);
            AssertNothingHappened();
        }

        [Fact]
        public async Task Recorded_Returns00_RecordsWithTheVnPayKeyAndPayTime_AndPublishesOnce()
        {
            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            var call = Assert.Single(_store.RecordCalls);
            Assert.Equal("TTPTGC5", call.MaYeuCau);
            Assert.Equal("VNPAY-14123456", call.MaGiaoDich);
            Assert.Equal(1_000_000m, call.SoTien);
            Assert.Equal(PayTimeUtc, call.NgayThucNhanUtc);
            var outcome = Assert.Single(_publisher.Outcomes);
            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(OnlinePaymentChannel.VnPay, outcome.Transfer.Kenh);
            Assert.Equal("ATM", outcome.Transfer.CardType);
            Assert.Equal(TxnRef, outcome.Transfer.Description);
        }

        [Fact]
        public async Task Recorded_ForALateRefund_StillReturns00()
        {
            _store.NextResult = FakeReservationAutoPaymentStore.Recorded(canHoanTien: true, vuaCoHieuLuc: false, trangThai: "HetHan");

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.Confirmed, result);
        }

        [Fact]
        public async Task MissingPayDate_UsesTheServerTime()
        {
            await _service.HandleIpnAsync(Query(q => q.Remove("vnp_PayDate")));

            Assert.Equal(Now, Assert.Single(_store.RecordCalls).NgayThucNhanUtc);
        }

        [Fact]
        public async Task Duplicate_Returns02()
        {
            _store.NextResult = new SystemReservationRecordResult(SystemReservationRecordStatus.Duplicate);

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.AlreadyConfirmed, result);
        }

        [Fact]
        public async Task StoreConflict_Returns99()
        {
            _store.NextResult = new SystemReservationRecordResult(SystemReservationRecordStatus.Conflict);

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.UnknownError, result);
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task StoreThrows_Returns99()
        {
            _store.ThrowOnRecord = true;

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.UnknownError, result);
        }

        [Fact]
        public async Task StoreNotFoundAtRecordTime_Returns01()
        {
            _store.NextResult = new SystemReservationRecordResult(SystemReservationRecordStatus.NotFound);

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.OrderNotFound, result);
        }

        [Fact]
        public async Task PublisherThrows_DoesNotChangeTheResult()
        {
            _publisher.Throw = true;

            var result = await _service.HandleIpnAsync(Query());

            Assert.Equal(VnPayIpnResult.Confirmed, result);
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task Logs_NeverContainTheOrderInfoOrTheSecret()
        {
            await _service.HandleIpnAsync(Query());

            Assert.DoesNotContain(_log.Entries, e => e.Message.Contains("Thanh toan giu cho") || e.Message.Contains(Secret));
        }
    }
}
