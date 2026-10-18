using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Panel thanh toán và ảnh QR của khách thuê (spec thanh toán trực tuyến §3.2): luôn theo số còn lại, chỉ đọc.
    public class TenantPaymentPanelServiceTests
    {
        private const int Tenant = PaymentTestHarness.TenantId;
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();

        private TenantPaymentPanelService Service(VietQrSettings? settings = null)
            => new(_h.Store, _h.VietQr, settings ?? _h.VietQrSettings);

        // ---------- Panel ----------

        [Fact]
        public async Task Panel_OwnerWithNothingPaid_ShowsTheFullAmountTransferCodeAndTwelveBanks()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);

            var result = await Service().GetPanelAsync(7, Tenant);

            Assert.True(result.Success, result.Message);
            var panel = result.Data!;
            Assert.Equal(7, panel.HoaDonId);
            Assert.Equal("HD-7", panel.MaHoaDon);
            Assert.Equal("P101", panel.TenPhong);
            Assert.Equal("CN1", panel.TenChiNhanh);
            Assert.Equal((9, 2026), (panel.Thang, panel.Nam));
            Assert.Equal(Now.AddDays(5), panel.HanThanhToanUtc);
            Assert.Equal(2_000_000m, panel.TongTien);
            Assert.Equal(0m, panel.DaThu);
            Assert.Equal(2_000_000m, panel.ConLai);
            Assert.True(panel.CoTheThanhToan);
            Assert.Null(panel.LyDoKhongTheThanhToan);
            Assert.Equal("TTPTHD7", panel.MaChuyenKhoan);
            Assert.Equal("MB", panel.TenNganHang);
            Assert.Equal("0123456789", panel.SoTaiKhoan);
            Assert.Equal("NHA TRO", panel.ChuTaiKhoan);
            Assert.Equal(12, panel.Deeplinks.Count);
            Assert.All(panel.Deeplinks, d =>
            {
                Assert.Contains("&am=2000000&", d.Url);
                Assert.Contains("&tn=TTPTHD7&", d.Url);
            });
            Assert.Empty(panel.CacLanThanhToan);
            Assert.Null(panel.MinhChungChoDoiChieu);
        }

        [Fact]
        public async Task Panel_WithABankName_ShowsTheBankNameInsteadOfTheBankId()
        {
            _h.AddSentInvoice();

            var result = await Service(_h.VietQrSettings with { BankName = "MBBank" }).GetPanelAsync(7, Tenant);

            Assert.Equal("MBBank", result.Data!.TenNganHang);
        }

        [Fact]
        public async Task Panel_PerformsNoWrites_AndTakesNoRowLock()
        {
            _h.AddSentInvoice();

            await Service().GetPanelAsync(7, Tenant);

            Assert.Equal(0, _h.Store.LockCalls);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(0, _h.UnitOfWork.SaveChangesCalls);
            Assert.Empty(_h.Store.Requests);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task Panel_InvoiceOfAnotherTenant_ReturnsNotFound()
        {
            _h.AddSentInvoice();

            var result = await Service().GetPanelAsync(7, nguoiThueId: 999);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task Panel_UnknownInvoice_ReturnsNotFound()
        {
            var result = await Service().GetPanelAsync(404, Tenant);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        public async Task Panel_InvoiceNotSentYet_IsHiddenFromTheTenant(TrangThaiPhatHanhHoaDon status)
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = status;

            var result = await Service().GetPanelAsync(7, Tenant);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task Panel_PartiallyPaid_UsesTheRemainingAmount_IgnoresVoidedRows_AndNumbersInstalments()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var first = _h.AddLedger(7, 500_000m, "GD-1");
            first.NgayThanhToan = Now.AddDays(-3);
            var voided = _h.AddLedger(7, 300_000m, "GD-HUY", isDeleted: true);
            voided.NgayThanhToan = Now.AddDays(-2);
            var second = _h.AddSePayLedger(7, 400_000m, "FT-2", Now.AddDays(-1));

            var panel = (await Service().GetPanelAsync(7, Tenant)).Data!;

            Assert.Equal(900_000m, panel.DaThu);
            Assert.Equal(1_100_000m, panel.ConLai);
            Assert.True(panel.CoTheThanhToan);
            Assert.Equal(new[] { first.LichSuThanhToanId, second.LichSuThanhToanId }, panel.CacLanThanhToan.Select(e => e.LichSuThanhToanId).ToArray());
            Assert.Equal(new[] { 1, 2 }, panel.CacLanThanhToan.Select(e => e.DotThu).ToArray());
            Assert.Equal(new[] { 500_000m, 400_000m }, panel.CacLanThanhToan.Select(e => e.SoTien).ToArray());
            Assert.Equal(new[] { "Tiền mặt", "Chuyển khoản 24/7 (VietQR, tự động)" }, panel.CacLanThanhToan.Select(e => e.PhuongThuc).ToArray());
            Assert.Equal(Now.AddDays(-1), panel.CacLanThanhToan[1].NgayThanhToanUtc);
            Assert.All(panel.Deeplinks, d => Assert.Contains("&am=1100000&", d.Url));
        }

        [Fact]
        public async Task Panel_NumbersInstalmentsByPaymentDate_NotByInsertionOrder()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var later = _h.AddLedger(7, 700_000m, "GD-LATER");
            later.NgayThanhToan = Now.AddDays(-1);
            var earlier = _h.AddLedger(7, 500_000m, "GD-EARLIER");
            earlier.NgayThanhToan = Now.AddDays(-4);

            var entries = (await Service().GetPanelAsync(7, Tenant)).Data!.CacLanThanhToan;

            Assert.Equal(new[] { earlier.LichSuThanhToanId, later.LichSuThanhToanId }, entries.Select(e => e.LichSuThanhToanId).ToArray());
            Assert.Equal(new[] { 1, 2 }, entries.Select(e => e.DotThu).ToArray());
        }

        [Fact]
        public async Task Panel_FullyPaid_HasNoTransferBlock()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            _h.AddLedger(7, 2_000_000m);

            var panel = (await Service().GetPanelAsync(7, Tenant)).Data!;

            Assert.Equal(0m, panel.ConLai);
            Assert.False(panel.CoTheThanhToan);
            Assert.Contains("đã được thanh toán đủ", panel.LyDoKhongTheThanhToan);
            Assert.Empty(panel.Deeplinks);
            Assert.Single(panel.CacLanThanhToan);
        }

        [Fact]
        public async Task Panel_InvoiceCancelledAfterSending_StillOpensButCannotBePaid()
        {
            var invoice = _h.AddSentInvoice();
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.IsDeleted = true;

            var result = await Service().GetPanelAsync(7, Tenant);

            Assert.True(result.Success, result.Message);
            Assert.False(result.Data!.CoTheThanhToan);
            Assert.Equal("Hóa đơn không còn ở trạng thái nhận thanh toán.", result.Data.LyDoKhongTheThanhToan);
            Assert.Empty(result.Data.Deeplinks);
        }

        [Fact]
        public async Task Panel_FractionalInvoiceTotal_CannotBePaid()
        {
            _h.AddSentInvoice(tongTien: 1_233_333.33m);

            var panel = (await Service().GetPanelAsync(7, Tenant)).Data!;

            Assert.False(panel.CoTheThanhToan);
            Assert.Equal("Hóa đơn có số tiền lẻ, vui lòng liên hệ quản lý để lập lại hóa đơn.", panel.LyDoKhongTheThanhToan);
            Assert.Empty(panel.Deeplinks);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Panel_WithoutAReceivingAccount_CannotBePaid(string accountNumber)
        {
            _h.AddSentInvoice();

            var panel = (await Service(_h.VietQrSettings with { AccountNumber = accountNumber }).GetPanelAsync(7, Tenant)).Data!;

            Assert.False(panel.CoTheThanhToan);
            Assert.Equal("Chủ trọ chưa cấu hình tài khoản nhận tiền, vui lòng liên hệ quản lý.", panel.LyDoKhongTheThanhToan);
            Assert.Empty(panel.Deeplinks);
        }

        [Fact]
        public async Task Panel_ReasonsAreCheckedInOrder_CancelledBeforePaidBeforeFractionalBeforeNoAccount()
        {
            var invoice = _h.AddSentInvoice(tongTien: 1_000_000.5m);
            _h.AddLedger(7, 1_000_000.5m);
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            var noAccount = _h.VietQrSettings with { AccountNumber = "" };

            Assert.Equal("Hóa đơn không còn ở trạng thái nhận thanh toán.", (await Service(noAccount).GetPanelAsync(7, Tenant)).Data!.LyDoKhongTheThanhToan);

            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            Assert.Equal("Hóa đơn đã được thanh toán đủ.", (await Service(noAccount).GetPanelAsync(7, Tenant)).Data!.LyDoKhongTheThanhToan);

            _h.Store.Ledger.Clear();
            Assert.Contains("số tiền lẻ", (await Service(noAccount).GetPanelAsync(7, Tenant)).Data!.LyDoKhongTheThanhToan);

            invoice.TongTien = 1_000_000m;
            Assert.Contains("chưa cấu hình tài khoản", (await Service(noAccount).GetPanelAsync(7, Tenant)).Data!.LyDoKhongTheThanhToan);
        }

        // ---------- Minh chứng cũ đang chờ đối chiếu ----------

        [Fact]
        public async Task Panel_LegacyProofWaitingForReview_IsReportedWithTheRequestAmount_AndStillShowsTheTransferBlock()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(7, 2_000_000m);
            var proof = _h.AddProof(request, "FT-OLD");

            var panel = (await Service().GetPanelAsync(7, Tenant)).Data!;

            Assert.NotNull(panel.MinhChungChoDoiChieu);
            Assert.Equal(proof.MinhChungThanhToanHoaDonId, panel.MinhChungChoDoiChieu.MinhChungId);
            Assert.Equal(2_000_000m, panel.MinhChungChoDoiChieu.SoTien);
            Assert.Equal(proof.NgayTao, panel.MinhChungChoDoiChieu.NgayNopUtc);
            Assert.True(panel.CoTheThanhToan);
            Assert.Equal(12, panel.Deeplinks.Count);
        }

        [Fact]
        public async Task Panel_LegacyProofRejectedOrRequestStillWaiting_IsNotReported()
        {
            _h.AddSentInvoice();
            var waitingForPayment = _h.AddRequest(7, 2_000_000m, code: "TT7WAIT");
            var rejected = _h.AddRequest(7, 2_000_000m, createdAt: Now.AddHours(-2), code: "TT7REJ");
            var proof = _h.AddProof(rejected);
            rejected.TuChoiMinhChung(proof, PaymentTestHarness.AdminId, "mờ", Now, Now.AddHours(24));

            var panel = (await Service().GetPanelAsync(7, Tenant)).Data!;

            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, waitingForPayment.TrangThai);
            Assert.Null(panel.MinhChungChoDoiChieu);
        }

        // ---------- QR ----------

        [Fact]
        public async Task Qr_UsesTheRemainingAmount_TheTransferCode_AndAnAsciiFileName()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            _h.AddLedger(7, 500_000m);

            var result = await Service().GetQrAsync(7, Tenant);

            Assert.True(result.Success, result.Message);
            Assert.Equal(1_500_000m, _h.VietQr.LastAmount);
            Assert.Equal("TTPTHD7", _h.VietQr.LastMemo);
            Assert.Equal("QR_Phong_P101_HD7.png", result.Data!.FileName);
            Assert.NotEmpty(result.Data.Png);
        }

        [Fact]
        public async Task Qr_FullyPaid_Fails()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            _h.AddLedger(7, 2_000_000m);

            var result = await Service().GetQrAsync(7, Tenant);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
            Assert.Contains("đã được thanh toán đủ", result.Message);
            Assert.Null(_h.VietQr.LastAmount);
        }

        [Fact]
        public async Task Qr_InvoiceCancelledAfterSending_Fails()
        {
            var invoice = _h.AddSentInvoice();
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.IsDeleted = true;

            var result = await Service().GetQrAsync(7, Tenant);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
            Assert.Null(_h.VietQr.LastAmount);
        }

        [Fact]
        public async Task Qr_FractionalTotalOrNoAccount_Fails()
        {
            var invoice = _h.AddSentInvoice(tongTien: 1_233_333.33m);

            var fractional = await Service().GetQrAsync(7, Tenant);
            invoice.TongTien = 2_000_000m;
            var noAccount = await Service(_h.VietQrSettings with { AccountNumber = "" }).GetQrAsync(7, Tenant);

            Assert.Contains("số tiền lẻ", fractional.Message);
            Assert.Contains("chưa cấu hình tài khoản", noAccount.Message);
            Assert.Null(_h.VietQr.LastAmount);
        }

        [Fact]
        public async Task Qr_InvoiceOfAnotherTenant_ReturnsNotFound()
        {
            _h.AddSentInvoice();

            var result = await Service().GetQrAsync(7, nguoiThueId: 999);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.Null(_h.VietQr.LastAmount);
        }

        [Fact]
        public async Task Qr_InvoiceNotSentYet_ReturnsNotFound()
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot;

            var result = await Service().GetQrAsync(7, Tenant);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }
    }
}
