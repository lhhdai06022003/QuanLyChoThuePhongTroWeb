using System;
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
    // Ghi tiền giữ chỗ khi có tiền vào với mã TTPTGC{id} (spec giữ chỗ §6): thứ tự kiểm, ánh xạ kết quả store và thông báo cho nhân viên.
    public class ReservationTransferHandlerTests
    {
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly FakeReservationAutoPaymentStore _store = new();
        private readonly FakeThongBaoService _thongBao = new();
        private readonly OnlinePaymentOptions _options = new() { ReservationEnabled = true };
        private readonly FixedTimeProvider _time = new(new DateTimeOffset(PaymentTestHarness.Now));
        private readonly CapturingLogger<ReservationTransferHandler> _log = new();
        private readonly ReservationTransferHandler _handler;

        public ReservationTransferHandlerTests()
        {
            _handler = new ReservationTransferHandler(_store, _thongBao, _options, _time, _log);
        }

        private static IncomingBankTransfer Transfer(decimal amount = 1_000_000m, string? reference = "FT26ABC", bool sync = false) => new(
            92704, "MBBank", TransferTime, "0123456789", null, null, "TTPTGC5", "in", amount, reference, "TTPTGC5")
        {
            LaDongBoBu = sync
        };

        private Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code = "TTPTGC5") =>
            _handler.HandleAsync(transfer, code);

        [Fact]
        public void TransferCode_IsThePrefixPlusTheId()
        {
            Assert.Equal("TTPTGC57", ReservationTransferHandler.TransferCode(57));
            Assert.Equal("TTPTGC", new ReservationTransferHandler(_store, _thongBao, _options, _time, _log).Prefix);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task NonPositiveAmount_IsIgnored_WithoutTouchingTheStore(int amount)
        {
            var outcome = await HandleAsync(Transfer(amount));

            Assert.Equal(TransferOutcomeKind.Ignored, outcome.Kind);
            Assert.Empty(_store.RecordCalls);
        }

        [Theory]
        [InlineData("TTPTGC")]
        [InlineData("TTPTGC0")]
        [InlineData("TTPTGCABC")]
        [InlineData("TTPTGC1234567890")]
        public async Task CodeWithoutAValidId_IsUnmatched(string code)
        {
            var outcome = await HandleAsync(Transfer(), code);

            Assert.Equal(TransferOutcomeKind.Unmatched, outcome.Kind);
            Assert.Equal(code, outcome.Code);
            Assert.Empty(_store.RecordCalls);
        }

        [Fact]
        public async Task SyncTransfer_IsIgnored_WithAReason_AndNothingIsRecorded()
        {
            var outcome = await HandleAsync(Transfer(sync: true));

            Assert.Equal(TransferOutcomeKind.Ignored, outcome.Kind);
            Assert.Equal("Giao dịch giữ chỗ: đồng bộ bù không ghi, đối chiếu tay ở màn Giữ chỗ.", outcome.LyDo);
            Assert.Empty(_store.RecordCalls);
            Assert.Empty(_thongBao.Sent);
        }

        [Fact]
        public async Task FlagOff_IsUnmatched_AndNothingIsRecorded()
        {
            _options.ReservationEnabled = false;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Unmatched, outcome.Kind);
            Assert.Equal("TTPTGC5", outcome.Code);
            Assert.Empty(_store.RecordCalls);
            Assert.Empty(_thongBao.Sent);
        }

        [Fact]
        public async Task DedupKeyLongerThanTheLimit_Fails_WithoutTouchingTheStore()
        {
            var outcome = await HandleAsync(Transfer(reference: new string('A', 100)));

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            Assert.Equal("Mã giao dịch quá dài", outcome.LyDo);
            Assert.Empty(_store.RecordCalls);
        }

        [Fact]
        public async Task StoreNotFound_IsUnmatched_WithAReason()
        {
            _store.NextResult = new SystemReservationRecordResult(SystemReservationRecordStatus.NotFound);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Unmatched, outcome.Kind);
            Assert.Equal("Không tìm thấy yêu cầu thanh toán giữ chỗ", outcome.LyDo);
            Assert.Empty(_thongBao.Sent);
        }

        [Fact]
        public async Task StoreDuplicate_IsDuplicate_WithoutNotifying()
        {
            _store.NextResult = new SystemReservationRecordResult(SystemReservationRecordStatus.Duplicate);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            Assert.Empty(_thongBao.Sent);
        }

        [Fact]
        public async Task StoreConflict_Fails_SoTheChannelRetries()
        {
            _store.NextResult = new SystemReservationRecordResult(SystemReservationRecordStatus.Conflict);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            Assert.Equal("Dữ liệu vừa thay đổi", outcome.LyDo);
        }

        [Fact]
        public async Task StoreThrows_Fails_AndLogsAnErrorWithoutTheTransferContent()
        {
            _store.ThrowOnRecord = true;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            Assert.Equal("Lỗi hệ thống khi ghi tiền giữ chỗ", outcome.LyDo);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains("SEPAY-FT26ABC", entry.Message);
        }

        [Fact]
        public async Task Recorded_PassesTheRightArgumentsToTheStore()
        {
            await HandleAsync(Transfer());

            var call = Assert.Single(_store.RecordCalls);
            Assert.Equal("TTPTGC5", call.MaYeuCau);
            Assert.Equal("SEPAY-FT26ABC", call.MaGiaoDich);
            Assert.Equal(1_000_000m, call.SoTien);
            Assert.Equal(TransferTime, call.NgayThucNhanUtc);
            Assert.Equal("Ghi nhận tự động SEPAY-FT26ABC", call.LyDo);
            Assert.Equal(PaymentTestHarness.Now, call.NowUtc);
        }

        [Fact]
        public async Task Recorded_VnPayTransfer_UsesTheVnPayKey()
        {
            var transfer = new IncomingBankTransfer(0, "NCB", TransferTime, null, null, null, "Thanh toan giu cho", "in", 1_000_000m, "14123456", "GC5_1")
            {
                Kenh = OnlinePaymentChannel.VnPay
            };

            await HandleAsync(transfer);

            Assert.Equal("VNPAY-14123456", Assert.Single(_store.RecordCalls).MaGiaoDich);
        }

        [Fact]
        public async Task Recorded_FullAmount_ReportsTheNumbers_AndNotifiesTheRoomStaff()
        {
            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal("TTPTGC5", outcome.Code);
            Assert.Equal(1_000_000m, outcome.SoTienGhiNhan);
            Assert.Equal(1_000_000m, outcome.TongTien);
            Assert.Equal(1_000_000m, outcome.DaThu);
            Assert.Equal(0m, outcome.ConLai);
            Assert.Equal(0m, outcome.TienThua);
            Assert.True(outcome.DaThanhToanDu);
            Assert.NotNull(outcome.GiuCho);
            Assert.Null(outcome.HoaDonId);
            Assert.Null(outcome.TenantUserId);
            Assert.Null(outcome.LichSuThanhToanId);
            Assert.False(outcome.ConMinhChungChoDoiChieu);

            var notice = Assert.Single(_thongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Equal(12, notice.TargetId);
            Assert.Equal("ThanhToan", notice.LinhVuc);
            Assert.Equal("/QuanLyNhaTro/GiuCho/ChiTiet/5", notice.Link);
            Assert.Contains("Đã nhận tiền giữ chỗ 1.000.000đ qua SePay", notice.TieuDe);
            Assert.Contains("phòng P101", notice.TieuDe);
            Assert.Contains("đã nhận 1.000.000/1.000.000đ, còn lại 0đ", notice.NoiDung);
            Assert.Contains("Giữ chỗ đã có hiệu lực.", notice.NoiDung);
        }

        [Fact]
        public async Task Recorded_Partial_ReportsTheRemainingAmount()
        {
            _store.NextResult = FakeReservationAutoPaymentStore.Recorded(
                daNhanTruoc: 0, daNhanSau: 400_000m, vuaCoHieuLuc: false, trangThai: "ChoXacNhanTien");

            var outcome = await HandleAsync(Transfer(400_000m));

            Assert.Equal(600_000m, outcome.ConLai);
            Assert.Equal(400_000m, outcome.DaThu);
            Assert.Equal(0m, outcome.TienThua);
            Assert.False(outcome.DaThanhToanDu);
            var notice = Assert.Single(_thongBao.Sent);
            Assert.Contains("còn lại 600.000đ", notice.NoiDung);
            Assert.DoesNotContain("đã có hiệu lực", notice.NoiDung);
        }

        [Fact]
        public async Task Recorded_Overpay_ReportsTheSurplusOfThisTransfer()
        {
            _store.NextResult = FakeReservationAutoPaymentStore.Recorded(daNhanTruoc: 600_000m, daNhanSau: 1_300_000m);

            var outcome = await HandleAsync(Transfer(700_000m));

            Assert.Equal(300_000m, outcome.TienThua);
            Assert.Equal(0m, outcome.ConLai);
            Assert.Contains("Khách chuyển dư 300.000đ, cần hoàn cho khách.", Assert.Single(_thongBao.Sent).NoiDung);
        }

        [Fact]
        public async Task Recorded_SurplusIsNeverMoreThanThisTransfer()
        {
            // Đã vượt số giữ chỗ từ trước: lần này chỉ tính phần của lần này là dư.
            _store.NextResult = FakeReservationAutoPaymentStore.Recorded(daNhanTruoc: 1_200_000m, daNhanSau: 1_400_000m, vuaCoHieuLuc: false);

            var outcome = await HandleAsync(Transfer(200_000m));

            Assert.Equal(200_000m, outcome.TienThua);
        }

        [Fact]
        public async Task Recorded_LateRefund_HasNoSurplus_AndSaysTheMoneyMustBeRefunded()
        {
            _store.NextResult = FakeReservationAutoPaymentStore.Recorded(
                daNhanTruoc: 0, daNhanSau: 1_000_000m, canHoanTien: true, vuaCoHieuLuc: false, trangThai: "HetHan");

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(0m, outcome.TienThua);
            var notice = Assert.Single(_thongBao.Sent);
            Assert.Contains("cần hoàn cho khách", notice.NoiDung);
            Assert.Contains("quá hạn hoặc không còn hiệu lực", notice.NoiDung);
            Assert.DoesNotContain("chuyển dư", notice.NoiDung);
        }

        [Fact]
        public async Task Recorded_WithAPendingEvidence_WarnsStaffToCheckForDuplicates()
        {
            _store.NextResult = FakeReservationAutoPaymentStore.Recorded(conMinhChung: true);

            await HandleAsync(Transfer());

            Assert.Contains("kiểm tra trùng trước khi xác nhận", Assert.Single(_thongBao.Sent).NoiDung);
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("minh chứng chờ"));
        }

        [Fact]
        public async Task NotificationThrows_StillReturnsRecorded_AndLogsAWarning()
        {
            _thongBao.ThrowOnSend = true;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("thông báo trong app"));
        }

        [Fact]
        public async Task NotificationFails_StillReturnsRecorded_AndLogsAWarning()
        {
            _thongBao.FailOnSend = true;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Contains(_log.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task Logs_NeverContainTheBankContent()
        {
            var transfer = Transfer() with { Content = "NOI-DUNG-BI-MAT-TTPTGC5", Description = "MO-TA-BI-MAT" };

            await HandleAsync(transfer);

            Assert.DoesNotContain(_log.Entries, e => e.Message.Contains("BI-MAT"));
            Assert.NotEmpty(_log.Entries.Where(e => e.Level == LogLevel.Information));
        }
    }
}
