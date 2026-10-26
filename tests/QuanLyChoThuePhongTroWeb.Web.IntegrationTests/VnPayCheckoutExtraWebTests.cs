using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Kiểm bổ sung (tester) cho VNPay phần 2: đủ sáu trạng thái trang kết quả (kể cả ba nhánh KhongGhiNhan và dòng
    // "VNPay báo bạn đã trả"), mã 07, tạo link ở các trạng thái hóa đơn khác nhau, phân quyền, panel JSON (tab ẩn khi
    // tắt/hết nợ), badge VNPay ở cổng quản lý và kiểm tĩnh JS/CSS/markup theo frontend_code_review.md.
    // Bổ sung cho VnPayCheckoutWebTests, không thay thế.
    [Collection(WebTestCollection.Name)]
    public class VnPayCheckoutExtraWebTests
    {
        private const string Password = "Password123!";
        private const string TmnCode = "TESTTMN1";
        private const string HashSecret = "test-vnpay-hash-secret";
        private const string ResultPath = "/KhachThue/HoaDon/ThanhToan/VnPay/KetQua";
        private const string IpnPath = "/api/webhooks/payment/vnpay-ipn";
        private const string UnverifiedTitle = "Không xác thực được kết quả từ VNPay.";
        private const string NotRecordedTitle = "VNPay báo đã thanh toán nhưng hóa đơn không còn nhận khoản này. Chủ trọ sẽ kiểm tra và liên hệ với bạn.";

        private readonly CustomWebApplicationFactory _factory;

        public VnPayCheckoutExtraWebTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seed(int HoaDonId, string MaHoaDon, string Tenant, string OtherTenant, string Admin, string NoProfile);

        private async Task<Seed> SeedAsync(
            decimal tongTien = 2_000_000m,
            TrangThaiPhatHanhHoaDon phatHanh = TrangThaiPhatHanhHoaDon.DaGui,
            string? maHoaDonPrefix = null,
            bool isDeleted = false,
            bool sent = true)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_VX_{s}", MaChiNhanh = TestCodes.MaChiNhanh("X"), DiaChi = "12 Duong Test", SoDienThoai = "0900000017", MoTa = "VX" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000017", CCCD = $"079{s[..6]}7", Email = $"vx_{s}@test.com" };
            var other = new NguoiThue { HoVaTen = $"Khac {s}", SoDienThoai = "0911000018", CCCD = $"079{s[..6]}8", Email = $"vy_{s}@test.com" };
            db.PhongTros.Add(room);
            db.NguoiThues.AddRange(tenant, other);
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
                MaHoaDon = $"{maHoaDonPrefix ?? "HD-VX-"}{s}",
                Thang = 9,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiPhatHanh = phatHanh,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = sent ? DateTime.UtcNow.AddDays(-1) : null,
                HanThanhToan = DateTime.UtcNow.AddDays(6),
                IsDeleted = isDeleted,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền phòng", DonGia = tongTien, SoLuong = 1, TongTien = tongTien }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            var seed = new Seed(invoice.HoaDonId, invoice.MaHoaDon, $"tvx_{s}", $"ovx_{s}", $"avx_{s}", $"nvx_{s}");
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Tenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = tenant.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.OtherTenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = other.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Admin, MatKhau = Password, Role = AppRole.Admin })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.NoProfile, MatKhau = Password, Role = AppRole.KhachThue })).Success);
            return seed;
        }

        private WebApplicationFactory<Program> WithVnPay(bool enabled, string tmnCode = TmnCode)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<VnPayOptions>();
                s.AddSingleton(new VnPayOptions { Enabled = enabled, TmnCode = tmnCode, HashSecret = HashSecret });
            }));

        private static HttpClient NewClient(WebApplicationFactory<Program> host)
            => host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        private static string Token(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success) match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static async Task<HttpClient> LoginOnlyAsync(HttpClient client, string username)
        {
            var login = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", Token(await login.Content.ReadAsStringAsync())),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", Password)
            });
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/QuanLyNhaTro/DangNhap", content)).StatusCode);
            return client;
        }

        private static async Task<(HttpClient Client, string Token)> LoginAsync(HttpClient client, string username, string page)
        {
            await LoginOnlyAsync(client, username);
            var authed = await client.GetAsync(page);
            Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
            return (client, Token(await authed.Content.ReadAsStringAsync()));
        }

        private static HttpRequestMessage CreateLinkRequest(int hoaDonId, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/KhachThue/HoaDon/ThanhToan/VnPay/{hoaDonId}");
            if (token != null)
            {
                request.Headers.Add("RequestVerificationToken", token);
            }

            return request;
        }

        private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        private static async Task<string> HtmlTextAsync(HttpResponseMessage res) => WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

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

        private static string NewTransactionNo() => "VNP" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private async Task<List<LichSuThanhToan>> LedgerAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.LichSuThanhToans.AsNoTracking().Where(l => l.HoaDonId == hoaDonId).ToListAsync();
        }

        private async Task AddLedgerAsync(int hoaDonId, decimal soTien)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = hoaDonId,
                MaGiaoDich = $"GD-{Guid.NewGuid():N}"[..24],
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                NgayThanhToan = DateTime.UtcNow.AddDays(-1),
                NgayXacNhan = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        private async Task SoftDeleteLedgerAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var row in await db.LichSuThanhToans.Where(l => l.HoaDonId == hoaDonId).ToListAsync())
            {
                row.IsDeleted = true;
            }

            await db.SaveChangesAsync();
        }

        private async Task CancelInvoiceAfterSendingAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = await db.HoaDons.SingleAsync(h => h.HoaDonId == hoaDonId);
            invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
            invoice.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        private static async Task<string> RunIpnAsync(WebApplicationFactory<Program> host, Dictionary<string, string> query)
        {
            using var res = await NewClient(host).GetAsync(IpnPath + ToQueryString(query));
            return await res.Content.ReadAsStringAsync();
        }

        private static Dictionary<string, string> ParseQuery(string url)
            => url[(url.IndexOf('?') + 1)..].Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);

        // ---------- Panel JSON: cờ tab VNPay ----------

        [Fact]
        public async Task Panel_EnabledAndPartiallyPaid_OffersTheVnPayTabWithTheRemainingBalance()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 500_000m);
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var data = (await JsonAsync(await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"))).GetProperty("data");

            Assert.True(data.GetProperty("vnPayEnabled").GetBoolean());
            Assert.True(data.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Equal(1_500_000m, data.GetProperty("conLai").GetDecimal());
        }

        [Fact]
        public async Task Panel_EnabledButFullyPaid_HasNothingLeftToPay_SoTheScriptShowsNoTab()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 2_000_000m);
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var data = (await JsonAsync(await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"))).GetProperty("data");

            Assert.True(data.GetProperty("vnPayEnabled").GetBoolean());
            Assert.Equal(0m, data.GetProperty("conLai").GetDecimal());
            Assert.False(data.GetProperty("coTheThanhToan").GetBoolean());
        }

        [Fact]
        public async Task Panel_DisabledWhilePartiallyPaid_StillOffersOnlyTheQrBlock()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 500_000m);
            using var host = WithVnPay(enabled: false);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var data = (await JsonAsync(await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"))).GetProperty("data");

            Assert.False(data.GetProperty("vnPayEnabled").GetBoolean());
            Assert.True(data.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Equal(12, data.GetProperty("deeplinks").GetArrayLength());
        }

        // ---------- Tạo link ----------

        [Fact]
        public async Task CreateLink_AsAdmin_IsRefused_AndNoLinkIsReturned()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (admin, token) = await LoginAsync(NewClient(host), seed.Admin, "/QuanLyNhaTro/QuanLyHoaDon");

            var res = await admin.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
            Assert.DoesNotContain("vpcpay", await res.Content.ReadAsStringAsync());
            Assert.Empty(await LedgerAsync(seed.HoaDonId));
        }

        [Fact]
        public async Task CreateLink_WithGet_IsNotAnEndpoint()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/VnPay/{seed.HoaDonId}");

            Assert.True(res.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"Nhận {(int)res.StatusCode}");
            Assert.DoesNotContain("vpcpay", await res.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        public async Task CreateLink_InvoiceNotPublishedYet_Returns404(TrangThaiPhatHanhHoaDon state)
        {
            var seed = await SeedAsync(phatHanh: state, sent: false);
            using var host = WithVnPay(enabled: true);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.False((await JsonAsync(res)).GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task CreateLink_InvoiceCancelledAfterSending_Returns400WithTheReason()
        {
            var seed = await SeedAsync();
            await CancelInvoiceAfterSendingAsync(seed.HoaDonId);
            using var host = WithVnPay(enabled: true);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("không còn ở trạng thái nhận thanh toán", (await JsonAsync(res)).GetProperty("message").GetString());
        }

        [Fact]
        public async Task CreateLink_FractionalInvoice_Returns400WithTheFractionalMessage()
        {
            var seed = await SeedAsync(tongTien: 1_500_000.5m);
            using var host = WithVnPay(enabled: true);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal("Hóa đơn có số tiền lẻ, vui lòng liên hệ quản lý để lập lại hóa đơn.", (await JsonAsync(res)).GetProperty("message").GetString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(2147483647)]
        public async Task CreateLink_UnknownOrInvalidInvoiceId_Returns404_NeverA500(int hoaDonId)
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(hoaDonId, token));

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CreateLink_AfterAPartialPayment_UsesTheRemainingBalance_AndAnAsciiOrderInfo()
        {
            var seed = await SeedAsync(maHoaDonPrefix: "HĐ-Tháng 9 #");
            await AddLedgerAsync(seed.HoaDonId, 500_000m);
            using var host = WithVnPay(enabled: true);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var query = ParseQuery((await JsonAsync(res)).GetProperty("url").GetString()!);
            Assert.Equal("150000000", query["vnp_Amount"]);
            Assert.StartsWith("Thanh toan hoa don HD-Thang 9 ", query["vnp_OrderInfo"]);
            Assert.All(query["vnp_OrderInfo"], c => Assert.True(c < 128, $"Ký tự ngoài ASCII trong vnp_OrderInfo: '{c}'"));
            Assert.Matches(@"^\d{14}$", query["vnp_CreateDate"]);
            Assert.Matches(@"^\d{14}$", query["vnp_ExpireDate"]);
            Assert.True(string.CompareOrdinal(query["vnp_ExpireDate"], query["vnp_CreateDate"]) > 0);
            Assert.True(VnPayProtocol.Verify(query, HashSecret));
            Assert.Single(await LedgerAsync(seed.HoaDonId));
        }

        [Fact]
        public async Task CreateLink_TenantAccountWithoutATenantProfile_Returns401()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var client = await LoginOnlyAsync(NewClient(host), seed.NoProfile);
            // Trang hóa đơn của tài khoản này chỉ trả một dòng chữ (không có layout nên không có token); lấy token từ Dashboard.
            var page = await client.GetAsync("/KhachThue/Dashboard");
            var token = Token(await page.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrEmpty(token), $"Không lấy được token chống giả mạo (trang Dashboard trả {(int)page.StatusCode})");

            var res = await client.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
            Assert.False((await JsonAsync(res)).GetProperty("success").GetBoolean());
        }

        // ---------- Trang kết quả: đủ sáu trạng thái ----------

        [Fact]
        public async Task ResultPage_Code07_ShowsTheSuspiciousTransactionSentence_AndNothingIsRecorded()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo(), q => q["vnp_ResponseCode"] = "07")));

            var html = await HtmlTextAsync(res);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("Thanh toán chưa thành công", html);
            Assert.Contains("Giao dịch đang được VNPay kiểm tra (mã 07). Vui lòng liên hệ chủ trọ trước khi thanh toán lại.", html);
            Assert.DoesNotContain("ktVnPayRecheck", html);
            Assert.Empty(await LedgerAsync(seed.HoaDonId));
        }

        [Fact]
        public async Task ResultPage_UnknownCode_ShowsTheCode_AndEncodesIt()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo(), q => q["vnp_ResponseCode"] = "42<b>x</b>")));

            var raw = await res.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.DoesNotContain("<b>x</b>", raw);
            Assert.Contains("&lt;b&gt;x&lt;/b&gt;", raw);
            Assert.Contains("Giao dịch không thành công (mã 42<b>x</b>)", WebUtility.HtmlDecode(raw));
        }

        [Fact]
        public async Task ResultPage_SignedForAnotherMerchantCode_IsUnverified_AndLeaksNothing()
        {
            var seed = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, transactionNo, q => q["vnp_TmnCode"] = "OTHERTMN")));

            var html = await HtmlTextAsync(res);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains(UnverifiedTitle, html);
            Assert.DoesNotContain(seed.MaHoaDon, html);
            Assert.DoesNotContain(transactionNo, html);
            Assert.DoesNotContain($"hoaDonId={seed.HoaDonId}", html);
            Assert.DoesNotContain("2.000.000", html);
            Assert.DoesNotContain("khachthue-vnpay.js", html);
        }

        [Fact]
        public async Task ResultPage_AmountChangedAfterSigning_IsUnverified_EvenForAnInvoiceThatIsOwnedByTheUser()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            var query = SignedQuery(seed.HoaDonId, NewTransactionNo());
            query["vnp_Amount"] = "100";

            var res = await tenant.GetAsync(ResultPath + ToQueryString(query));

            Assert.Contains(UnverifiedTitle, await HtmlTextAsync(res));
        }

        [Fact]
        public async Task ResultPage_SuccessButTheInvoiceWasCancelledAfterSending_ShowsNotRecorded()
        {
            var seed = await SeedAsync();
            await CancelInvoiceAfterSendingAsync(seed.HoaDonId);
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())));

            var html = await HtmlTextAsync(res);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains(NotRecordedTitle, html);
            Assert.DoesNotContain("ktVnPayRecheck", html);
            Assert.DoesNotContain("khachthue-vnpay.js", html);
            Assert.Contains($"/KhachThue/HoaDon?hoaDonId={seed.HoaDonId}", html);
            Assert.Empty(await LedgerAsync(seed.HoaDonId));
        }

        [Fact]
        public async Task ResultPage_SuccessButTheLedgerRowOfThisTransactionWasVoided_ShowsNotRecorded()
        {
            var seed = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            var query = SignedQuery(seed.HoaDonId, transactionNo);
            Assert.Contains("\"RspCode\":\"00\"", await RunIpnAsync(host, query));
            await SoftDeleteLedgerAsync(seed.HoaDonId);

            var res = await tenant.GetAsync(ResultPath + ToQueryString(query));

            var html = await HtmlTextAsync(res);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains(NotRecordedTitle, html);
            Assert.DoesNotContain("Đã ghi nhận thanh toán", html);
            Assert.DoesNotContain("ktVnPayRecheck", html);
        }

        [Fact]
        public async Task ResultPage_Recorded_ForTheExactAmount_ShowsNoExtraVnPayLine()
        {
            var seed = await SeedAsync();
            var query = SignedQuery(seed.HoaDonId, NewTransactionNo());
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            Assert.Contains("\"RspCode\":\"00\"", await RunIpnAsync(host, query));

            var html = await HtmlTextAsync(await tenant.GetAsync(ResultPath + ToQueryString(query)));

            Assert.Contains("Đã ghi nhận thanh toán", html);
            Assert.Contains("2.000.000đ", html);
            Assert.DoesNotContain("VNPay báo bạn đã trả", html);
        }

        [Fact]
        public async Task ResultPage_Recorded_WithASurplus_ShowsTheLedgerAmount_AndWhatVnPayReported()
        {
            var seed = await SeedAsync();
            var query = SignedQuery(seed.HoaDonId, NewTransactionNo(), q => q["vnp_Amount"] = "250000000");
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            Assert.Contains("\"RspCode\":\"00\"", await RunIpnAsync(host, query));
            var ledger = Assert.Single(await LedgerAsync(seed.HoaDonId));
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);

            var html = await HtmlTextAsync(await tenant.GetAsync(ResultPath + ToQueryString(query)));

            Assert.Contains("Đã ghi nhận thanh toán", html);
            Assert.Contains("2.000.000đ", html);
            Assert.Contains("VNPay báo bạn đã trả 2.500.000đ", html);
            Assert.Contains($"/KhachThue/HoaDon/BienLai/{ledger.LichSuThanhToanId}", html);
        }

        [Fact]
        public async Task ResultPage_Recorded_AfterAnEarlierPartialPayment_ShowsWhatWasActuallyRecorded()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 500_000m);
            var query = SignedQuery(seed.HoaDonId, NewTransactionNo());
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            Assert.Contains("\"RspCode\":\"00\"", await RunIpnAsync(host, query));

            var html = await HtmlTextAsync(await tenant.GetAsync(ResultPath + ToQueryString(query)));

            Assert.Contains("Đã ghi nhận thanh toán", html);
            Assert.Contains("1.500.000đ", html);
            Assert.Contains("VNPay báo bạn đã trả 2.000.000đ", html);
        }

        [Fact]
        public async Task ResultPage_AsAdmin_IsNotShown()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (admin, _) = await LoginAsync(NewClient(host), seed.Admin, "/QuanLyNhaTro/QuanLyHoaDon");

            var res = await admin.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())));

            Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
        }

        [Fact]
        public async Task ResultPage_TenantAccountWithoutAProfile_ShowsTheUnverifiedState()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var client = await LoginOnlyAsync(NewClient(host), seed.NoProfile);

            var res = await client.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await HtmlTextAsync(res);
            Assert.Contains(UnverifiedTitle, html);
            Assert.DoesNotContain(seed.MaHoaDon, html);
        }

        [Fact]
        public async Task ResultPage_Disabled_Returns404_ForEveryQuery()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: false);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync(ResultPath)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo(), q => q["vnp_ResponseCode"] = "24")))).StatusCode);
        }

        // ---------- Markup của trang kết quả (frontend_code_review.md) ----------

        [Fact]
        public async Task ResultPage_Markup_HasNoHardcodedColorsOrInlineStyles_ButHasTheMobileButtonsAndViewport()
        {
            var seed = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            // Trạng thái "đang ghi nhận" có đủ cả ba nút/liên kết nên là trang phong phú nhất.
            var raw = await (await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, transactionNo)))).Content.ReadAsStringAsync();

            // Chỉ khối nội dung của view: từ thẻ empty đến hết div empty-action (không gồm layout).
            var start = raw.IndexOf("class=\"empty\"", StringComparison.Ordinal);
            var actionsStart = raw.IndexOf("empty-action", start, StringComparison.Ordinal);
            var body = raw[start..raw.IndexOf("</div>", actionsStart, StringComparison.Ordinal)];
            Assert.Contains("name=\"viewport\"", raw);
            Assert.DoesNotContain("bg-white", body);
            Assert.DoesNotContain("text-dark", body);
            Assert.DoesNotContain("bg-light", body);
            Assert.DoesNotContain("style=\"", body);
            Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", Regex.Replace(body, @"href=""[^""]*""", string.Empty));
            // Mỗi nút/liên kết hành động cao đủ 44px (btn-lg) và đầy bề ngang trên điện thoại (w-100).
            var actions = Regex.Matches(body, @"<(?:a|button)\b[^>]*class=""btn [^""]*""[^>]*>");
            Assert.True(actions.Count >= 2, "Trang đang ghi nhận phải có nút Kiểm tra lại và nút quay lại");
            foreach (Match action in actions)
            {
                Assert.Contains("btn-lg", action.Value);
                Assert.Contains("w-100", action.Value);
            }

            // Không in lại bất kỳ tham số nào của query VNPay.
            Assert.DoesNotContain(transactionNo, raw);
            Assert.DoesNotContain("vnp_", body);
        }

        [Fact]
        public async Task ResultPage_WatchScript_ReceivesOnlyTheInvoiceNumber_AndOnlyInTheRecordingState()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var recording = await (await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())))).Content.ReadAsStringAsync();
            var cancelled = await (await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo(), q => q["vnp_ResponseCode"] = "24")))).Content.ReadAsStringAsync();
            var unverified = await (await tenant.GetAsync(ResultPath)).Content.ReadAsStringAsync();

            Assert.Matches(@"<script>KtVnPay\.watchResult\(\d+\);</script>", recording);
            Assert.Contains($"KtVnPay.watchResult({seed.HoaDonId});", recording);
            foreach (var page in new[] { cancelled, unverified })
            {
                Assert.DoesNotContain("KtVnPay", page);
                Assert.DoesNotContain("khachthue-vnpay.js", page);
            }
        }

        // ---------- Cổng quản lý: badge VNPay ----------

        [Fact]
        public async Task ManagerDetailJson_MarksTheVnPayRow_AndCarriesTheTransactionNumberAsReference()
        {
            var seed = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = WithVnPay(enabled: true);
            Assert.Contains("\"RspCode\":\"00\"", await RunIpnAsync(host, SignedQuery(seed.HoaDonId, transactionNo)));
            var (admin, _) = await LoginAsync(NewClient(host), seed.Admin, "/QuanLyNhaTro/QuanLyHoaDon");

            var res = await admin.GetAsync($"/HoaDon/GetById/{seed.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var row = (await JsonAsync(res)).GetProperty("lichSuThanhToans").EnumerateArray().Single();
            Assert.True(row.GetProperty("laVnPay").GetBoolean());
            Assert.False(row.GetProperty("laSePay").GetBoolean());
            Assert.Equal(transactionNo, row.GetProperty("maThamChieu").GetString());
            Assert.Equal("VNPAY-" + transactionNo, row.GetProperty("maGiaoDich").GetString());
        }

        [Fact]
        public async Task ManagerInvoicePage_EscapesTheVnPayReference_AndKeepsTheSePayBadge()
        {
            var seed = await SeedAsync();
            var (admin, _) = await LoginAsync(NewClient(_factory.WithWebHostBuilder(_ => { })), seed.Admin, "/QuanLyNhaTro/QuanLyHoaDon");

            var html = await (await admin.GetAsync("/QuanLyNhaTro/QuanLyHoaDon")).Content.ReadAsStringAsync();

            Assert.Contains("escapeHtml(ls.maThamChieu ? 'TXN ' + ls.maThamChieu : ls.maGiaoDich)", html);
            Assert.Contains("escapeHtml(ls.maThamChieu ? 'REF ' + ls.maThamChieu : ls.maGiaoDich)", html);
            Assert.Contains("badge bg-blue-lt me-1\">VNPay</span>", html);
            Assert.Contains("badge bg-green-lt me-1\">SePay</span>", html);
        }

        // ---------- Kiểm tĩnh JS và CSS ----------

        private async Task<string> StaticAsync(string path)
            => await (await NewClient(_factory.WithWebHostBuilder(_ => { })).GetAsync(path)).Content.ReadAsStringAsync();

        [Fact]
        public async Task VnPayScript_EscapesByDesign_ChecksTheUrlBeforeRedirecting_AndGuardsDoubleClicks()
        {
            var js = await StaticAsync("/js/khachthue-vnpay.js");

            // Id đi vào URL qua encodeURIComponent, chỉ chuyển trang khi link là https và server báo thành công.
            Assert.Contains("encodeURIComponent(hoaDonId)", js);
            Assert.Contains("res.success", js);
            Assert.Contains("res.url.indexOf('https://') === 0", js);
            Assert.Single(Regex.Matches(js, @"location\.assign\("));
            // Chống bấm đúp: bỏ qua khi nút đang disabled, disable + spinner khi gửi, hoàn nút khi lỗi.
            Assert.Contains("$button.prop('disabled')", js);
            Assert.Contains("spinner-border", js);
            Assert.Contains("prop('disabled', false)", js);
            // Lỗi không im lặng: có .fail và Swal; thông báo server chỉ vào text.
            Assert.Contains(".fail(", js);
            Assert.Contains("Swal.fire", js);
            Assert.Contains("text: message", js);
            // Mọi lần gọi .html( chỉ nhận chuỗi cố định hoặc html gốc của nút, không nhận dữ liệu server.
            foreach (Match call in Regex.Matches(js, @"\.html\(([^;]*)\)"))
            {
                var argument = call.Groups[1].Value.Trim();
                Assert.True(argument.Length == 0 || argument.StartsWith("originalHtml") || argument.StartsWith("'<span"),
                    "Gọi .html( với dữ liệu không phải chuỗi cố định: " + call.Value);
            }

            Assert.DoesNotContain("res.message +", js);
            Assert.DoesNotContain("+ res.message", js);
            Assert.DoesNotContain("innerHTML", js);
            Assert.DoesNotContain("document.write", js);
            // Token chỉ ở header, không nằm trên URL.
            Assert.DoesNotContain("?__RequestVerificationToken", js);
            Assert.DoesNotContain("data: {", js);
        }

        [Fact]
        public async Task PanelScript_ShowsTheTabsOnlyWhenVnPayIsEnabledAndThereIsSomethingToPay()
        {
            var js = await StaticAsync("/js/khachthue-thanhtoan.js");

            var paidInFull = js.IndexOf("if (p.conLai <= 0)", StringComparison.Ordinal);
            var cannotPay = js.IndexOf("if (!p.coTheThanhToan)", StringComparison.Ordinal);
            var tabs = js.IndexOf("p.vnPayEnabled ? renderTabs(p) : renderTransfer(p)", StringComparison.Ordinal);
            Assert.True(paidInFull >= 0 && cannotPay >= 0 && tabs >= 0, "Thiếu một trong ba nhánh của renderMain");
            Assert.True(paidInFull < tabs, "Nhánh hết nợ phải xét trước khi dựng tab");
            Assert.True(cannotPay < tabs, "Nhánh không thể thanh toán (X5) phải xét trước khi dựng tab");
            // QR vẫn là tab mặc định.
            Assert.Matches(@"id=""ktPayTabQrBtn""[^>]*aria-selected=""true""", js);
            Assert.Contains("tab-pane fade show active\" id=\"ktPayTabQr\"", js);
        }

        [Fact]
        public async Task PanelScript_TabsAreWiredForAccessibility_AndTheVnPayBlockOnlyPrintsTheRemainingBalanceThroughVnd()
        {
            var js = await StaticAsync("/js/khachthue-thanhtoan.js");

            Assert.Equal(2, Regex.Matches(js, @"data-bs-toggle=""tab""").Count);
            foreach (var id in new[] { "ktPayTabQr", "ktPayTabVnPay" })
            {
                Assert.Contains($"data-bs-target=\"#{id}\"", js);
                Assert.Contains($"aria-controls=\"{id}\"", js);
                Assert.Contains($"id=\"{id}\"", js);
            }

            Assert.Contains("role=\"tablist\"", js);
            Assert.Equal(2, Regex.Matches(js, @"role=""tabpanel""").Count);
            var vnPayBlock = Regex.Match(js, @"function renderVnPay\(p\)\s*\{.*?\n    \}", RegexOptions.Singleline).Value;
            Assert.NotEmpty(vnPayBlock);
            // Chỉ một dữ liệu server (số còn lại), luôn qua vnd().
            Assert.Single(Regex.Matches(vnPayBlock, @"\bp\.\w+"));
            Assert.Contains("vnd(p.conLai)", vnPayBlock);
            Assert.DoesNotContain("href=", vnPayBlock);
            Assert.Contains("Số tiền sẽ thanh toán", vnPayBlock);
            Assert.Contains("id=\"ktPayVnPay\"", vnPayBlock);
            // Nút có vùng bấm lớn và đầy bề ngang trên điện thoại; thiếu script phụ thì báo lỗi, không im lặng.
            Assert.Contains("btn btn-primary btn-lg w-100 w-sm-auto", vnPayBlock);
            Assert.Contains("if (!window.KtVnPay)", js);
            Assert.Contains("window.KtVnPay.start(current.hoaDonId, $(this), reloadPanel)", js);
        }

        [Fact]
        public async Task Css_TabLinksAreAtLeast44px_WrapLongLabels_AndUseNoHardcodedColors()
        {
            var css = await StaticAsync("/css/hoadon.css");

            var rule = Regex.Match(css, @"\.kt-pay-tabs \.nav-link\s*\{([^}]*)\}");
            Assert.True(rule.Success, "Thiếu rule .kt-pay-tabs .nav-link");
            Assert.Contains("min-height: 44px", rule.Groups[1].Value);
            Assert.Contains("white-space: normal", rule.Groups[1].Value);
            Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgb\(|rgba\(|hsl\(|(?<![-\w])(?:white|black)(?![-\w])", rule.Groups[1].Value);
            Assert.DoesNotMatch(@"\bwidth\s*:\s*\d+px", rule.Groups[1].Value);
            // Lớp w-sm-auto mà các nút VNPay dùng phải có sẵn.
            Assert.Matches(@"\.w-sm-auto\s*\{\s*width:\s*auto\s*!important;", css);
        }

        // Ở 375 px, bảng danh sách hóa đơn 6 cột từng kéo cả trang rộng 718 px (site.css ép .table-responsive overflow visible);
        // mở modal thì Bootstrap đệm padding-right 343px và modal chi tiết (cả panel thanh toán) chỉ còn 32 px.
        // Bảng phải cuộn ngang trong khung của nó.
        [Fact]
        public async Task TenantInvoiceList_ScrollsInsideItsOwnBox_SoTheDetailModalKeepsTheFullWidth()
        {
            var css = await StaticAsync("/css/hoadon.css");
            var rule = Regex.Match(css, @"\.table-responsive\.kt-hoadon-list-scroll\s*\{([^}]*)\}");
            Assert.True(rule.Success, "Thiếu rule .table-responsive.kt-hoadon-list-scroll");
            Assert.Contains("overflow-x: auto !important", rule.Groups[1].Value);

            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            var html = await (await tenant.GetAsync("/KhachThue/HoaDon")).Content.ReadAsStringAsync();

            Assert.Matches(@"<div class=""table-responsive kt-hoadon-list-scroll"">\s*<table[^>]*id=""hoaDonTable""", html);
        }
    }
}
