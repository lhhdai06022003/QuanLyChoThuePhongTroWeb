using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Kiểm tra tạo link VNPay và trang kết quả cho giữ chỗ (spec giữ chỗ §5.2, §5.3, §15)
    public class ReservationVnPayCheckoutServiceTests
    {
        private const string Secret = "secret";
        private const string TmnCode = "TESTTMN1";
        private const string ReturnUrl = "http://localhost:5009/tai-khoan/yeu-cau-phong/giu-cho/dat-coc/vnpay/ket-qua";
        private const string ClientIp = "203.0.113.9";
        private const int ActorId = 700;
        private const int ReservationId = 5;
        private static readonly DateTime Now = new(2026, 10, 3, 2, 0, 0, DateTimeKind.Utc);

        private readonly FakeReservationAutoPaymentStore _store = new();
        private readonly FakeThongBaoService _thongBao = new();
        private readonly RecordingOutcomePublisher _publisher = new();
        private readonly OnlinePaymentOptions _onlineOptions = new() { ReservationEnabled = true };
        private readonly FixedTimeProvider _time = new(new DateTimeOffset(Now));
        private readonly CapturingLogger<ReservationVnPayService> _log = new();

        private ReservationVnPayService CreateService(bool vnPayEnabled = true, bool reservationEnabled = true, int expireMinutes = 15)
        {
            var handler = new ReservationTransferHandler(_store, _thongBao, _onlineOptions, _time, new CapturingLogger<ReservationTransferHandler>());
            var options = new VnPayOptions { Enabled = vnPayEnabled, TmnCode = TmnCode, HashSecret = Secret, ExpireMinutes = expireMinutes };
            _onlineOptions.ReservationEnabled = reservationEnabled;
            return new ReservationVnPayService(options, _onlineOptions, _store, handler, _publisher, _time, _log);
        }

        private static Dictionary<string, string> ParseQuery(string url)
        {
            return url[(url.IndexOf('?') + 1)..].Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);
        }

        [Fact]
        public async Task CreatePaymentUrl_ValidReservation_BuildsSignedUrlWithCorrectParams()
        {
            var service = CreateService();
            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(
                reservationId: ReservationId,
                actorId: ActorId,
                maYeuCau: "TTPTGC5",
                soTienGiuCho: 1_000_000m,
                daNhan: 400_000m, // còn lại 600.000đ
                hanThanhToanUtc: Now.AddHours(2),
                soPhong: "P101"
            );

            var result = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);

            Assert.True(result.Success);
            var url = result.Data!;
            Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", url);
            var query = ParseQuery(url);
            Assert.Equal("2.1.0", query["vnp_Version"]);
            Assert.Equal("pay", query["vnp_Command"]);
            Assert.Equal(TmnCode, query["vnp_TmnCode"]);
            Assert.Equal("60000000", query["vnp_Amount"]); // 600.000 x 100
            Assert.Equal("VND", query["vnp_CurrCode"]);
            Assert.Equal(VnPayProtocol.BuildReservationTxnRef(ReservationId, Now), query["vnp_TxnRef"]);
            Assert.Equal("Thanh toan giu cho phong P101", query["vnp_OrderInfo"]);
            Assert.Equal("other", query["vnp_OrderType"]);
            Assert.Equal("vn", query["vnp_Locale"]);
            Assert.Equal(ReturnUrl, query["vnp_ReturnUrl"]);
            Assert.Equal(ClientIp, query["vnp_IpAddr"]);
            Assert.True(VnPayProtocol.Verify(query, Secret));
        }

        [Fact]
        public async Task CreatePaymentUrl_WhenFlagsDisabled_ReturnsNotFound()
        {
            var serviceVnPayOff = CreateService(vnPayEnabled: false);
            var res1 = await serviceVnPayOff.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(res1.Success);
            Assert.Equal(ServiceErrorKind.NotFound, res1.ErrorKind);

            var serviceResOff = CreateService(reservationEnabled: false);
            var res2 = await serviceResOff.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(res2.Success);
            Assert.Equal(ServiceErrorKind.NotFound, res2.ErrorKind);
        }

        [Fact]
        public async Task CreatePaymentUrl_WhenSnapshotNotFoundOrDifferentActor_ReturnsNotFound()
        {
            var service = CreateService();
            _store.Snapshot = null;

            var res = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(res.Success);
            Assert.Equal(ServiceErrorKind.NotFound, res.ErrorKind);

            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(reservationId: ReservationId, actorId: 999);
            var resDifferentActor = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(resDifferentActor.Success);
            Assert.Equal(ServiceErrorKind.NotFound, resDifferentActor.ErrorKind);
        }

        [Fact]
        public async Task CreatePaymentUrl_WhenInvalidStatus_ReturnsFail()
        {
            var service = CreateService();
            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(
                trangThai: TrangThaiYeuCauGiuCho.DangGiuCho
            );

            var res = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(res.Success);
            Assert.Equal("Yêu cầu giữ chỗ không còn ở trạng thái nhận thanh toán.", res.Message);
        }

        [Fact]
        public async Task CreatePaymentUrl_WhenOldCodePrefix_ReturnsFail()
        {
            var service = CreateService();
            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(
                maYeuCau: "GCABC1234567"
            );

            var res = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(res.Success);
            Assert.Equal("Yêu cầu này chỉ nhận chuyển khoản, vui lòng liên hệ nhân viên.", res.Message);
        }

        [Fact]
        public async Task CreatePaymentUrl_WhenDeadlineLessThan5Minutes_ReturnsFail()
        {
            var service = CreateService();
            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(
                hanThanhToanUtc: Now.AddMinutes(4)
            );

            var res = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(res.Success);
            Assert.Equal("Sắp hết hạn thanh toán, vui lòng liên hệ nhân viên.", res.Message);
        }

        [Fact]
        public async Task CreatePaymentUrl_WhenFullyPaid_ReturnsFail()
        {
            var service = CreateService();
            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(
                soTienGiuCho: 1_000_000m,
                daNhan: 1_000_000m
            );

            var res = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.False(res.Success);
            Assert.Equal("Yêu cầu giữ chỗ đã được thanh toán đủ.", res.Message);
        }

        [Fact]
        public async Task CreatePaymentUrl_CapsExpireDateAtDeadline()
        {
            var service = CreateService(expireMinutes: 30);
            var deadline = Now.AddMinutes(10);
            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(
                hanThanhToanUtc: deadline
            );

            var res = await service.CreatePaymentUrlAsync(ActorId, ReservationId, ClientIp, ReturnUrl);
            Assert.True(res.Success);
            var query = ParseQuery(res.Data!);
            Assert.Equal(VnPayProtocol.FormatVietnamTime(deadline), query["vnp_ExpireDate"]);
        }

        // --- Tests for GetReturnResultAsync ---

        private static Dictionary<string, string> ReturnQuery(
            int reservationId = ReservationId,
            string responseCode = "00",
            string transactionNo = "14123456",
            decimal amount = 1_000_000m,
            bool sign = true,
            string tmnCode = TmnCode)
        {
            var q = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = tmnCode,
                ["vnp_Amount"] = (amount * 100m).ToString("0"),
                ["vnp_BankCode"] = "NCB",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan giu cho phong P101",
                ["vnp_PayDate"] = "20261003093000",
                ["vnp_ResponseCode"] = responseCode,
                ["vnp_TransactionNo"] = transactionNo,
                ["vnp_TransactionStatus"] = responseCode,
                ["vnp_TxnRef"] = VnPayProtocol.BuildReservationTxnRef(reservationId, Now)
            };
            if (sign)
            {
                q["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(q), Secret);
            }
            return q;
        }

        [Fact]
        public async Task GetReturnResult_InvalidSignature_ReturnsUnverified()
        {
            var service = CreateService();
            var q = ReturnQuery(sign: false);
            q["vnp_SecureHash"] = "invalidsignature";

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.KhongXacThuc, res.TrangThai);
            Assert.Null(res.YeuCauGiuChoId);
        }

        [Fact]
        public async Task GetReturnResult_DifferentActor_ReturnsUnverified()
        {
            var service = CreateService();
            var q = ReturnQuery();
            _store.Snapshot = FakeReservationAutoPaymentStore.DefaultSnapshot(reservationId: ReservationId, actorId: 999);

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.KhongXacThuc, res.TrangThai);
            Assert.Null(res.YeuCauGiuChoId);
        }

        [Fact]
        public async Task GetReturnResult_CancelledByCustomer_ReturnsDaHuy()
        {
            var service = CreateService();
            var q = ReturnQuery(responseCode: "24");

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.DaHuy, res.TrangThai);
            Assert.Equal(ReservationId, res.YeuCauGiuChoId);
        }

        [Fact]
        public async Task GetReturnResult_FailedPayment_ReturnsKhongThanhCongWithDescription()
        {
            var service = CreateService();
            var q = ReturnQuery(responseCode: "51");

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.KhongThanhCong, res.TrangThai);
            Assert.Equal(ReservationId, res.YeuCauGiuChoId);
            Assert.False(string.IsNullOrEmpty(res.MoTaLoi));
        }

        [Fact]
        public async Task GetReturnResult_SuccessAndTransactionRecorded_ReturnsDaGhiNhan()
        {
            var service = CreateService();
            var q = ReturnQuery();
            _store.Transaction = new ReservationTransactionSnapshot(
                GiaoDichGiuChoId: 88,
                YeuCauThanhToanGiuChoId: 9,
                MaGiaoDich: "VNPAY-14123456",
                SoTienThucNhan: 1_000_000m,
                GhiChu: null,
                NgayThucNhanUtc: Now
            );

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.DaGhiNhan, res.TrangThai);
            Assert.Equal(ReservationId, res.YeuCauGiuChoId);
            Assert.Equal(88, res.GiaoDichGiuChoId);
            Assert.Equal(1_000_000m, res.SoTien);
            Assert.False(res.CanHoan);
        }

        [Fact]
        public async Task GetReturnResult_SuccessWithLateRefund_ReturnsDaGhiNhanWithCanHoan()
        {
            var service = CreateService();
            var q = ReturnQuery();
            _store.Transaction = new ReservationTransactionSnapshot(
                GiaoDichGiuChoId: 89,
                YeuCauThanhToanGiuChoId: 9,
                MaGiaoDich: "VNPAY-14123456",
                SoTienThucNhan: 1_000_000m,
                GhiChu: ReservationPaymentMarkers.LateRefundRequired,
                NgayThucNhanUtc: Now
            );

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.DaGhiNhan, res.TrangThai);
            Assert.True(res.CanHoan);
        }

        [Fact]
        public async Task GetReturnResult_SuccessNotYetRecorded_ReturnsDangGhiNhan()
        {
            var service = CreateService();
            var q = ReturnQuery();
            _store.Transaction = null;

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.DangGhiNhan, res.TrangThai);
            Assert.Equal(ReservationId, res.YeuCauGiuChoId);
        }

        [Fact]
        public async Task GetReturnResult_FlagDisabledAndNotRecorded_ReturnsKhongGhiNhan()
        {
            var service = CreateService(reservationEnabled: false);
            var q = ReturnQuery();
            _store.Transaction = null;

            var res = await service.GetReturnResultAsync(q, ActorId);
            Assert.Equal(VnPayReturnStatus.KhongGhiNhan, res.TrangThai);
            Assert.Equal(ReservationId, res.YeuCauGiuChoId);
        }
    }
}
