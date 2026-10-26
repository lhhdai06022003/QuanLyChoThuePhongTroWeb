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
    // Ghi sổ hóa đơn từ giao dịch kênh VNPay (spec VNPay §6, §7): ghi chú, lý do lịch sử, câu chữ riêng của kênh VNPay
    // (B5') và các nhánh dùng chung với SePay. Câu chữ của kênh SePay được giữ ở InvoiceTransferHandlerTests.
    public class InvoiceTransferHandlerVnPayTests
    {
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly DateTime PayTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly PaymentTestHarness _h = new();
        private readonly InvoiceTransferHandler _handler;

        public InvoiceTransferHandlerVnPayTests()
        {
            _handler = new InvoiceTransferHandler(_h.Transaction);
        }

        private static IncomingBankTransfer Transfer(
            decimal amount = 2_000_000m, string? transactionNo = "14123456", string? bankCode = "NCB", string? cardType = "ATM",
            string? txnRef = "HD7_638654321098765432") => new(
                0, bankCode, PayTime, null, null, null, "Thanh toan hoa don HD-7", "in", amount, transactionNo, txnRef)
        {
            Kenh = OnlinePaymentChannel.VnPay,
            CardType = cardType
        };

        private Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, string code = "TTPTHD7")
            => _handler.HandleAsync(transfer, code);

        [Fact]
        public async Task Note_StripsBracketsFromVnPayValues()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer(bankCode: "N[C]B", cardType: "A]T[M", txnRef: "HD7_[1]"));

            var note = Assert.Single(_h.Store.Ledger).GhiChu;
            Assert.Equal("[VNPAY] NCB · ATM · TXN 14123456 · REF HD7_1", note);
            Assert.DoesNotContain("[TIEN-THUA", note);
        }

        [Fact]
        public async Task LongNote_IsCutTo500Characters_AndTheSurplusTagStaysFirst()
        {
            _h.AddSentInvoice();

            await HandleAsync(Transfer(amount: 2_500_000m, txnRef: "HD7_" + new string('9', 700)));

            var note = Assert.Single(_h.Store.Ledger).GhiChu;
            Assert.Equal(InvoicePaymentPolicy.MaxLyDoLength, note.Length);
            Assert.StartsWith("[TIEN-THUA:500000] [VNPAY] NCB · ATM · TXN 14123456 · REF HD7_", note);
            Assert.Equal(500_000m, PaymentNoteTags.Parse(note).TienThua);
        }

        [Fact]
        public async Task HistoryReason_NamesVnPayAndTheTransactionNumber()
        {
            var hoaDon = _h.AddSentInvoice();

            await HandleAsync(Transfer());

            Assert.Equal("[TU_DONG] VNPay ghi nhận 2.000.000đ, TXN 14123456", Assert.Single(hoaDon.LichSuTrangThaiHoaDons).LyDo);
        }

        [Fact]
        public async Task Overpayment_OnlyAdminSeesTheVnPaySurplusSentence()
        {
            _h.AddSentInvoice();

            var outcome = await HandleAsync(Transfer(amount: 2_500_000m));

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(500_000m, outcome.TienThua);
            var admin = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.AdminId);
            var staff = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId);
            var tenant = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
            Assert.EndsWith(" Khách trả dư 500.000đ qua VNPay, cần xử lý ngoài hệ thống.", admin.NoiDung);
            Assert.DoesNotContain("Khách chuyển dư", admin.NoiDung);
            Assert.DoesNotContain("trả dư", staff.NoiDung);
            Assert.DoesNotContain("trả dư", tenant.NoiDung);
            Assert.All(_h.Store.Notifications, n => Assert.Equal("Đã nhận thanh toán VNPay 2.500.000đ — P101", n.TieuDe));
        }

        [Fact]
        public async Task WaitingRequest_IsCancelledWithTheVnPayReason()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, request.TrangThai);
            Assert.Equal("Đã nhận thanh toán VNPay tự động", request.LichSuTrangThaiYeuCauThanhToanHoaDons.Last().LyDo);
        }

        [Fact]
        public async Task RequestUnderReview_IsKept_AndTheCheckForDuplicateSentenceIsAdded()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            _h.AddProof(request);

            var outcome = await HandleAsync(Transfer());

            Assert.True(outcome.ConMinhChungChoDoiChieu);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.Contains(
                "Hóa đơn còn minh chứng chờ đối chiếu, kiểm tra trùng trước khi xác nhận.",
                Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.StaffId).NoiDung);
        }

        [Fact]
        public async Task SameTransactionNumber_OnAnotherInvoice_IsADuplicateWhenTheKeyExistsOnThatInvoice()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 500_000m, "14123456", Now.AddHours(-1));

            var outcome = await HandleAsync(Transfer());

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task SePayTransfer_KeepsItsOwnWording()
        {
            _h.AddSentInvoice();
            var sePay = new IncomingBankTransfer(
                92704, "MBBank", PayTime, "0123456789", null, null, "TTPTHD7", "in", 2_500_000m, "FT26ABC", "TTPTHD7");

            await HandleAsync(sePay);

            Assert.All(_h.Store.Notifications, n => Assert.Equal("Đã nhận chuyển khoản 2.500.000đ — P101", n.TieuDe));
            var admin = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.AdminId);
            Assert.EndsWith(" Khách chuyển dư 500.000đ, cần xử lý ngoài hệ thống.", admin.NoiDung);
            Assert.StartsWith("[TIEN-THUA:500000] [SEPAY] MBBank", Assert.Single(_h.Store.Ledger).GhiChu);
        }
    }
}
