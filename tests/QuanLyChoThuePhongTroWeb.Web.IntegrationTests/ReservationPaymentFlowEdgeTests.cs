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
    // Bổ sung của Tester cho đợt 3 phần 1, tầng HTTP: thông báo Telegram của tiền giữ chỗ (đủ tiền, muộn, cờ tắt) và việc
    // IPN VNPay phân nhánh theo vnp_TxnRef (mã HD… không bao giờ chạm vào giữ chỗ). Cờ bật bằng WithWebHostBuilder.
    [Collection(WebTestCollection.Name)]
    public class ReservationPaymentFlowEdgeTests
    {
        private const string SePayUrl = "/api/webhooks/payment/sepay";
        private const string IpnUrl = "/api/webhooks/payment/vnpay-ipn";
        private const string TmnCode = "TESTTMN1";
        private const string HashSecret = "test-vnpay-hash-secret";
        private const decimal HoldAmount = 500_000m;

        private readonly CustomWebApplicationFactory _factory;

        public ReservationPaymentFlowEdgeTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private WebApplicationFactory<Program> Host(bool reservationEnabled)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<OnlinePaymentOptions>();
                s.AddSingleton(new OnlinePaymentOptions { PublicBaseUrl = "https://tro.test", ReservationEnabled = reservationEnabled });
                s.RemoveAll<VnPayOptions>();
                s.AddSingleton(new VnPayOptions { Enabled = true, TmnCode = TmnCode, HashSecret = HashSecret });
            }));

        private sealed record Seeded(int HoldId, int PaymentId);

        private async Task<Seeded> SeedAsync(TrangThaiYeuCauGiuCho status = TrangThaiYeuCauGiuCho.ChoThanhToan, TimeSpan? deadlineFromNow = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_GE_{s}", MaChiNhanh = TestCodes.MaChiNhanh("E"), DiaChi = "A", SoDienThoai = "0900000007", MoTa = "GE" };
            var guest = new NguoiDung { TenDangNhap = $"ge_guest_{s}", MatKhauHash = "hash", Role = Role.KhachVangLai, IsActive = true };
            var staff = new NguoiDung { TenDangNhap = $"ge_staff_{s}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            db.ChiNhanhs.Add(branch);
            db.NguoiDungs.AddRange(guest, staff);
            await db.SaveChangesAsync();

            var profile = new KhachVangLai { NguoiDungId = guest.NguoiDungId, HoTen = $"Khach GE {s}", SoDienThoai = "0911000007", Email = $"ge_{s}@test.com" };
            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"E_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "E", TrangThai = TrangThaiPhong.Trong };
            db.KhachVangLais.Add(profile);
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var deadline = DateTime.UtcNow + (deadlineFromNow ?? TimeSpan.FromDays(1));
            var hold = new YeuCauGiuCho
            {
                KhachVangLaiId = profile.KhachVangLaiId,
                PhongTroId = room.PhongTroId,
                SoTienGiuCho = HoldAmount,
                HanThanhToan = deadline,
                HanKyHopDong = DateTime.UtcNow.AddDays(3),
                NguoiDuyetId = staff.NguoiDungId,
                NgayDuyet = DateTime.UtcNow.AddDays(-2),
                TrangThai = status
            };
            db.YeuCauGiuChos.Add(hold);
            await db.SaveChangesAsync();

            var payment = new YeuCauThanhToanGiuCho
            {
                YeuCauGiuChoId = hold.YeuCauGiuChoId,
                MaYeuCau = $"TTPTGC{hold.YeuCauGiuChoId}",
                NoiDungChuyenKhoan = $"TTPTGC{hold.YeuCauGiuChoId}",
                SoTien = HoldAmount,
                HanThanhToan = deadline,
                NgayTao = DateTime.UtcNow.AddDays(-2),
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiTaoId = staff.NguoiDungId
            };
            db.YeuCauThanhToanGiuChos.Add(payment);
            await db.SaveChangesAsync();
            return new Seeded(hold.YeuCauGiuChoId, payment.YeuCauThanhToanGiuChoId);
        }

        private async Task<(List<GiaoDichGiuCho> Rows, TrangThaiYeuCauGiuCho Hold)> StateAsync(Seeded seed)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.GiaoDichGiuChos.AsNoTracking().Where(g => g.YeuCauThanhToanGiuChoId == seed.PaymentId).ToListAsync();
            var hold = await db.YeuCauGiuChos.AsNoTracking().SingleAsync(h => h.YeuCauGiuChoId == seed.HoldId);
            return (rows, hold.TrangThai);
        }

        // ---- SePay ----

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static byte[] SePayPayload(int holdId, string reference)
        {
            var content = $"Nguyen Van A TTPTGC{holdId}";
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = Random.Shared.Next(1, int.MaxValue),
                gateway = "MBBank",
                transactionDate = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss"),
                accountNumber = CustomWebApplicationFactory.TestBankAccount,
                subAccount = (string?)null,
                code = (string?)null,
                content,
                transferType = "in",
                transferAmount = (long)HoldAmount,
                accumulated = 0,
                referenceCode = reference,
                description = content
            });
        }

        private async Task<HttpStatusCode> PostSePayAsync(WebApplicationFactory<Program> host, byte[] body)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var data = Encoding.UTF8.GetBytes(timestamp + ".").Concat(body).ToArray();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_factory.SePayOptionsInstance.WebhookSecret));
            using var request = new HttpRequestMessage(HttpMethod.Post, SePayUrl) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            request.Headers.Add("X-SePay-Timestamp", timestamp);
            request.Headers.Add("X-SePay-Signature", "sha256=" + Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant());
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        private List<(string ChatId, string Text)> TelegramFor(string reference)
            => _factory.FakeTelegramInstance.Messages.Where(m => m.Text.Contains(reference)).ToList();

        [Fact]
        public async Task SePay_FullPayment_SendsOneTelegramMessage_ToTheDefaultChat()
        {
            var seed = await SeedAsync();
            var reference = NewReference();

            var status = await PostSePayAsync(Host(true), SePayPayload(seed.HoldId, reference));

            Assert.Equal(HttpStatusCode.OK, status);
            var message = Assert.Single(TelegramFor(reference));
            Assert.Equal("-100100", message.ChatId);
            Assert.Contains("THU TIỀN GIỮ CHỖ", message.Text);
            Assert.DoesNotContain("CẦN HOÀN", message.Text);
            Assert.Equal(TrangThaiYeuCauGiuCho.DangGiuCho, (await StateAsync(seed)).Hold);
        }

        [Fact]
        public async Task SePay_PaymentAfterTheHoldExpired_IsRecordedAsALateRefund_AndTelegramSaysToRefund()
        {
            var seed = await SeedAsync(TrangThaiYeuCauGiuCho.HetHan, TimeSpan.FromHours(-1));
            var reference = NewReference();

            var status = await PostSePayAsync(Host(true), SePayPayload(seed.HoldId, reference));

            Assert.Equal(HttpStatusCode.OK, status);
            var (rows, hold) = await StateAsync(seed);
            var row = Assert.Single(rows);
            Assert.Equal("LATE_REFUND_REQUIRED", row.GhiChu);
            Assert.Null(row.NguoiXacNhanId);
            Assert.Equal(TrangThaiYeuCauGiuCho.HetHan, hold);
            Assert.Contains("CẦN HOÀN", Assert.Single(TelegramFor(reference)).Text);
        }

        [Fact]
        public async Task SePay_FlagOff_RaisesATelegramAlertForTheUnrecognizedCode_AndRecordsNothing()
        {
            var seed = await SeedAsync();
            var reference = NewReference();

            var status = await PostSePayAsync(Host(false), SePayPayload(seed.HoldId, reference));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Empty((await StateAsync(seed)).Rows);
            var message = Assert.Single(TelegramFor(reference));
            Assert.Contains($"TTPTGC{seed.HoldId}", message.Text);
        }

        // ---- VNPay IPN ----

        private static string ToQueryString(IDictionary<string, string> query)
            => "?" + string.Join("&", query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        private static Dictionary<string, string> SignedIpn(string txnRef, string transactionNo, int holdId)
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_Amount"] = ((long)(HoldAmount * 100)).ToString(),
                ["vnp_BankCode"] = "NCB",
                ["vnp_CardType"] = "ATM",
                ["vnp_OrderInfo"] = "Thanh toan " + holdId,
                ["vnp_PayDate"] = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss"),
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = transactionNo,
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = txnRef
            };
            query["vnp_SecureHash"] = VnPayProtocol.Sign(VnPayProtocol.BuildSignData(query), HashSecret);
            return query;
        }

        private static async Task<string> RspCodeAsync(WebApplicationFactory<Program> host, IDictionary<string, string> query)
        {
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.GetAsync(IpnUrl + ToQueryString(query));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("RspCode").GetString()!;
        }

        // Mã đơn kiểu hóa đơn (HD{id}_…) với id trùng một giữ chỗ đang chờ: phải đi nhánh hóa đơn nên không ghi gì vào giữ chỗ,
        // dù cờ giữ chỗ bật. Mã trả do nhánh hóa đơn xử lý (00/01/02), nhưng giữ chỗ tuyệt đối không bị tác động.
        [Fact]
        public async Task Ipn_InvoiceStyleTxnRef_NeverReachesTheReservationBranch()
        {
            var seed = await SeedAsync();
            var transactionNo = "VNE" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            var query = SignedIpn(VnPayProtocol.BuildTxnRef(seed.HoldId, DateTime.UtcNow), transactionNo, seed.HoldId);

            var code = await RspCodeAsync(Host(true), query);

            Assert.True(code is "00" or "01" or "02");
            var (rows, hold) = await StateAsync(seed);
            Assert.Empty(rows);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, hold);
        }

        // Nhánh giữ chỗ ghi đúng một dòng VNPAY- kể cả khi có hai IPN cùng khóa chạy lần lượt, và thông báo Telegram nêu kênh VNPay.
        [Fact]
        public async Task Ipn_ReservationTxnRef_IsRecordedOnce_AndTelegramNamesVnPay()
        {
            var seed = await SeedAsync();
            var transactionNo = "VNE" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            var host = Host(true);
            var query = SignedIpn(VnPayProtocol.BuildReservationTxnRef(seed.HoldId, DateTime.UtcNow), transactionNo, seed.HoldId);

            Assert.Equal("00", await RspCodeAsync(host, query));
            Assert.Equal("02", await RspCodeAsync(host, query));

            var (rows, _) = await StateAsync(seed);
            Assert.Equal("VNPAY-" + transactionNo, Assert.Single(rows).MaGiaoDich);
            var message = Assert.Single(TelegramFor(transactionNo));
            Assert.Contains("VNPay", message.Text);
        }
    }
}
