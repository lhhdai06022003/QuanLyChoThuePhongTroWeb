using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Bổ sung cho SePayWebhookTests: mã trả về theo từng kết quả xử lý (kể cả lỗi hệ thống -> 500), thứ tự
    // kiểm tra (401/400/404/413 không bao giờ chạm tới bước ghi sổ), gửi song song và biên giờ Việt Nam -> UTC.
    [Collection(WebTestCollection.Name)]
    public class SePayWebhookBehaviourTests
    {
        private const string Url = "/api/webhooks/payment/sepay";

        private readonly CustomWebApplicationFactory _factory;

        public SePayWebhookBehaviourTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Thay bước ghi sổ bằng bản đếm lượt gọi để chứng minh các lỗi xác thực/payload dừng trước đó.
        private sealed class FakeIngestion : IBankTransferIngestionService
        {
            private readonly Func<IncomingBankTransfer, TransferOutcome> _result;
            public FakeIngestion(Func<IncomingBankTransfer, TransferOutcome> result) => _result = result;

            public ConcurrentQueue<IncomingBankTransfer> Calls { get; } = new();

            public Task<TransferOutcome> IngestAsync(IncomingBankTransfer transfer, CancellationToken ct = default)
            {
                Calls.Enqueue(transfer);
                return Task.FromResult(_result(transfer));
            }
        }

        private static FakeIngestion Returning(TransferOutcomeKind kind, string? lyDo = null)
            => new(t => new TransferOutcome { Kind = kind, Transfer = t, LyDo = lyDo });

        private WebApplicationFactory<Program> WithIngestion(FakeIngestion fake)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IBankTransferIngestionService>();
                s.AddSingleton<IBankTransferIngestionService>(fake);
            }));

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static byte[] Payload(int hoaDonId, string reference, long amount = 2_000_000, string? content = null, string transactionDate = "2026-10-03 09:30:00")
        {
            content ??= $"TTPTHD{hoaDonId}";
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = Random.Shared.Next(1, int.MaxValue),
                gateway = "MBBank",
                transactionDate,
                accountNumber = CustomWebApplicationFactory.TestBankAccount,
                subAccount = (string?)null,
                code = (string?)null,
                content,
                transferType = "in",
                transferAmount = amount,
                accumulated = 0,
                referenceCode = reference,
                description = content
            });
        }

        private string Sign(string timestamp, byte[] body)
        {
            var data = Encoding.UTF8.GetBytes(timestamp + ".").Concat(body).ToArray();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.SePayOptionsInstance.WebhookSecret));
            return "sha256=" + Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
        }

        private HttpRequestMessage Signed(byte[] body, string? signature = null)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Add("X-SePay-Timestamp", timestamp);
            request.Headers.Add("X-SePay-Signature", signature ?? Sign(timestamp, body));
            return request;
        }

        private static HttpRequestMessage Unsigned(byte[] body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return request;
        }

        private static async Task<(HttpStatusCode Status, string Body)> SendAsync(WebApplicationFactory<Program> factory, HttpRequestMessage request)
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        private Task<(HttpStatusCode Status, string Body)> SendAsync(HttpRequestMessage request) => SendAsync(_factory, request);

        private async Task<int> SeedInvoiceAsync(decimal tongTien = 2_000_000m)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_SB_{s}", MaChiNhanh = TestCodes.MaChiNhanh("B"), DiaChi = "A", SoDienThoai = "0900000002", MoTa = "SePay" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000002", CCCD = $"079{s[..6]}2", Email = $"b_{s}@test.com" };
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
                MaHoaDon = $"HD-SPB-{s}",
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

        // ---------- Mã trả về theo kết quả xử lý ----------

        [Fact]
        public async Task SystemFailure_Returns500_SoSePayRetries()
        {
            var fake = Returning(TransferOutcomeKind.Failed, "Đã có lỗi hệ thống, vui lòng thử lại.");
            using var host = WithIngestion(fake);

            var (status, body) = await SendAsync(host, Signed(Payload(1, NewReference())));

            Assert.Equal(HttpStatusCode.InternalServerError, status);
            Assert.Equal("{\"success\": false}", body);
            Assert.Single(fake.Calls);
        }

        [Theory]
        [InlineData(TransferOutcomeKind.Recorded)]
        [InlineData(TransferOutcomeKind.Duplicate)]
        [InlineData(TransferOutcomeKind.Ignored)]
        [InlineData(TransferOutcomeKind.WrongAccount)]
        [InlineData(TransferOutcomeKind.Unmatched)]
        [InlineData(TransferOutcomeKind.InvoiceNotPayable)]
        [InlineData(TransferOutcomeKind.AlreadyPaid)]
        public async Task EveryOutcomeExceptFailed_Returns200WithSuccessBody(TransferOutcomeKind kind)
        {
            var fake = Returning(kind);
            using var host = WithIngestion(fake);

            var (status, body) = await SendAsync(host, Signed(Payload(1, NewReference())));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"success\": true}", body);
            Assert.Single(fake.Calls);
        }

        [Fact]
        public async Task ParsedTransfer_IsPassedToTheService_WithUtcDateAndNormalizedFields()
        {
            var fake = Returning(TransferOutcomeKind.Recorded);
            using var host = WithIngestion(fake);
            var reference = NewReference();

            await SendAsync(host, Signed(Payload(105, reference, amount: 1_234_000, transactionDate: "2026-10-03 09:30:15")));

            var transfer = Assert.Single(fake.Calls);
            Assert.Equal(new DateTime(2026, 10, 3, 2, 30, 15, DateTimeKind.Utc), transfer.TransactionDateUtc);
            Assert.Equal(1_234_000m, transfer.TransferAmount);
            Assert.Equal("in", transfer.TransferType);
            Assert.Equal(reference, transfer.ReferenceCode);
            Assert.Equal($"SEPAY-{reference}", transfer.DedupKey);
            Assert.Equal(CustomWebApplicationFactory.TestBankAccount, transfer.AccountNumber);
        }

        // ---------- 401/400/404/413 dừng trước bước ghi sổ (không đụng DB) ----------

        [Fact]
        public async Task Unauthorized_NeverReachesTheService_AndLeavesTheDatabaseUntouched()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var fake = Returning(TransferOutcomeKind.Recorded);
            using var host = WithIngestion(fake);
            var body = Payload(hoaDonId, NewReference());

            var wrongSignature = await SendAsync(host, Signed(body, signature: "sha256=" + new string('a', 64)));
            var noHeaders = await SendAsync(host, Unsigned(body));
            // Chữ ký đúng nhưng body hỏng: phải 401 chứ không phải 400, vì xác thực chạy trước khi parse.
            var brokenBodyWrongSignature = await SendAsync(host, Signed(Encoding.UTF8.GetBytes("{"), signature: "sha256=" + new string('b', 64)));

            Assert.Equal(HttpStatusCode.Unauthorized, wrongSignature.Status);
            Assert.Equal(HttpStatusCode.Unauthorized, noHeaders.Status);
            Assert.Equal(HttpStatusCode.Unauthorized, brokenBodyWrongSignature.Status);
            Assert.Empty(fake.Calls);
            Assert.Empty(await LedgerAsync(hoaDonId));
        }

        [Fact]
        public async Task BadRequest_NeverReachesTheService()
        {
            var fake = Returning(TransferOutcomeKind.Recorded);
            using var host = WithIngestion(fake);

            var malformed = await SendAsync(host, Signed(Encoding.UTF8.GetBytes("{")));
            var missingAmount = await SendAsync(host, Signed(Encoding.UTF8.GetBytes(
                "{\"id\":1,\"transferType\":\"in\",\"transactionDate\":\"2026-10-03 09:30:00\"}")));

            Assert.Equal(HttpStatusCode.BadRequest, malformed.Status);
            Assert.Equal(HttpStatusCode.BadRequest, missingAmount.Status);
            Assert.Empty(fake.Calls);
        }

        [Fact]
        public async Task Disabled_Returns404_AndNeverReachesTheService()
        {
            var fake = Returning(TransferOutcomeKind.Recorded);
            using var host = WithIngestion(fake);
            var options = _factory.SePayOptionsInstance;
            options.Enabled = false;
            try
            {
                var valid = await SendAsync(host, Signed(Payload(1, NewReference())));
                var invalid = await SendAsync(host, Unsigned(Encoding.UTF8.GetBytes("{")));
                var oversized = await SendAsync(host, Unsigned(Enumerable.Repeat((byte)' ', 64 * 1024 + 1).ToArray()));

                Assert.Equal(HttpStatusCode.NotFound, valid.Status);
                Assert.Equal(HttpStatusCode.NotFound, invalid.Status);
                Assert.Equal(HttpStatusCode.NotFound, oversized.Status);
                Assert.Empty(fake.Calls);
            }
            finally
            {
                options.Enabled = true;
            }
        }

        [Fact]
        public async Task OversizedBody_Returns413_BeforeAuthenticationAndService()
        {
            var fake = Returning(TransferOutcomeKind.Recorded);
            using var host = WithIngestion(fake);

            var (status, _) = await SendAsync(host, Unsigned(Enumerable.Repeat((byte)' ', 64 * 1024 + 1).ToArray()));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, status);
            Assert.Empty(fake.Calls);
        }

        [Fact]
        public async Task BodyAtTheLimit_IsStillReadAndAuthenticated()
        {
            // Đúng 64 KB (không vượt) thì không bị 413: đi tiếp tới bước xác thực và bị 401 vì không ký.
            var (status, _) = await SendAsync(Unsigned(Enumerable.Repeat((byte)' ', 64 * 1024).ToArray()));

            Assert.Equal(HttpStatusCode.Unauthorized, status);
        }

        // ---------- Ghi sổ thật ----------

        // TC05/E05 ở tầng HTTP: hai request giống hệt gửi cùng lúc vẫn chỉ ra một dòng sổ.
        [Fact]
        public async Task SamePayloadSentConcurrently_RecordsOnce()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var reference = NewReference();
            var body = Payload(hoaDonId, reference);

            var results = await Task.WhenAll(
                Enumerable.Range(0, 4).Select(_ => Task.Run(() => SendAsync(Signed(body)))));

            Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.Status));
            var ledger = Assert.Single(await LedgerAsync(hoaDonId));
            Assert.Equal($"SEPAY-{reference}", ledger.MaGiaoDich);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task ResendAfterTheLedgerRowWasVoided_DoesNotRecordAgain()
        {
            var hoaDonId = await SeedInvoiceAsync();
            var body = Payload(hoaDonId, NewReference());
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(Signed(body))).Status);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await db.LichSuThanhToans.SingleAsync(l => l.HoaDonId == hoaDonId);
                row.IsDeleted = true;
                await db.SaveChangesAsync();
            }

            var (status, _) = await SendAsync(Signed(body));

            Assert.Equal(HttpStatusCode.OK, status);
            var rows = await LedgerAsync(hoaDonId);
            var only = Assert.Single(rows);
            Assert.True(only.IsDeleted);
        }

        [Fact]
        public async Task PartialPayment_IsRecorded_ForAnInvoiceThatDoesNotAllowPartialPayment()
        {
            var hoaDonId = await SeedInvoiceAsync();
            Assert.False((await InvoiceAsync(hoaDonId)).ChoPhepThanhToanMotPhan);

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), amount: 100_000)));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(100_000m, Assert.Single(await LedgerAsync(hoaDonId)).SoTienThanhToan);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task TransferAfterTheInvoiceIsPaid_Returns200_AndDoesNotRecord()
        {
            var hoaDonId = await SeedInvoiceAsync();
            await SendAsync(Signed(Payload(hoaDonId, NewReference())));

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference())));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Single(await LedgerAsync(hoaDonId));
        }

        [Fact]
        public async Task CodeWithMoreThanNineDigits_IsUnmatched_AndNoInvoiceIsTouched()
        {
            var hoaDonId = await SeedInvoiceAsync();
            // Số hóa đơn thật theo sau 10 chữ số 0: dài hơn 9 chữ số nên phải là Unmatched, không được cắt lấy 9 chữ số đầu.
            var content = $"TTPTHD{hoaDonId}0000000000";

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), content: content)));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await InvoiceAsync(hoaDonId)).TrangThaiHoaDon);
        }

        [Fact]
        public async Task LowercaseAndSpacedCodeInContent_StillMatches()
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), content: $"nguyen van a ttpt hd {hoaDonId} thang 9")));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Single(await LedgerAsync(hoaDonId));
        }

        // Biên giờ Việt Nam (UTC+7) -> UTC: 00:00-06:59 rơi vào ngày hôm trước theo UTC.
        [Theory]
        [InlineData("2026-10-03 00:00:00", 2026, 10, 2, 17, 0, 0)]
        [InlineData("2026-10-03 06:59:59", 2026, 10, 2, 23, 59, 59)]
        [InlineData("2026-10-03 07:00:00", 2026, 10, 3, 0, 0, 0)]
        [InlineData("2026-10-03 23:59:59", 2026, 10, 3, 16, 59, 59)]
        [InlineData("2026-01-01 00:00:00", 2025, 12, 31, 17, 0, 0)]
        public async Task TransactionDate_IsStoredAsUtc(string transactionDate, int y, int mo, int d, int h, int mi, int s)
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), transactionDate: transactionDate)));

            Assert.Equal(HttpStatusCode.OK, status);
            var ledger = Assert.Single(await LedgerAsync(hoaDonId));
            Assert.Equal(new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc), ledger.NgayThanhToan);
            // Ngày xác nhận là giờ server (UTC), không lấy từ SePay.
            Assert.NotNull(ledger.NgayXacNhan);
            Assert.True(Math.Abs((DateTime.UtcNow - ledger.NgayXacNhan!.Value).TotalMinutes) < 5);
        }

        [Fact]
        public async Task TransactionDateInAnotherFormat_Returns400_AndWritesNothing()
        {
            var hoaDonId = await SeedInvoiceAsync();

            var (status, _) = await SendAsync(Signed(Payload(hoaDonId, NewReference(), transactionDate: "2026-10-03T09:30:00")));

            Assert.Equal(HttpStatusCode.BadRequest, status);
            Assert.Empty(await LedgerAsync(hoaDonId));
        }
    }
}
