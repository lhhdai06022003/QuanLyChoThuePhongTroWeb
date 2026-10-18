using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Kiểm hành vi nhất quán giữa các nơi cùng hiển thị một con số: QR, deeplink, panel, biên lai web và email biên lai
    // (thanh toán trực tuyến đợt 1 phần 3, T1, T3). Bổ sung cho TenantPaymentPanelServiceTests và InvoiceReceiptServiceTests.
    public class TenantOnlinePaymentConsistencyTests
    {
        private const int Tenant = PaymentTestHarness.TenantId;
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();

        private TenantPaymentPanelService Panel() => new(_h.Store, _h.VietQr, _h.VietQrSettings);
        private InvoiceReceiptService Receipts()
            => new(_h.Store, _h.Access, new RecordingPaymentReceiptEmailService(), new OnlinePaymentOptions(), NullLogger<InvoiceReceiptService>.Instance);

        private static string QueryValue(string url, string key)
        {
            var query = new Uri(url).Query.TrimStart('?').Split('&');
            var pair = query.Single(p => p.StartsWith(key + "=", StringComparison.Ordinal));
            return Uri.UnescapeDataString(pair[(key.Length + 1)..]);
        }

        // Hóa đơn 7 (2.000.000đ): 500.000 tiền mặt, 700.000 SePay, một dòng 300.000 đã hủy ghi nhận ở giữa.
        private List<int> SeedMixedLedger()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var first = _h.AddLedger(7, 500_000m, "GD-1");
            first.NgayThanhToan = Now.AddDays(-4);
            var voided = _h.AddLedger(7, 300_000m, "GD-VOID", isDeleted: true);
            voided.NgayThanhToan = Now.AddDays(-3);
            var second = _h.AddSePayLedger(7, 700_000m, "FT26XYZ", Now.AddDays(-2));
            return new List<int> { first.LichSuThanhToanId, second.LichSuThanhToanId };
        }

        // ---------- T1: một con số duy nhất ----------

        [Fact]
        public async Task RemainingAmount_IsTheSameInThePanel_TheQrImage_AndEveryDeeplink()
        {
            SeedMixedLedger();

            var panel = (await Panel().GetPanelAsync(7, Tenant)).Data!;
            var qr = await Panel().GetQrAsync(7, Tenant);

            Assert.Equal(800_000m, panel.ConLai);
            Assert.Equal(panel.TongTien - panel.DaThu, panel.ConLai);
            Assert.True(qr.Success, qr.Message);
            Assert.Equal(panel.ConLai, _h.VietQr.LastAmount);
            Assert.Equal(12, panel.Deeplinks.Count);
            Assert.All(panel.Deeplinks, d => Assert.Equal(panel.ConLai.ToString("0", CultureInfo.InvariantCulture), QueryValue(d.Url, "am")));
            Assert.All(panel.Deeplinks, d => Assert.Equal(panel.MaChuyenKhoan, QueryValue(d.Url, "tn")));
            Assert.Equal(_h.VietQr.LastMemo, panel.MaChuyenKhoan);
        }

        [Fact]
        public async Task PartialPaymentSettingsOfTheInvoice_DoNotChangeTheTransferAmount()
        {
            // T1: ChoPhepThanhToanMotPhan và SoTienThanhToanToiThieu không áp vào chuyển khoản; QR luôn là số còn lại.
            _h.AddSentInvoice(tongTien: 2_000_000m, choPhepMotPhan: true, toiThieu: 1_500_000m);
            _h.AddLedger(7, 100_000m, "GD-1");

            var panel = (await Panel().GetPanelAsync(7, Tenant)).Data!;
            var qr = await Panel().GetQrAsync(7, Tenant);

            Assert.True(panel.CoTheThanhToan);
            Assert.Equal(1_900_000m, panel.ConLai);
            Assert.True(qr.Success, qr.Message);
            Assert.Equal(1_900_000m, _h.VietQr.LastAmount);

            var off = new PaymentTestHarness();
            off.AddSentInvoice(tongTien: 2_000_000m, choPhepMotPhan: false);
            off.AddLedger(7, 100_000m, "GD-1");
            Assert.Equal(1_900_000m, (await new TenantPaymentPanelService(off.Store, off.VietQr, off.VietQrSettings).GetPanelAsync(7, Tenant)).Data!.ConLai);
        }

        [Fact]
        public async Task OneMoreCentOfLedger_RemovesTheBlock_AndVoidingTheRowBringsTheFullAmountBack()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var row = _h.AddLedger(7, 2_000_000m, "GD-FULL");

            var paid = (await Panel().GetPanelAsync(7, Tenant)).Data!;
            Assert.False(paid.CoTheThanhToan);
            Assert.Empty(paid.Deeplinks);

            // Admin hủy ghi nhận khoản đó: khách lại phải thấy QR với đủ số tiền, không còn dòng nào trong danh sách đợt.
            row.IsDeleted = true;
            var reopened = (await Panel().GetPanelAsync(7, Tenant)).Data!;
            Assert.True(reopened.CoTheThanhToan);
            Assert.Equal(2_000_000m, reopened.ConLai);
            Assert.Empty(reopened.CacLanThanhToan);
            Assert.Equal(12, reopened.Deeplinks.Count);
        }

        // ---------- Panel và biên lai cùng một cách đánh số đợt ----------

        [Fact]
        public async Task PanelInstalments_AgreeWithEachReceipt()
        {
            var ids = SeedMixedLedger();

            var panel = (await Panel().GetPanelAsync(7, Tenant)).Data!;

            Assert.Equal(ids, panel.CacLanThanhToan.Select(e => e.LichSuThanhToanId).ToList());
            Assert.Equal(panel.DaThu, panel.CacLanThanhToan.Sum(e => e.SoTien));
            foreach (var entry in panel.CacLanThanhToan)
            {
                var receipt = (await Receipts().GetForTenantAsync(entry.LichSuThanhToanId, Tenant)).Data!;
                Assert.Equal(entry.DotThu, receipt.DotThu);
                Assert.Equal(entry.SoTien, receipt.SoTienLanNay);
                Assert.Equal(entry.NgayThanhToanUtc, receipt.NgayThanhToanUtc);
                Assert.Equal(entry.PhuongThuc, receipt.PhuongThuc);
            }

            var lastReceipt = (await Receipts().GetForTenantAsync(ids[^1], Tenant)).Data!;
            Assert.Equal(panel.DaThu, lastReceipt.LuyKeDaThanhToan);
            Assert.Equal(panel.ConLai, lastReceipt.ConNo);
        }

        // ---------- T3: công thức giống hệt email biên lai ----------

        [Fact]
        public async Task WebReceipt_UsesTheSameFiguresAsTheReceiptEmail_ForEveryInstalment()
        {
            _h.AddSentInvoice(tongTien: 2_000_000m);
            var a = _h.AddLedger(7, 500_000m, "GD-A");
            a.NgayThanhToan = Now.AddDays(-5);
            var b = _h.AddSePayLedger(7, 700_000m, "FT26ONE", Now.AddDays(-3));
            var voided = _h.AddLedger(7, 111_000m, "GD-VOID", isDeleted: true);
            voided.NgayThanhToan = Now.AddDays(-2);
            var c = _h.AddLedger(7, 900_000m, "GD-C");
            c.NgayThanhToan = Now.AddDays(-1);
            c.GhiChu = PaymentNoteTags.TienThua(100_000m, "chuyển dư");

            foreach (var id in new[] { a.LichSuThanhToanId, b.LichSuThanhToanId, c.LichSuThanhToanId })
            {
                var snapshot = (await _h.Store.GetReceiptSnapshotAsync(id))!;
                var email = PaymentNotificationFormatter.BuildReceiptEmail(snapshot, maThamChieu: null, linkXemBienLai: null);
                var web = (await Receipts().GetForTenantAsync(id, Tenant)).Data!;

                Assert.Equal(email.DotThu, web.DotThu);
                Assert.Equal(email.LuyKeDaThanhToan, web.LuyKeDaThanhToan);
                Assert.Equal(email.ConNo, web.ConNo);
                Assert.Equal(email.TienThua, web.TienThua);
                Assert.Equal(email.DaThuDu, web.DaThuDu);
                Assert.Equal(email.SoTienLanNay, web.SoTienLanNay);
                Assert.Equal(email.TongTien, web.TongTien);
                Assert.Equal(email.PhuongThuc, web.PhuongThuc);
                Assert.Equal((email.Thang, email.Nam), (web.Thang, web.Nam));
                Assert.Equal(email.TenPhong, web.TenPhong);
                Assert.Equal(email.TenChiNhanh, web.TenChiNhanh);
                Assert.Equal(email.TenNguoiThanhToan, web.TenNguoiThanhToan);
                Assert.Equal(email.NgayThanhToanVn, web.NgayThanhToanUtc.AddHours(7).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
            }

            var last = (await Receipts().GetForTenantAsync(c.LichSuThanhToanId, Tenant)).Data!;
            Assert.Equal(3, last.DotThu);
            Assert.Equal(2_100_000m, last.LuyKeDaThanhToan);
            Assert.Equal(0m, last.ConNo);
            Assert.Equal(100_000m, last.TienThua);
            Assert.True(last.DaThuDu);
        }

        [Fact]
        public async Task Overpaid_DebtNeverGoesNegative()
        {
            _h.AddSentInvoice(tongTien: 1_000_000m);
            var row = _h.AddLedger(7, 1_200_000m, "GD-OVER");
            row.GhiChu = PaymentNoteTags.TienThua(200_000m, "dư");

            var receipt = (await Receipts().GetForTenantAsync(row.LichSuThanhToanId, Tenant)).Data!;
            var panel = (await Panel().GetPanelAsync(7, Tenant)).Data!;

            Assert.Equal(0m, receipt.ConNo);
            Assert.True(receipt.DaThuDu);
            // Panel: số còn lại âm không được sinh QR mà cũng không được lộ ra thành "thanh toán được".
            Assert.False(panel.CoTheThanhToan);
            Assert.Empty(panel.Deeplinks);
            Assert.False((await Panel().GetQrAsync(7, Tenant)).Success);
        }

        // ---------- Phân quyền: id người thuê không hợp lệ ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public async Task NonPositiveTenantId_IsNotFound_ForPanelQrAndReceipt(int nguoiThueId)
        {
            _h.AddSentInvoice();
            var row = _h.AddLedger(7, 500_000m);

            Assert.Equal(ServiceErrorKind.NotFound, (await Panel().GetPanelAsync(7, nguoiThueId)).ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await Panel().GetQrAsync(7, nguoiThueId)).ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await Receipts().GetForTenantAsync(row.LichSuThanhToanId, nguoiThueId)).ErrorKind);
        }

        [Fact]
        public async Task TenantCannotReadTheManagerReceiptPath_AndStaffIdIsNotATenantId()
        {
            // Nhân viên có id trùng số với người thuê vẫn chỉ đi qua đường quyền chi nhánh; người thuê id 10 (= StaffId) không sở hữu hóa đơn.
            _h.AddSentInvoice();
            var row = _h.AddLedger(7, 500_000m);

            Assert.Equal(ServiceErrorKind.NotFound, (await Receipts().GetForTenantAsync(row.LichSuThanhToanId, PaymentTestHarness.StaffId)).ErrorKind);
            Assert.Equal(ServiceErrorKind.NotFound, (await Panel().GetPanelAsync(7, PaymentTestHarness.AdminId)).ErrorKind);
        }

        [Fact]
        public async Task EmployeeWithNoBranchAssignment_IsForbidden()
        {
            _h.AddSentInvoice();
            var row = _h.AddLedger(7, 500_000m);

            var result = await Receipts().GetForEmployeeAsync(actorId: 999, row.LichSuThanhToanId);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
        }

        [Fact]
        public async Task EmployeeOfTheBranch_GetsTheSameFiguresAsTheTenant()
        {
            var ids = SeedMixedLedger();

            var tenantView = (await Receipts().GetForTenantAsync(ids[1], Tenant)).Data!;
            var staffView = (await Receipts().GetForEmployeeAsync(PaymentTestHarness.StaffId, ids[1])).Data!;
            var adminView = (await Receipts().GetForEmployeeAsync(PaymentTestHarness.AdminId, ids[1])).Data!;

            Assert.Equivalent(tenantView, staffView);
            Assert.Equivalent(tenantView, adminView);
        }

        // ---------- Panel không ghi gì dù được gọi nhiều lần ----------

        [Fact]
        public async Task OpeningThePanelAndQrRepeatedly_CreatesNoRequestAndChangesNoLedger()
        {
            SeedMixedLedger();
            var ledgerBefore = _h.Store.Ledger.Select(l => (l.LichSuThanhToanId, l.SoTienThanhToan, l.IsDeleted)).ToList();
            var requestsBefore = _h.Store.Requests.Count;

            for (var i = 0; i < 3; i++)
            {
                await Panel().GetPanelAsync(7, Tenant);
                await Panel().GetQrAsync(7, Tenant);
            }

            Assert.Equal(requestsBefore, _h.Store.Requests.Count);
            Assert.Equal(ledgerBefore, _h.Store.Ledger.Select(l => (l.LichSuThanhToanId, l.SoTienThanhToan, l.IsDeleted)).ToList());
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(0, _h.UnitOfWork.SaveChangesCalls);
        }

        // ---------- Tên file QR: tên phòng do người dùng nhập, không được xuống header Content-Disposition ----------

        [Theory]
        [InlineData("a\r\nContent-Type: text/html")]
        [InlineData("\"; filename=\"evil.exe")]
        [InlineData("P1\u202Egnp.exe")]
        [InlineData("Ｐ１０１")]
        [InlineData("🏠 Phòng 1")]
        [InlineData("../../etc/passwd")]
        [InlineData("C:\\Windows\\system32")]
        public void QrFileName_NeverContainsAnythingOutsideTheAsciiWhitelist(string hostile)
        {
            var name = PaymentQrFileName.Build(hostile, 42);

            Assert.Matches(@"^QR_Phong_[A-Za-z0-9_-]*HD42\.png$", name);
        }
    }
}
