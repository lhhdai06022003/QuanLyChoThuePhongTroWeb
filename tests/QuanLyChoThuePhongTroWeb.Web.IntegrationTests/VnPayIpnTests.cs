using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // IPN VNPay (spec VNPay §5.2): GET ẩn danh /api/webhooks/payment/vnpay-ipn, luôn HTTP 200 với JSON {RspCode, Message},
    // bật/tắt bằng VnPay:Enabled, ghi sổ và thông báo giống SePay. Telegram và email là fake dùng chung cả bộ test nên
    // mọi so khớp lọc theo mã giao dịch hoặc hóa đơn duy nhất.
    [Collection(WebTestCollection.Name)]
    public class VnPayIpnTests
    {
        private const string Url = "/api/webhooks/payment/vnpay-ipn";
        private const string TmnCode = "TESTTMN1";
        private const string HashSecret = "test-vnpay-hash-secret";

        private readonly CustomWebApplicationFactory _factory;

        public VnPayIpnTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seeded(int InvoiceId, string MaHoaDon, int TenantUserId, string TenantEmail);

        private sealed class RecordingNotifier : IThongBaoNotifier
        {
            public ConcurrentQueue<(int UserId, object Payload)> Sent { get; } = new();
            public ConcurrentQueue<(int UserId, InvoicePaymentUpdatedEvent Payload)> PaymentUpdates { get; } = new();

            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default)
            {
                Sent.Enqueue((nguoiDungId, payload));
                return Task.CompletedTask;
            }

            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task SendInvoicePaymentUpdatedAsync(int nguoiDungId, InvoicePaymentUpdatedEvent payload, CancellationToken cancellationToken = default)
            {
                PaymentUpdates.Enqueue((nguoiDungId, payload));
                return Task.CompletedTask;
            }
        }

        private sealed class FakeVnPayService : IVnPayPaymentService
        {
            public IReadOnlyDictionary<string, string>? LastQuery { get; private set; }

            public Task<VnPayIpnResult> HandleIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken ct = default)
            {
                LastQuery = query;
                return Task.FromResult(VnPayIpnResult.UnknownError);
            }
        }

        private WebApplicationFactory<Program> WithVnPay(bool enabled, Action<IServiceCollection>? configure = null)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<VnPayOptions>();
                s.AddSingleton(new VnPayOptions { Enabled = enabled, TmnCode = TmnCode, HashSecret = HashSecret });
                configure?.Invoke(s);
            }));

        private static string NewTransactionNo() => "VNP" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        // Query IPN hợp lệ cho một hóa đơn (2.000.000đ), đã ký bằng VnPayProtocol.
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

        private static string ToQueryString(IDictionary<string, string> query)
            => "?" + string.Join("&", query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        private static async Task<(HttpStatusCode Status, string Body, string? ContentType)> GetAsync(
            WebApplicationFactory<Program> factory, IDictionary<string, string> query)
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.GetAsync(Url + ToQueryString(query));
            return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.MediaType);
        }

        // Chi nhánh, khách thuê có tài khoản và email, một hóa đơn DaGui.
        private async Task<Seeded> SeedAsync(decimal tongTien = 2_000_000m)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_VP_{s}", MaChiNhanh = TestCodes.MaChiNhanh("V"), DiaChi = "12 Duong Test", SoDienThoai = "0900000006", MoTa = "VP" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var email = $"vp_{s}@test.com";
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000006", CCCD = $"079{s[..6]}6", Email = email };
            db.PhongTros.Add(room);
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var tenantUser = new NguoiDung { TenDangNhap = $"kh_vp_{s}", MatKhauHash = "hash", Role = Role.KhachThue, IsActive = true, NguoiThueId = tenant.NguoiThueId };
            db.NguoiDungs.Add(tenantUser);

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
                MaHoaDon = $"HD-VP-{s}",
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

            return new Seeded(invoice.HoaDonId, invoice.MaHoaDon, tenantUser.NguoiDungId, email);
        }

        private async Task<List<LichSuThanhToan>> LedgerAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.LichSuThanhToans.AsNoTracking().Where(l => l.HoaDonId == hoaDonId).ToListAsync();
        }

        private async Task<TrangThaiHoaDon> InvoiceStatusAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (await db.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == hoaDonId)).TrangThaiHoaDon;
        }

        // ---------- Bật/tắt và phương thức ----------

        [Fact]
        public async Task VnPayDisabled_Returns404_AndRecordsNothing()
        {
            var seeded = await SeedAsync();
            using var host = WithVnPay(enabled: false);

            var (status, _, _) = await GetAsync(host, SignedQuery(seeded.InvoiceId, NewTransactionNo()));

            Assert.Equal(HttpStatusCode.NotFound, status);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        [Fact]
        public async Task DefaultHost_WithoutVnPayConfiguration_Returns404()
        {
            var seeded = await SeedAsync();

            var (status, _, _) = await GetAsync(_factory, SignedQuery(seeded.InvoiceId, NewTransactionNo()));

            Assert.Equal(HttpStatusCode.NotFound, status);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        [Fact]
        public async Task Post_Returns405()
        {
            var seeded = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            using var response = await client.PostAsync(Url + ToQueryString(SignedQuery(seeded.InvoiceId, NewTransactionNo())), new StringContent(string.Empty));

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        // ---------- Chữ ký ----------

        [Fact]
        public async Task WrongSignature_Returns200With97_AndRecordsNothing()
        {
            var seeded = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var query = SignedQuery(seeded.InvoiceId, NewTransactionNo());
            var signature = query["vnp_SecureHash"];
            query["vnp_SecureHash"] = (signature[0] == '0' ? '1' : '0') + signature[1..];

            var (status, body, contentType) = await GetAsync(host, query);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("application/json", contentType);
            Assert.Equal("{\"RspCode\":\"97\",\"Message\":\"Invalid signature\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        [Fact]
        public async Task MissingSignature_Returns200With97()
        {
            var seeded = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var query = SignedQuery(seeded.InvoiceId, NewTransactionNo());
            query.Remove("vnp_SecureHash");

            var (status, body, _) = await GetAsync(host, query);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"97\",\"Message\":\"Invalid signature\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        // ---------- Ghi sổ và thông báo ----------

        [Fact]
        public async Task ValidIpn_Returns00_RecordsTheLedgerRow_AndSendsEveryNotification()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            var recorder = new RecordingNotifier();
            using var host = WithVnPay(enabled: true, s =>
            {
                s.RemoveAll<IThongBaoNotifier>();
                s.AddSingleton<IThongBaoNotifier>(recorder);
            });

            var (status, body, contentType) = await GetAsync(host, SignedQuery(seeded.InvoiceId, transactionNo));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("application/json", contentType);
            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", body);

            var ledger = Assert.Single(await LedgerAsync(seeded.InvoiceId));
            Assert.Equal($"VNPAY-{transactionNo}", ledger.MaGiaoDich);
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, ledger.PhuongThucThanhToan);
            Assert.Null(ledger.NguoiXacNhanId);
            Assert.Equal(new DateTime(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc), ledger.NgayThanhToan);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, await InvoiceStatusAsync(seeded.InvoiceId));

            var update = Assert.Single(recorder.PaymentUpdates);
            Assert.Equal(seeded.TenantUserId, update.UserId);
            Assert.Equal(new InvoicePaymentUpdatedEvent(seeded.InvoiceId, 0m, "Đã thanh toán"), update.Payload);

            var message = Assert.Single(_factory.FakeTelegramInstance.Messages.Where(m => m.Text.Contains(transactionNo)));
            Assert.Contains("Thẻ ATM / Visa / MasterCard (VNPay", message.Text);

            var email = Assert.Single(_factory.FakeEmailServiceInstance.PaymentReceiptCalls.Where(c => c.Data.HoaDonId == seeded.InvoiceId));
            Assert.Equal(seeded.TenantEmail, email.ToEmail);
            Assert.Equal("Thẻ ATM / Visa / MasterCard (VNPay)", email.Data.PhuongThuc);
            Assert.Equal(transactionNo, email.Data.MaThamChieu);
        }

        [Fact]
        public async Task RepeatedIpn_Returns02_AndKeepsASingleLedgerRow()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = WithVnPay(enabled: true);
            var query = SignedQuery(seeded.InvoiceId, transactionNo);

            await GetAsync(host, query);
            var (status, body, _) = await GetAsync(host, query);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"02\",\"Message\":\"Order already confirmed\"}", body);
            Assert.Single(await LedgerAsync(seeded.InvoiceId));
            Assert.Single(_factory.FakeTelegramInstance.Messages.Where(m => m.Text.Contains(transactionNo)));
        }

        [Fact]
        public async Task FailedPayment_Returns00_WithoutRecordingOrNotifying()
        {
            var seeded = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = WithVnPay(enabled: true);

            var (status, body, _) = await GetAsync(host, SignedQuery(seeded.InvoiceId, transactionNo, q => q["vnp_ResponseCode"] = "24"));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
            Assert.Empty(_factory.FakeTelegramInstance.Messages.Where(m => m.Text.Contains(transactionNo)));
        }

        [Fact]
        public async Task AmountNotDivisibleBy100_Returns04()
        {
            var seeded = await SeedAsync();
            using var host = WithVnPay(enabled: true);

            var (status, body, _) = await GetAsync(host, SignedQuery(seeded.InvoiceId, NewTransactionNo(), q => q["vnp_Amount"] = "200000050"));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"04\",\"Message\":\"Invalid amount\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        [Fact]
        public async Task UnknownTxnRef_Returns01()
        {
            var seeded = await SeedAsync();
            using var host = WithVnPay(enabled: true);

            var (status, body, _) = await GetAsync(host, SignedQuery(seeded.InvoiceId, NewTransactionNo(), q => q["vnp_TxnRef"] = "khong-hop-le"));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"01\",\"Message\":\"Order not found\"}", body);
            Assert.Empty(await LedgerAsync(seeded.InvoiceId));
        }

        // ---------- Lỗi hệ thống ----------

        // Không gây lỗi thật vì InvoicePaymentTransaction log Error làm fixture FAIL: thay service bằng fake trả 99.
        [Fact]
        public async Task SystemError_Returns200With99_AndHandsTheQueryToTheService()
        {
            var seeded = await SeedAsync();
            var fake = new FakeVnPayService();
            using var host = WithVnPay(enabled: true, s =>
            {
                s.RemoveAll<IVnPayPaymentService>();
                s.AddSingleton<IVnPayPaymentService>(fake);
            });
            var query = SignedQuery(seeded.InvoiceId, NewTransactionNo());

            var (status, body, _) = await GetAsync(host, query);

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal("{\"RspCode\":\"99\",\"Message\":\"Unknown error\"}", body);
            Assert.NotNull(fake.LastQuery);
            Assert.Equal(query["vnp_TxnRef"], fake.LastQuery!["vnp_TxnRef"]);
            Assert.Equal(query["vnp_OrderInfo"], fake.LastQuery["vnp_OrderInfo"]);
        }
    }
}
