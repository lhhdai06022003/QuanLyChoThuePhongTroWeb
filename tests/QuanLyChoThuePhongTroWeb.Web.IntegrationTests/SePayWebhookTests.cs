using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Webhook SePay qua HTTP (spec thanh toán trực tuyến §4.1, §5, §11.2): xác thực, mã trả về và ghi sổ hóa đơn.
    [Collection(WebTestCollection.Name)]
    public class SePayWebhookTests
    {
        private const string Url = "/api/webhooks/payment/sepay";
        private const string SuccessBody = "{\"success\": true}";

        private readonly CustomWebApplicationFactory _factory;

        public SePayWebhookTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        // Payload đúng định dạng SePay (transactionDate là giờ Việt Nam).
        private static byte[] Payload(
            int hoaDonId, string reference, long amount = 2_000_000, string transferType = "in",
            string? accountNumber = CustomWebApplicationFactory.TestBankAccount, string? content = null,
            string transactionDate = "2026-10-03 09:30:00")
        {
            content ??= $"Nguyen Van A chuyen tien TTPTHD{hoaDonId}";
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = Random.Shared.Next(1, int.MaxValue),
                gateway = "MBBank",
                transactionDate,
                accountNumber,
                subAccount = (string?)null,
                code = (string?)null,
                content,
                transferType,
                transferAmount = amount,
                accumulated = 0,
                referenceCode = reference,
                description = content
            });
        }

        private string Sign(string timestamp, byte[] body)
        {
            var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
            var data = prefix.Concat(body).ToArray();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.SePayOptionsInstance.WebhookSecret));
            return "sha256=" + Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
        }

        private static string Now(int offsetSeconds = 0) => (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offsetSeconds).ToString();

        private static HttpRequestMessage Request(byte[] body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            return request;
        }

        private HttpRequestMessage Signed(byte[] body, string? timestamp = null, string? signature = null)
        {
            timestamp ??= Now();
            var request = Request(body);
            request.Headers.Add("X-SePay-Timestamp", timestamp);
            request.Headers.Add("X-SePay-Signature", signature ?? Sign(timestamp, body));
            return request;
        }

        private async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpRequestMessage request)
        {
            using var client = NewClient();
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        private async Task<int> SeedInvoiceAsync(decimal tongTien = 2_000_000m, TrangThaiPhatHanhHoaDon phatHanh = TrangThaiPhatHanhHoaDon.DaGui)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_SP_{s}", MaChiNhanh = TestCodes.MaChiNhanh("S"), DiaChi = "A", SoDienThoai = "0900000001", MoTa = "SePay" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000001", CCCD = $"079{s[..6]}1", Email = $"k_{s}@test.com" };
            db.PhongTros.Add(room);
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

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
                MaHoaDon = $"HD-SEPAY-{s}",
                Thang = 9,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiPhatHanh = phatHanh,
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
            return invoice.HoaDonId;
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

        // ---------- Xác thực ----------

        [Fact]
        public async Task WrongSignature_Returns401_AndWritesNothing()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var body = Payload(hoaDonId, NewReference());
            var wrongSignature = "sha256=" + new string('0', 64);

            var (status, _) = await SendAsync(Signed(body, signature: wrongSignature));

            Assert.Equal(HttpStatusCode.Unauthorized, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task SignatureOfAnotherBody_Returns401()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var body = Payload(hoaDonId, NewReference());
            var timestamp = Now();
            var otherSignature = Sign(timestamp, Payload(hoaDonId, NewReference(), amount: 1));

            var (status, _) = await SendAsync(Signed(body, timestamp, otherSignature));

            Assert.Equal(HttpStatusCode.Unauthorized, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
        }

        [Fact]
        public async Task MissingAuthenticationHeaders_Returns401()
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, _) = await SendAsync(Request(Payload(hoaDonId, NewReference())));

            Assert.Equal(HttpStatusCode.Unauthorized, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
        }

        [Theory]
        [InlineData(-1000)]
        [InlineData(1000)]
        public async Task SkewedTimestamp_WithValidSignature_Returns401(int offsetSeconds)
        {
            var hoaDonId = await SeedInvoiceAsync();
            var body = Payload(hoaDonId, NewReference());

            var (status, _) = await SendAsync(Signed(body, timestamp: Now(offsetSeconds)));

            Assert.Equal(HttpStatusCode.Unauthorized, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
        }

        // ---------- Mã trả về ----------

        [Fact]
        public async Task OutgoingTransfer_Returns200Success_AndWritesNothing()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var body = Payload(hoaDonId, NewReference(), transferType: "out");

            var (status, responseBody) = await SendAsync(Signed(body));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(SuccessBody, responseBody);
            using var json = JsonDocument.Parse(responseBody);
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Empty(await LedgerAsync(hoaDonId));
        }

        [Fact]
        public async Task MalformedJson_WithValidSignature_Returns400()
        {
            var (status, _) = await SendAsync(Signed(Encoding.UTF8.GetBytes("{")));

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Theory]
        [InlineData("{\"id\":\"92704\",\"transferType\":\"in\",\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":\"2000000\",\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":2000000.5,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":5,\"transferAmount\":2000000,\"transactionDate\":\"2026-10-03 09:30:00\"}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":2000000}")]
        [InlineData("{\"id\":92704,\"transferType\":\"in\",\"transferAmount\":2000000,\"transactionDate\":\"03/10/2026 09:30\"}")]
        [InlineData("[]")]
        [InlineData("null")]
        public async Task MissingOrWrongTypedRequiredField_Returns400(string json)
        {
            var (status, _) = await SendAsync(Signed(Encoding.UTF8.GetBytes(json)));

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Fact]
        public async Task OversizedBody_Returns413()
        {
            var body = Enumerable.Repeat((byte)' ', 64 * 1024 + 1).ToArray();

            var (status, _) = await SendAsync(Request(body));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, status);
        }

        [Fact]
        public async Task WebhookDisabled_Returns404_BeforeAnyCheck()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var body = Payload(hoaDonId, NewReference());
            var options = _factory.SePayOptionsInstance;
            options.Enabled = false;
            try
            {
                var (signedStatus, _) = await SendAsync(Signed(body));
                var (unsignedStatus, _) = await SendAsync(Request(body));

                Assert.Equal(HttpStatusCode.NotFound, signedStatus);
                Assert.Equal(HttpStatusCode.NotFound, unsignedStatus);
                Assert.Empty(await LedgerAsync(hoaDonId));
            }
            finally
            {
                options.Enabled = true;
            }
        }

        // ---------- Ghi sổ ----------

        [Fact]
        public async Task SamePayloadSentThreeTimes_RecordsOnce_AndPaysInvoice()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var reference = NewReference();
            var body = Payload(hoaDonId, reference);

            for (var i = 0; i < 3; i++)
            {
                var (status, responseBody) = await SendAsync(Signed(body));

                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Equal(SuccessBody, responseBody);
            }

            var ledger = Assert.Single(await LedgerAsync(hoaDonId));
            Assert.Equal($"SEPAY-{reference}", ledger.MaGiaoDich);
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, ledger.PhuongThucThanhToan);
            Assert.Null(ledger.NguoiXacNhanId);
            // transactionDate là giờ Việt Nam, lưu UTC.
            Assert.Equal(new DateTime(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc), ledger.NgayThanhToan);
            Assert.StartsWith($"[SEPAY] MBBank · REF {reference}", ledger.GhiChu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task PartialAmount_SetsPartialStatus()
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), amount: 1_500_000)));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(1_500_000m, Assert.Single(await LedgerAsync(hoaDonId)).SoTienThanhToan);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task TwoTransfersWithDifferentReferences_BothCount()
        {
            var hoaDonId = await SeedInvoiceAsync();

            await SendAsync(Signed(Payload(hoaDonId, NewReference(), amount: 1_200_000)));
            await SendAsync(Signed(Payload(hoaDonId, NewReference(), amount: 800_000)));

            Assert.Equal(2, (await LedgerAsync(hoaDonId)).Count);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task Overpayment_RecordsTheDebtOnly_AndTagsTheSurplus()
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), amount: 2_500_000)));

            Assert.Equal(HttpStatusCode.OK, status);
            var ledger = Assert.Single(await LedgerAsync(hoaDonId));
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.StartsWith("[TIEN-THUA:500000] [SEPAY]", ledger.GhiChu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task TransferToAnotherAccount_Returns200_AndWritesNothing()
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, responseBody) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), accountNumber: "9999999999")));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(SuccessBody, responseBody);
            Assert.Empty(await LedgerAsync(hoaDonId));
        }

        [Fact]
        public async Task TransferWithoutCode_Returns200_AndWritesNothing()
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), content: "Chuyen tien tro thang 9")));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
        }

        [Fact]
        public async Task UnpublishedInvoice_Returns200_AndWritesNothing()
        {
            var hoaDonId = await SeedInvoiceAsync(phatHanh: TrangThaiPhatHanhHoaDon.DaChot);

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference())));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task CodeForAnInvoiceThatDoesNotExist_Returns200()
        {
            var (status, responseBody) = await SendAsync(Signed(Payload(999_999_999, NewReference())));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(SuccessBody, responseBody);
        }

        // ---------- Chế độ ApiKey ----------

        [Fact]
        public async Task ApiKeyMode_AcceptsTheKey_AndRejectsSignatureOnlyRequests()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var body = Payload(hoaDonId, NewReference());
            var options = _factory.SePayOptionsInstance;
            options.WebhookAuthMode = SePayWebhookAuthMode.ApiKey;
            options.WebhookApiKey = "test-sepay-api-key";
            try
            {
                var (signatureOnly, _) = await SendAsync(Signed(body));
                Assert.Equal(HttpStatusCode.Unauthorized, signatureOnly);

                var wrongKey = Request(body);
                wrongKey.Headers.TryAddWithoutValidation("Authorization", "Apikey wrong-key");
                Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(wrongKey)).Status);
                Assert.Empty(await LedgerAsync(hoaDonId));

                var correctKey = Request(body);
                correctKey.Headers.TryAddWithoutValidation("Authorization", "Apikey test-sepay-api-key");
                var (status, responseBody) = await SendAsync(correctKey);

                Assert.Equal(HttpStatusCode.OK, status);
                Assert.Equal(SuccessBody, responseBody);
                Assert.Single(await LedgerAsync(hoaDonId));
            }
            finally
            {
                options.WebhookAuthMode = SePayWebhookAuthMode.Hmac;
                options.WebhookApiKey = string.Empty;
            }
        }
    }
}
