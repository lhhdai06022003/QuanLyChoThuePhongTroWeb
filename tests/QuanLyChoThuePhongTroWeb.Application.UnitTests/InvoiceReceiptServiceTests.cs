using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Biên lai web của một dòng sổ (spec thanh toán trực tuyến §3.4): đợt, lũy kế, còn nợ, tiền thừa, mã tham chiếu, phân quyền.
    // Gửi lại email biên lai (§6.3, §7.2): quyền InvoiceSend theo chi nhánh, dòng đã hủy, email khách, lỗi gửi thư.
    public class InvoiceReceiptServiceTests
    {
        private const int Tenant = PaymentTestHarness.TenantId;
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();
        private readonly RecordingPaymentReceiptEmailService _email = new();
        private readonly OnlinePaymentOptions _options = new() { PublicBaseUrl = "https://tro.test" };
        private readonly CapturingLogger<InvoiceReceiptService> _log = new();

        private InvoiceReceiptService Service(IEmployeeAccessService? access = null)
            => new(_h.Store, access ?? _h.Access, _email, _options, _log);

        // Hóa đơn 7 (2.000.000đ) đã trả 500.000 rồi 700.000, mỗi lần một ngày khác nhau.
        private (int First, int Second) AddTwoInstalments()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var first = _h.AddLedger(7, 500_000m, "GD-1");
            first.NgayThanhToan = Now.AddDays(-3);
            var second = _h.AddLedger(7, 700_000m, "GD-2");
            second.NgayThanhToan = Now.AddDays(-2);
            return (first.LichSuThanhToanId, second.LichSuThanhToanId);
        }

        // ---------- Số liệu biên lai ----------

        [Fact]
        public async Task Tenant_SecondInstalment_CarriesTheRunningTotalAndTheRemainingDebt()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().GetForTenantAsync(second, Tenant);

            Assert.True(result.Success, result.Message);
            var receipt = result.Data!;
            Assert.Equal(second, receipt.LichSuThanhToanId);
            Assert.Equal(7, receipt.HoaDonId);
            Assert.Equal("HD-7", receipt.MaHoaDon);
            Assert.Equal("CN1", receipt.TenChiNhanh);
            Assert.Equal("1 Đường A", receipt.DiaChiChiNhanh);
            Assert.Equal("P101", receipt.TenPhong);
            Assert.Equal("Nguyễn Văn A", receipt.TenNguoiThanhToan);
            Assert.Equal((9, 2026), (receipt.Thang, receipt.Nam));
            Assert.Equal(Now.AddDays(-2), receipt.NgayThanhToanUtc);
            Assert.Equal("Tiền mặt", receipt.PhuongThuc);
            Assert.Equal("GD-2", receipt.MaGiaoDich);
            Assert.Equal(2_000_000m, receipt.TongTien);
            Assert.Equal(700_000m, receipt.SoTienLanNay);
            Assert.Equal(2, receipt.DotThu);
            Assert.Equal(1_200_000m, receipt.LuyKeDaThanhToan);
            Assert.Equal(800_000m, receipt.ConNo);
            Assert.False(receipt.DaThuDu);
            Assert.Equal(0m, receipt.TienThua);
        }

        [Fact]
        public async Task Tenant_FirstInstalment_IsNumberedOne_AndIgnoresLaterPayments()
        {
            var (first, _) = AddTwoInstalments();

            var receipt = (await Service().GetForTenantAsync(first, Tenant)).Data!;

            Assert.Equal(1, receipt.DotThu);
            Assert.Equal(500_000m, receipt.LuyKeDaThanhToan);
            Assert.Equal(1_500_000m, receipt.ConNo);
            Assert.False(receipt.DaThuDu);
        }

        [Fact]
        public async Task Tenant_InstalmentThatCompletesTheInvoice_IsMarkedFullyPaid()
        {
            var (_, second) = AddTwoInstalments();
            var last = _h.AddLedger(7, 800_000m, "GD-3");
            last.NgayThanhToan = Now.AddDays(-1);

            var receipt = (await Service().GetForTenantAsync(last.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal(3, receipt.DotThu);
            Assert.Equal(2_000_000m, receipt.LuyKeDaThanhToan);
            Assert.Equal(0m, receipt.ConNo);
            Assert.True(receipt.DaThuDu);
            Assert.False((await Service().GetForTenantAsync(second, Tenant)).Data!.DaThuDu);
        }

        [Fact]
        public async Task Tenant_VoidedRowBeforeThisOne_IsNotCounted()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var voided = _h.AddLedger(7, 300_000m, "GD-HUY", isDeleted: true);
            voided.NgayThanhToan = Now.AddDays(-5);
            var only = _h.AddLedger(7, 500_000m, "GD-1");
            only.NgayThanhToan = Now.AddDays(-4);

            var receipt = (await Service().GetForTenantAsync(only.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal(1, receipt.DotThu);
            Assert.Equal(500_000m, receipt.LuyKeDaThanhToan);
        }

        [Fact]
        public async Task Tenant_RowWithTheSurplusTag_ReportsTheSurplus()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var row = _h.AddLedger(7, 2_000_000m, "GD-1");
            row.GhiChu = PaymentNoteTags.TienThua(100_000m, "chuyển dư");

            var receipt = (await Service().GetForTenantAsync(row.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal(100_000m, receipt.TienThua);
            Assert.True(receipt.DaThuDu);
            Assert.Equal(0m, receipt.ConNo);
        }

        // ---------- Phương thức và mã tham chiếu ----------

        [Fact]
        public async Task SePayRow_HasTheAutomaticMethodAndTheBankReference()
        {
            _h.AddSentInvoice();
            var row = _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", Now.AddDays(-1));

            var receipt = (await Service().GetForTenantAsync(row.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal("Chuyển khoản 24/7 (VietQR, tự động)", receipt.PhuongThuc);
            Assert.Equal("SEPAY-FT26ABC", receipt.MaGiaoDich);
            Assert.Equal("FT26ABC", receipt.MaThamChieu);
        }

        [Fact]
        public async Task SePayRow_ReferenceIsShownAsStored_UppercasedWithoutSpaces()
        {
            _h.AddSentInvoice();
            var row = _h.AddSePayLedger(7, 2_000_000m, "ft 26 abc", Now.AddDays(-1));

            var receipt = (await Service().GetForTenantAsync(row.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal("FT26ABC", receipt.MaThamChieu);
        }

        [Fact]
        public async Task SePayRowKeyedByTheSePayId_HasNoBankReference()
        {
            _h.AddSentInvoice();
            var row = _h.AddSePayLedger(7, 2_000_000m, "ID-92704", Now.AddDays(-1));

            var receipt = (await Service().GetForTenantAsync(row.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal("SEPAY-ID-92704", receipt.MaGiaoDich);
            Assert.Null(receipt.MaThamChieu);
            Assert.Equal("Chuyển khoản 24/7 (VietQR, tự động)", receipt.PhuongThuc);
        }

        [Fact]
        public async Task CashRow_HasNoBankReference()
        {
            _h.AddSentInvoice();
            var row = _h.AddLedger(7, 2_000_000m, "TM-001");

            var receipt = (await Service().GetForTenantAsync(row.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal("Tiền mặt", receipt.PhuongThuc);
            Assert.Equal("TM-001", receipt.MaGiaoDich);
            Assert.Null(receipt.MaThamChieu);
        }

        [Fact]
        public async Task ManualBankTransferRow_IsLabelledAsATransfer_WithoutABankReference()
        {
            _h.AddSentInvoice();
            var row = _h.AddLedger(7, 2_000_000m, "FT-TAY");
            row.PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan;

            var receipt = (await Service().GetForTenantAsync(row.LichSuThanhToanId, Tenant)).Data!;

            Assert.Equal("Chuyển khoản", receipt.PhuongThuc);
            Assert.Null(receipt.MaThamChieu);
        }

        // ---------- Khách thuê ----------

        [Fact]
        public async Task Tenant_AnotherTenantsReceipt_ReturnsNotFound()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().GetForTenantAsync(second, nguoiThueId: 999);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.Equal("Không tìm thấy biên lai.", result.Message);
        }

        [Fact]
        public async Task Tenant_VoidedRow_ReturnsNotFound()
        {
            _h.AddSentInvoice();
            var voided = _h.AddLedger(7, 500_000m, "GD-HUY", isDeleted: true);

            var result = await Service().GetForTenantAsync(voided.LichSuThanhToanId, Tenant);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task Tenant_UnknownRow_OrNoTenantId_ReturnsNotFound()
        {
            var (_, second) = AddTwoInstalments();

            Assert.Equal(ServiceErrorKind.NotFound, (await Service().GetForTenantAsync(404, Tenant)).ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await Service().GetForTenantAsync(second, nguoiThueId: 0)).ErrorKind);
        }

        [Fact]
        public async Task Tenant_ReceiptOfAnInvoiceThatWasNeverSent_ReturnsNotFound()
        {
            var (_, second) = AddTwoInstalments();
            _h.Store.Invoices[7].TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot;

            var result = await Service().GetForTenantAsync(second, Tenant);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task Tenant_ReceiptOfAnInvoiceCancelledAfterSending_StillOpens()
        {
            var (_, second) = AddTwoInstalments();
            var invoice = _h.Store.Invoices[7];
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.IsDeleted = true;

            var result = await Service().GetForTenantAsync(second, Tenant);

            Assert.True(result.Success, result.Message);
        }

        // ---------- Quản lý ----------

        [Fact]
        public async Task Employee_StaffOfTheSameBranch_CanOpenTheReceipt()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().GetForEmployeeAsync(PaymentTestHarness.StaffId, second);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, result.Data!.DotThu);
        }

        [Fact]
        public async Task Employee_StaffOfAnotherBranch_IsForbidden()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().GetForEmployeeAsync(PaymentTestHarness.OtherStaffId, second);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal("Bạn không có quyền xem biên lai này.", result.Message);
        }

        [Fact]
        public async Task Employee_Admin_CanOpenAnyBranchesReceipt()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().GetForEmployeeAsync(PaymentTestHarness.AdminId, second);

            Assert.True(result.Success, result.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Employee_WithoutAValidActorId_IsForbidden_FailClosed(int actorId)
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().GetForEmployeeAsync(actorId, second);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
        }

        [Fact]
        public async Task Employee_UnknownUser_IsForbidden()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().GetForEmployeeAsync(actorId: 4040, second);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
        }

        [Fact]
        public async Task Employee_VoidedRowOrUnknownRow_ReturnsNotFound()
        {
            _h.AddSentInvoice();
            var voided = _h.AddLedger(7, 500_000m, "GD-HUY", isDeleted: true);

            Assert.Equal(ServiceErrorKind.NotFound, (await Service().GetForEmployeeAsync(PaymentTestHarness.AdminId, voided.LichSuThanhToanId)).ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await Service().GetForEmployeeAsync(PaymentTestHarness.AdminId, 404)).ErrorKind);
        }

        // ---------- Gửi lại biên lai ----------

        [Fact]
        public async Task Resend_StaffOfTheSameBranch_SendsOneReceiptEmailToTheContractHolder()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Đã gửi lại biên lai.", result.Message);
            Assert.Equal(second, result.Data!.LichSuThanhToanId);
            Assert.Equal(7, result.Data.HoaDonId);

            var (toEmail, data) = Assert.Single(_email.ReceiptCalls);
            Assert.Equal("khach@test.vn", toEmail);
            Assert.Equal(second, data.LichSuThanhToanId);
            Assert.Equal(7, data.HoaDonId);
            Assert.Equal("HD-7", data.MaHoaDon);
            Assert.Equal($"https://tro.test/KhachThue/HoaDon/BienLai/{second}", data.LinkXemBienLai);
            Assert.Equal("Tiền mặt", data.PhuongThuc);
            Assert.Null(data.MaThamChieu);
        }

        [Fact]
        public async Task Resend_CarriesTheSameInstalmentFiguresAsTheWebReceipt()
        {
            var (_, second) = AddTwoInstalments();

            await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            var data = Assert.Single(_email.ReceiptCalls).Data;
            Assert.Equal(2, data.DotThu);
            Assert.Equal(2_000_000m, data.TongTien);
            Assert.Equal(700_000m, data.SoTienLanNay);
            Assert.Equal(1_200_000m, data.LuyKeDaThanhToan);
            Assert.Equal(800_000m, data.ConNo);
            Assert.False(data.DaThuDu);
        }

        [Fact]
        public async Task Resend_SePayRowKeyedByTheBankReference_CarriesTheReferenceAndTheAutomaticMethod()
        {
            _h.AddSentInvoice();
            var row = _h.AddSePayLedger(7, 2_000_000m, "ft 26 abc", Now.AddDays(-1));

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, row.LichSuThanhToanId);

            Assert.True(result.Success, result.Message);
            var data = Assert.Single(_email.ReceiptCalls).Data;
            Assert.Equal("FT26ABC", data.MaThamChieu);
            Assert.Equal("Chuyển khoản 24/7 (VietQR, tự động)", data.PhuongThuc);
        }

        [Fact]
        public async Task Resend_SePayRowKeyedByTheSePayId_HasNoBankReference()
        {
            _h.AddSentInvoice();
            var row = _h.AddSePayLedger(7, 2_000_000m, "ID-92704", Now.AddDays(-1));

            await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, row.LichSuThanhToanId);

            Assert.Null(Assert.Single(_email.ReceiptCalls).Data.MaThamChieu);
        }

        [Fact]
        public async Task Resend_CashAndManualTransferRows_AreAllowed_WithoutABankReference()
        {
            _h.AddSentInvoice();
            var cash = _h.AddLedger(7, 500_000m, "TM-001");
            var transfer = _h.AddLedger(7, 500_000m, "FT-TAY");
            transfer.PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan;

            Assert.True((await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, cash.LichSuThanhToanId)).Success);
            Assert.True((await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, transfer.LichSuThanhToanId)).Success);

            Assert.Equal(2, _email.ReceiptCalls.Count);
            Assert.Equal("Tiền mặt", _email.ReceiptCalls[0].Data.PhuongThuc);
            Assert.Equal("Chuyển khoản", _email.ReceiptCalls[1].Data.PhuongThuc);
            Assert.All(_email.ReceiptCalls, call => Assert.Null(call.Data.MaThamChieu));
        }

        [Fact]
        public async Task Resend_Admin_CanSendAnyBranchesReceipt()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.AdminId, second);

            Assert.True(result.Success, result.Message);
            Assert.Single(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Resend_StaffOfAnotherBranch_IsForbidden_AndNothingIsSent()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.OtherStaffId, second);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Equal("Bạn không có quyền gửi biên lai này.", result.Message);
            Assert.Empty(_email.ReceiptCalls);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Resend_WithoutAValidActorId_IsForbidden_FailClosed(int actorId)
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().ResendForEmployeeAsync(actorId, second);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Empty(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Resend_UnknownUser_IsForbidden()
        {
            var (_, second) = AddTwoInstalments();

            var result = await Service().ResendForEmployeeAsync(actorId: 4040, second);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.Empty(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Resend_ChecksTheInvoiceSendAction_NotTheReadOrResendEmailAction()
        {
            var (_, second) = AddTwoInstalments();
            var access = new FakeEmployeeAccessService();
            access.Permissions.Add((20, PaymentTestHarness.Branch, EmployeeActionCodes.InvoiceRead));
            access.Permissions.Add((20, PaymentTestHarness.Branch, EmployeeActionCodes.InvoiceResendEmail));
            access.Permissions.Add((21, PaymentTestHarness.Branch, EmployeeActionCodes.InvoiceSend));

            var withoutSend = await Service(access).ResendForEmployeeAsync(20, second);
            var withSend = await Service(access).ResendForEmployeeAsync(21, second);

            Assert.Equal(ServiceErrorKind.Forbidden, withoutSend.ErrorKind);
            Assert.True(withSend.Success, withSend.Message);
            Assert.Single(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Resend_VoidedRow_FailsWithoutSending()
        {
            _h.AddSentInvoice();
            var voided = _h.AddLedger(7, 500_000m, "GD-HUY", isDeleted: true);

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.AdminId, voided.LichSuThanhToanId);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
            Assert.Equal("Khoản thanh toán đã hủy ghi nhận, không gửi biên lai.", result.Message);
            Assert.Empty(_email.ReceiptCalls);
        }

        [Fact]
        public async Task Resend_VoidedRowOfAnotherBranch_IsForbidden_NotRevealingItsState()
        {
            _h.AddSentInvoice();
            var voided = _h.AddLedger(7, 500_000m, "GD-HUY", isDeleted: true);

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.OtherStaffId, voided.LichSuThanhToanId);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
        }

        [Fact]
        public async Task Resend_UnknownRow_ReturnsNotFound()
        {
            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.AdminId, 404);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.Equal("Không tìm thấy biên lai.", result.Message);
            Assert.Empty(_email.ReceiptCalls);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("khong-phai-email")]
        public async Task Resend_TenantWithoutAValidEmail_Fails_AndLogsAWarningWithoutTheAddress(string? email)
        {
            var (_, second) = AddTwoInstalments();
            _h.Store.Meta[7] = _h.Store.Meta[7] with { EmailKhachThue = email };

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            Assert.False(result.Success);
            Assert.Equal("Khách thuê chưa có email hợp lệ.", result.Message);
            Assert.Empty(_email.ReceiptCalls);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("7", entry.Message);
            Assert.Contains(second.ToString(), entry.Message);
            if (!string.IsNullOrWhiteSpace(email))
            {
                Assert.DoesNotContain(email.Trim(), entry.Message);
            }
        }

        [Fact]
        public async Task Resend_TrimsTheAddress()
        {
            var (_, second) = AddTwoInstalments();
            _h.Store.Meta[7] = _h.Store.Meta[7] with { EmailKhachThue = "  khach@test.vn  " };

            await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            Assert.Equal("khach@test.vn", Assert.Single(_email.ReceiptCalls).ToEmail);
        }

        [Fact]
        public async Task Resend_WhenTheEmailServiceReportsAnError_Fails_AndLogsAWarningWithoutTheAddress()
        {
            var (_, second) = AddTwoInstalments();
            _email.Result = (false, "Không gửi được email biên lai.");

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            Assert.False(result.Success);
            Assert.Equal("Không gửi được email biên lai. Vui lòng thử lại sau.", result.Message);
            Assert.Single(_email.ReceiptCalls);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.DoesNotContain("khach@test.vn", entry.Message);
        }

        [Fact]
        public async Task Resend_WhenTheEmailServiceThrows_Fails_AndLogsAWarningWithoutTheAddress()
        {
            var (_, second) = AddTwoInstalments();
            _email.Throw = true;

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            Assert.False(result.Success);
            Assert.Equal("Không gửi được email biên lai. Vui lòng thử lại sau.", result.Message);
            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(nameof(InvalidOperationException), entry.Message);
            Assert.DoesNotContain("khach@test.vn", entry.Message);
            Assert.DoesNotContain("SMTP down", entry.Message);
        }

        [Fact]
        public async Task Resend_WhenTheCallerCancels_TheCancellationPropagates()
        {
            var (_, second) = AddTwoInstalments();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var service = new InvoiceReceiptService(_h.Store, _h.Access, new CancellingEmailService(), _options, _log);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.ResendForEmployeeAsync(PaymentTestHarness.StaffId, second, cts.Token));
        }

        [Fact]
        public async Task Resend_WithoutAPublicBaseUrl_StillSends_WithoutTheViewLink()
        {
            var (_, second) = AddTwoInstalments();
            _options.PublicBaseUrl = string.Empty;

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            Assert.True(result.Success, result.Message);
            Assert.Null(Assert.Single(_email.ReceiptCalls).Data.LinkXemBienLai);
        }

        [Fact]
        public async Task Resend_LogsTheActorAtInformation_WithoutAnyWrite()
        {
            var (_, second) = AddTwoInstalments();
            var ledgerCountBefore = _h.Store.Ledger.Count;

            await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, second);

            var entry = Assert.Single(_log.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains(PaymentTestHarness.StaffId.ToString(), entry.Message);
            Assert.Contains(second.ToString(), entry.Message);
            Assert.Equal(ledgerCountBefore, _h.Store.Ledger.Count);
            Assert.Equal(0, _h.UnitOfWork.SaveChangesCalls);
            Assert.Empty(_h.UnitOfWork.Transactions);
        }

        private sealed class CancellingEmailService : IEmailService
        {
            public Task<(bool IsSuccess, string ErrorMessage)> SendPaymentReceiptAsync(string toEmail, PaymentReceiptEmail data)
                => throw new OperationCanceledException();

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
                => throw new NotSupportedException();

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
                => throw new NotSupportedException();
        }
    }
}
