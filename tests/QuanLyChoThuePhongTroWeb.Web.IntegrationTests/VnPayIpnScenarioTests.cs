using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Bổ sung cho VnPayIpnTests (spec VNPay §5.2, §7.2, §11.2) bằng service thật trên DB test: TmnCode sai, mã giao dịch
    // rỗng, hóa đơn đã trả/đã hủy kèm cảnh báo hoàn tiền, mã hóa query kiểu VNPay (dấu cách thành "+"), thanh toán một
    // phần và hai IPN giống nhau gọi đồng thời. Telegram và email là fake dùng chung cả bộ test nên lọc theo mã duy nhất.
    [Collection(WebTestCollection.Name)]
    public class VnPayIpnScenarioTests
    {
        private const string Url = "/api/webhooks/payment/vnpay-ipn";
        private const string TmnCode = "TESTTMN1";
        private const string HashSecret = "test-vnpay-hash-secret";

        private readonly CustomWebApplicationFactory _factory;

        public VnPayIpnScenarioTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seeded(int InvoiceId, string MaHoaDon);

        private WebApplicationFactory<Program> EnabledHost()
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<VnPayOptions>();
                s.AddSingleton(new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = HashSecret });
            }));

        private static string NewTransactionNo() => "VNS" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static Dictionary<string, string> SignedQuery(int hoaDonId, string transactionNo, Action<Dictionary<string, string>>? tweak = null)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_Amount"] = "200000000",
                ["vnp_BankCode"] = "NCB",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan hoa don HD-" + hoaDonId,
                ["vnp_PayDate"] = "20261003093000",
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = transactionNo,
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = VnPayProtocol.BuildTxnRef(hoaDonId, DateTime.UtcNow)
            };
            tweak?.Invoke(query);
            query["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(query), HashSecret);
            return query;
        }

        // VNPay gọi IPN với giá trị mã hóa kiểu form (dấu cách thành "+"), khác Uri.EscapeDataString.
        private static string ToFormEncodedQueryString(IDictionary<string, string> query)
            => "?" + string.Join("&", query.Select(p => $"{p.Key}={WebUtility.UrlEncode(p.Value)}"));

        private static string ToQueryString(IDictionary<string, string> query)
            => "?" + string.Join("&", query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        private static async Task<(HttpStatusCode Status, string Body)> GetRawAsync(WebApplicationFactory<Program> host, string queryString)
        {
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.GetAsync(Url + queryString);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        private static Task<(HttpStatusCode Status, string Body)> GetAsync(WebApplicationFactory<Program> host, IDictionary<string, string> query)
            => GetRawAsync(host, ToQueryString(query));

        private async Task<Seeded> SeedAsync(decimal tongTien = 2_000_000m)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_VS_{s}", MaChiNhanh = TestCodes.MaChiNhanh("S"), DiaChi = "12 Duong Test", SoDienThoai = "0900000007", MoTa = "VS" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000007", CCCD = $"079{s[..6]}7", Email = $"vs_{s}@test.com" };
            db.PhongTros.Add(room);
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            db.NguoiDungs.Add(new NguoiDung { TenDangNhap = $"kh_vs_{s}", MatKhauHash = "hash", Role = Role.KhachThue, IsActive = true, NguoiThueId = tenant.NguoiThueId });
            var contract = new HopDong
            {
                MaHopDong = $"HD_{s}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-2),
                TienCocPhong = 2000000m,
                TienThuePhong = 2000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            db.HopDongs.Add(contract);
            await db.SaveChangesAsync();

            var invoice = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-VS-{s}",
                Thang = 9,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow.AddDays(-1),
                HanThanhToan = DateTime.UtcNow.AddDays(6),
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền phòng", DonGia = tongTien, SoLuong = 1, TongTien = tongTien }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            return new Seeded(invoice.HoaDonId, invoice.MaHoaDon);
        }

        private async Task<List<LichSuThanhToan>> LedgerAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.LichSuThanhToans.AsNoTracking().Where(l => l.HoaDonId == hoaDonId).ToListAsync();
        }

        private async Task<HoaDon> InvoiceAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == hoaDonId);
        }

        private IEnumerable<string> TelegramFor(string transactionNo)
            => _factory.FakeTelegramInstance.Messages.Where(m => m.Text.Contains(transactionNo)).Select(m => m.Text);

        // ---------- Chữ ký, merchant, mã giao dịch ----------

        [Fact]
        public async Task OtherTmnCode_SignedCorrectly_Returns97_AndRecordsNothing()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();

            var (status, body) = await GetAsync(host, SignedQuery(seeded.InvoiceId, transactionNo, q => q["vnp_TmnCode"] = "OTHERTMN"));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"97\",\"Message\":\"Invalid signature\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
            Assert.Empty(TelegramFor(transactionNo));
        }

        [Fact]
        public async Task AmountChangedAfterSigning_Returns97_AndRecordsNothing()
        {
            var seeded = await SeedAsync();
            using var host = EnabledHost();
            var query = SignedQuery(seeded.InvoiceId, NewTransactionNo());
            query["vnp_Amount"] = "100";

            var (status, body) = await GetAsync(host, query);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"97\",\"Message\":\"Invalid signature\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        [Fact]
        public async Task BlankTransactionNo_Returns99_AndRecordsNothing()
        {
            var seeded = await SeedAsync();
            using var host = EnabledHost();

            var (status, body) = await GetAsync(host, SignedQuery(seeded.InvoiceId, string.Empty));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"99\",\"Message\":\"Unknown error\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        // T2 (người dùng chọn 26/10/2026): hóa đơn không có trong DB → 01 "Order not found", không Telegram "cần hoàn tiền".
        [Fact]
        public async Task TxnRefOfAnInvoiceThatDoesNotExist_Returns01_WithoutARefundAlert()
        {
            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();

            var (status, body) = await GetAsync(host, SignedQuery(999_999_999, transactionNo));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"01\",\"Message\":\"Order not found\"}", body);
            Assert.Empty(TelegramFor(transactionNo));
            Assert.Empty(await LedgerAsync(999_999_999));
        }

        // ---------- Mã hóa query kiểu VNPay ----------

        [Fact]
        public async Task QueryEncodedWithPlusForSpaces_IsVerifiedAndRecorded()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();
            var query = SignedQuery(seeded.InvoiceId, transactionNo, q => q["vnp_OrderInfo"] = "Thanh toan hoa don HD " + seeded.InvoiceId + " thang 9");

            var (status, body) = await GetRawAsync(host, ToFormEncodedQueryString(query));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", body);
            Assert.Equal($"VNPAY-{transactionNo}", Assert.Single(await LedgerAsync(seeded.InvoiceId)).MaGiaoDich);
        }

        [Fact]
        public async Task OrderInfoWithVietnameseCharacters_IsVerifiedAndRecorded()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();
            var query = SignedQuery(seeded.InvoiceId, transactionNo, q => q["vnp_OrderInfo"] = "Thanh toán hóa đơn tháng 9 & phòng P101");

            var (status, body) = await GetRawAsync(host, ToFormEncodedQueryString(query));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", body);
            Assert.Single(await LedgerAsync(seeded.InvoiceId));
        }

        // ---------- Số tiền ----------

        [Fact]
        public async Task PartialPayment_IsRecordedAsGiven_AndTheInvoiceBecomesPartiallyPaid()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();

            var (_, body) = await GetAsync(host, SignedQuery(seeded.InvoiceId, transactionNo, q => q["vnp_Amount"] = "50000000"));

            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", body);
            Assert.Equal(500_000m, Assert.Single(await LedgerAsync(seeded.InvoiceId)).SoTienThanhToan);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, (await InvoiceAsync(seeded.InvoiceId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task PayingMoreThanTheBalance_RecordsTheBalance_AndTagsTheSurplus()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();

            var (_, body) = await GetAsync(host, SignedQuery(seeded.InvoiceId, transactionNo, q => q["vnp_Amount"] = "250000000"));

            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", body);
            var ledger = Assert.Single(await LedgerAsync(seeded.InvoiceId));
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.StartsWith("[TIEN-THUA:500000]", ledger.GhiChu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await InvoiceAsync(seeded.InvoiceId)).TrangThaiHoaDon);
        }

        // ---------- Hóa đơn không nhận tiền được ----------

        [Fact]
        public async Task AlreadyPaidInvoice_Returns02_RaisesOneRefundAlert_AndIsNotRaisedTwice()
        {
            var seeded = await SeedAsync();
            using var host = EnabledHost();
            await GetAsync(host, SignedQuery(seeded.InvoiceId, NewTransactionNo()));
            var second = NewTransactionNo();
            var query = SignedQuery(seeded.InvoiceId, second);

            var (status, body) = await GetAsync(host, query);
            var (_, repeatedBody) = await GetAsync(host, query);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"02\",\"Message\":\"Order already confirmed\"}", body);
            Assert.Equal(body, repeatedBody);
            Assert.Single(await LedgerAsync(seeded.InvoiceId));
            var text = Assert.Single(TelegramFor(second));
            Assert.Contains("THANH TOÁN VNPAY CẦN HOÀN TIỀN", text);
            Assert.Contains($"Hóa đơn {seeded.MaHoaDon} đã trả đủ", text);
            Assert.Contains($"Mã GD VNPay: {second}", text);
            Assert.DoesNotContain("CHUYỂN KHOẢN CẦN XỬ LÝ TAY", text);
        }

        [Fact]
        public async Task CancelledInvoice_Returns02_NoLedgerRow_AndARefundAlert()
        {
            var seeded = await SeedAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var invoice = await db.HoaDons.SingleAsync(h => h.HoaDonId == seeded.InvoiceId);
                invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                invoice.IsDeleted = true;
                await db.SaveChangesAsync();
            }

            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();

            var (status, body) = await GetAsync(host, SignedQuery(seeded.InvoiceId, transactionNo));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"02\",\"Message\":\"Order already confirmed\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
            var text = Assert.Single(TelegramFor(transactionNo));
            Assert.Contains("THANH TOÁN VNPAY CẦN HOÀN TIỀN", text);
            Assert.Contains($"Hóa đơn {seeded.MaHoaDon} không nhận tiền được: Hóa đơn đã hủy", text);
        }

        [Fact]
        public async Task FailedPayment_Returns00_AndLeavesTheInvoiceUnpaid()
        {
            var seeded = await SeedAsync();
            using var host = EnabledHost();

            var (_, body) = await GetAsync(host, SignedQuery(seeded.InvoiceId, NewTransactionNo(), q => q["vnp_TransactionStatus"] = "02"));

            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await InvoiceAsync(seeded.InvoiceId)).TrangThaiHoaDon);
        }

        // ---------- Song song ----------

        [Fact]
        public async Task TheSameIpnSentTwiceAtTheSameTime_IsRecordedOnce_AndNotifiedOnce()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = EnabledHost();
            var query = SignedQuery(seeded.InvoiceId, transactionNo);

            var bodies = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => GetAsync(host, query))));

            Assert.All(bodies, b => Assert.Equal(HttpStatusCode.OK, b.Status));
            Assert.Equal(new[] { "00", "02" }, bodies.Select(b => b.Body.Contains("\"RspCode\":\"00\"") ? "00" : "02").OrderBy(x => x).ToArray());
            Assert.Equal($"VNPAY-{transactionNo}", Assert.Single(await LedgerAsync(seeded.InvoiceId)).MaGiaoDich);
            Assert.Single(TelegramFor(transactionNo));
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await InvoiceAsync(seeded.InvoiceId)).TrangThaiHoaDon);
        }
    }
}
