using System;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Biên lai web và gửi lại email của dòng sổ VNPay (spec VNPay §7.1): nhãn phương thức và mã tham chiếu vnp_TransactionNo.
    public class InvoiceReceiptServiceVnPayTests
    {
        private const int Tenant = PaymentTestHarness.TenantId;
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();
        private readonly RecordingPaymentReceiptEmailService _email = new();
        private readonly OnlinePaymentOptions _options = new() { PublicBaseUrl = "https://tro.test" };
        private readonly CapturingLogger<InvoiceReceiptService> _log = new();

        private InvoiceReceiptService Service() => new(_h.Store, _h.Access, _email, _options, _log);

        private int AddVnPayRow()
        {
            _h.AddSentInvoice();
            return _h.AddVnPayLedger(7, 2_000_000m, "14123456", Now.AddDays(-1)).LichSuThanhToanId;
        }

        [Fact]
        public async Task Tenant_Receipt_ShowsTheVnPayMethodAndTheTransactionNumber()
        {
            var id = AddVnPayRow();

            var result = await Service().GetForTenantAsync(id, Tenant);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Thẻ ATM / Visa / MasterCard (VNPay)", result.Data!.PhuongThuc);
            Assert.Equal("14123456", result.Data.MaThamChieu);
            Assert.Equal("VNPAY-14123456", result.Data.MaGiaoDich);
        }

        [Fact]
        public async Task Employee_Receipt_ShowsTheVnPayMethodAndTheTransactionNumber()
        {
            var id = AddVnPayRow();

            var result = await Service().GetForEmployeeAsync(PaymentTestHarness.StaffId, id);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Thẻ ATM / Visa / MasterCard (VNPay)", result.Data!.PhuongThuc);
            Assert.Equal("14123456", result.Data.MaThamChieu);
        }

        [Fact]
        public async Task Resend_VnPayRow_CarriesTheTransactionNumberAndTheVnPayMethod()
        {
            var id = AddVnPayRow();

            var result = await Service().ResendForEmployeeAsync(PaymentTestHarness.StaffId, id);

            Assert.True(result.Success, result.Message);
            var data = Assert.Single(_email.ReceiptCalls).Data;
            Assert.Equal("14123456", data.MaThamChieu);
            Assert.Equal("Thẻ ATM / Visa / MasterCard (VNPay)", data.PhuongThuc);
        }
    }
}
