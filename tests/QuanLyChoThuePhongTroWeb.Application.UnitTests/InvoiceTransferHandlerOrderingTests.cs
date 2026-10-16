using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Thứ tự kiểm tra Duplicate → InvoiceNotPayable → AlreadyPaid và cách tính số còn lại (spec thanh toán trực tuyến §5.1).
    // Bổ sung cho InvoiceTransferHandlerTests: mỗi test dựng đồng thời hai điều kiện để chỉ thứ tự mới quyết định kết quả.
    public class InvoiceTransferHandlerOrderingTests
    {
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);
        private const string Key = "SEPAY-FT26ABC";

        private readonly PaymentTestHarness _h = new();
        private readonly InvoiceTransferHandler _handler;

        public InvoiceTransferHandlerOrderingTests()
        {
            _handler = new InvoiceTransferHandler(_h.Transaction);
        }

        private static IncomingBankTransfer Transfer(decimal amount = 2_000_000m, string reference = "FT26ABC") => new(
            92704, "MBBank", TransferTime, "0123456789", null, null, "TTPTHD7", "in", amount, reference, "TTPTHD7");

        private Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer) => _handler.HandleAsync(transfer, "TTPTHD7");

        // ---------- Thứ tự ----------

        [Fact]
        public async Task Duplicate_WinsOverInvoiceNotPayable_WhenInvoiceWasCancelledAfterRecording()
        {
            var hoaDon = _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m, Key);
            hoaDon.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            hoaDon.IsDeleted = true;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task Duplicate_WinsOverInvoiceNotPayable_ForAnUnpublishedInvoice()
        {
            var hoaDon = _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, Key, isDeleted: true);
            hoaDon.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
        }

        [Fact]
        public async Task InvoiceNotPayable_WinsOverAlreadyPaid()
        {
            var hoaDon = _h.AddSentInvoice();
            _h.AddLedger(7, 2_000_000m, "GD-OLD");
            hoaDon.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            hoaDon.IsDeleted = true;

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task SameKeyOnAnotherInvoice_IsNotAnApplicationLevelDuplicate()
        {
            // Kiểm trùng chỉ trong hóa đơn của giao dịch; cùng khóa ở hóa đơn khác do unique index chặn (test Infrastructure).
            _h.AddSentInvoice();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddLedger(8, 500_000m, Key);

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
        }

        [Fact]
        public async Task DifferentReferenceOnTheSameInvoice_IsNotADuplicate()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, "SEPAY-FT-KHAC");

            var outcome = await HandleAsync(Transfer(amount: 1_500_000m));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(2, _h.Store.Ledger.Count);
        }

        [Fact]
        public async Task Duplicate_LeavesAWaitingRequestUntouched()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, Key);
            var request = _h.AddRequest();

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, request.TrangThai);
            Assert.Null(outcome.MaLuotDaHuy);
        }

        [Fact]
        public async Task InvoiceNotPayable_LeavesAWaitingRequestUntouched()
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot;
            var request = _h.AddRequest();

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, request.TrangThai);
        }

        // ---------- Số tiền ----------

        [Fact]
        public async Task VoidedLedgerRow_DoesNotReduceTheRemainingAmount()
        {
            var hoaDon = _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, "GD-DA-HUY", isDeleted: true);

            var outcome = await HandleAsync(Transfer(amount: 2_000_000m));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(2_000_000m, outcome.SoTienGhiNhan);
            Assert.Equal(0m, outcome.TienThua);
            Assert.Equal(2_000_000m, outcome.DaThu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            var live = _h.Store.Ledger.Where(l => !l.IsDeleted).Single();
            Assert.DoesNotContain("[TIEN-THUA", live.GhiChu);
        }

        [Fact]
        public async Task AmountEqualToTheRemainingAfterAPartialPayment_HasNoSurplusTag()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m);

            var outcome = await HandleAsync(Transfer(amount: 1_500_000m));

            Assert.Equal(0m, outcome.TienThua);
            Assert.True(outcome.DaThanhToanDu);
            var ledger = _h.Store.Ledger.Last();
            Assert.DoesNotContain("TIEN-THUA", ledger.GhiChu);
            Assert.StartsWith("[SEPAY]", ledger.GhiChu);
        }

        [Fact]
        public async Task OverpaymentByOneDong_IsTaggedWithTheExactSurplus()
        {
            _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer(amount: 2_000_001m));

            Assert.Equal(2_000_000m, outcome.SoTienGhiNhan);
            Assert.Equal(1m, outcome.TienThua);
            Assert.StartsWith("[TIEN-THUA:1] [SEPAY]", Assert.Single(_h.Store.Ledger).GhiChu);
        }

        [Fact]
        public async Task OneDongShort_IsAPartialPayment_AndLeavesOneDongOwed()
        {
            var hoaDon = _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer(amount: 1_999_999m));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(1m, outcome.ConLai);
            Assert.False(outcome.DaThanhToanDu);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, hoaDon.TrangThaiHoaDon);
        }

        [Fact]
        public async Task OverpaymentAfterAPartialPayment_RecordsOnlyWhatIsStillOwed()
        {
            // Một phần rồi dư: tổng ghi sổ không bao giờ vượt tổng tiền hóa đơn.
            var hoaDon = _h.AddSentInvoice();
            await HandleAsync(Transfer(amount: 1_000_000m, reference: "FT-1"));

            var second = await HandleAsync(Transfer(amount: 3_000_000m, reference: "FT-2"));

            Assert.Equal(1_000_000m, second.SoTienGhiNhan);
            Assert.Equal(2_000_000m, second.TienThua);
            Assert.Equal(2_000_000m, _h.Store.Ledger.Sum(l => l.SoTienThanhToan));
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, hoaDon.TrangThaiHoaDon);
            Assert.Equal(PaymentNoteTags.Parse(_h.Store.Ledger.Last().GhiChu).TienThua, 2_000_000m);
        }

        [Fact]
        public async Task TransferAfterTheInvoiceIsPaid_DoesNotRecordASecondTime()
        {
            _h.AddSentInvoice();
            await HandleAsync(Transfer(reference: "FT-1"));

            var second = await HandleAsync(Transfer(reference: "FT-2"));

            Assert.Equal(TransferOutcomeKind.AlreadyPaid, second.Kind);
            Assert.Single(_h.Store.Ledger);
        }
    }
}
