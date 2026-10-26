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
    // Cổng khách thuê của VNPay, phần 2 (spec VNPay §4.1-§4.3, §8): cờ vnPayEnabled của panel, tạo link, trang kết quả
    // chỉ đọc, script mới và badge VNPay ở cổng quản lý.
    [Collection(WebTestCollection.Name)]
    public class VnPayCheckoutWebTests
    {
        private const string Password = "Password123!";
        private const string TmnCode = "TESTTMN1";
        private const string HashSecret = "test-vnpay-hash-secret";
        private const string ReturnUrl = "http://localhost/KhachThue/HoaDon/ThanhToan/VnPay/KetQua";
        private const string ResultPath = "/KhachThue/HoaDon/ThanhToan/VnPay/KetQua";
        private const string IpnPath = "/api/webhooks/payment/vnpay-ipn";

        private readonly CustomWebApplicationFactory _factory;

        public VnPayCheckoutWebTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seed(int HoaDonId, string MaHoaDon, string Tenant, string OtherTenant, string Admin);

        private async Task<Seed> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_VC_{s}", MaChiNhanh = TestCodes.MaChiNhanh("C"), DiaChi = "12 Duong Test", SoDienThoai = "0900000007", MoTa = "VC" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000007", CCCD = $"079{s[..6]}7", Email = $"vc_{s}@test.com" };
            var other = new NguoiThue { HoVaTen = $"Khac {s}", SoDienThoai = "0911000008", CCCD = $"079{s[..6]}8", Email = $"vo_{s}@test.com" };
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
                MaHoaDon = $"HD-VC-{s}",
                Thang = 9,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow.AddDays(-1),
                HanThanhToan = DateTime.UtcNow.AddDays(6),
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền phòng", DonGia = 2000000m, SoLuong = 1, TongTien = 2000000m }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            var seed = new Seed(invoice.HoaDonId, invoice.MaHoaDon, $"tvc_{s}", $"ovc_{s}", $"avc_{s}");
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Tenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = tenant.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.OtherTenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = other.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Admin, MatKhau = Password, Role = AppRole.Admin })).Success);
            return seed;
        }

        private WebApplicationFactory<Program> WithVnPay(bool enabled)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<VnPayOptions>();
                s.AddSingleton(new VnPayOptions { Enabled = enabled, TmnCode = TmnCode, HashSecret = HashSecret });
            }));

        private static HttpClient NewClient(WebApplicationFactory<Program> host)
            => host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        private static string Token(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success) match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static async Task<(HttpClient Client, string Token)> LoginAsync(HttpClient client, string username, string page)
        {
            var login = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", Token(await login.Content.ReadAsStringAsync())),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", Password)
            });
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/QuanLyNhaTro/DangNhap", content)).StatusCode);

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

        // Razor mã hóa ký tự ngoài ASCII và một số dấu nên so khớp chữ phải giải mã HTML trước.
        private static async Task<string> HtmlTextAsync(HttpResponseMessage res) => WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

        // Query kết quả/IPN hợp lệ của hóa đơn (2.000.000đ), đã ký bằng VnPayProtocol.
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

        private async Task<bool> AnyPaymentRequestAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.YeuCauThanhToanHoaDons.AnyAsync(y => y.HoaDonId == hoaDonId);
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

        // ---------- Cờ vnPayEnabled của panel ----------

        [Fact]
        public async Task Panel_ByDefault_ReportsVnPayDisabled()
        {
            var seed = await SeedAsync();
            var (tenant, _) = await LoginAsync(NewClient(_factory.WithWebHostBuilder(_ => { })), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.False((await JsonAsync(res)).GetProperty("data").GetProperty("vnPayEnabled").GetBoolean());
        }

        [Fact]
        public async Task Panel_WhenEnabled_ReportsVnPayEnabled()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.True((await JsonAsync(res)).GetProperty("data").GetProperty("vnPayEnabled").GetBoolean());
        }

        // ---------- Tạo link ----------

        [Fact]
        public async Task CreateLink_WhenDisabled_Returns404_AndResultPageToo()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: false);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            Assert.Equal(HttpStatusCode.NotFound, (await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())))).StatusCode);
        }

        [Fact]
        public async Task CreateLink_Anonymous_RedirectsToLogin()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);

            var res = await NewClient(host).SendAsync(CreateLinkRequest(seed.HoaDonId, token: null));

            Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
        }

        [Fact]
        public async Task CreateLink_WithoutAntiforgeryToken_Returns400()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token: null));

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        [Fact]
        public async Task CreateLink_OtherTenant_Returns404()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (other, token) = await LoginAsync(NewClient(host), seed.OtherTenant, "/KhachThue/HoaDon");

            var res = await other.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.False((await JsonAsync(res)).GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task CreateLink_FullyPaidInvoice_Returns400WithTheMessage()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 2_000_000m);
            using var host = WithVnPay(enabled: true);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var body = await JsonAsync(res);
            Assert.False(body.GetProperty("success").GetBoolean());
            Assert.Contains("đã được thanh toán đủ", body.GetProperty("message").GetString());
        }

        [Fact]
        public async Task CreateLink_Owner_ReturnsASignedUrl_AndWritesNothing()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, token) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.SendAsync(CreateLinkRequest(seed.HoaDonId, token));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await JsonAsync(res);
            Assert.True(body.GetProperty("success").GetBoolean());
            var url = body.GetProperty("url").GetString()!;
            Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", url);

            var query = url[(url.IndexOf('?') + 1)..].Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);
            Assert.Equal("200000000", query["vnp_Amount"]);
            Assert.Equal(ReturnUrl, query["vnp_ReturnUrl"]);
            Assert.Equal("127.0.0.1", query["vnp_IpAddr"]);
            Assert.Equal(TmnCode, query["vnp_TmnCode"]);
            Assert.True(VnPayProtocol.TryParseTxnRef(query["vnp_TxnRef"], out var hoaDonId));
            Assert.Equal(seed.HoaDonId, hoaDonId);
            Assert.True(VnPayProtocol.Verify(query, HashSecret));

            Assert.Empty(await LedgerAsync(seed.HoaDonId));
            Assert.False(await AnyPaymentRequestAsync(seed.HoaDonId));
        }

        // ---------- Trang kết quả ----------

        [Fact]
        public async Task ResultPage_Anonymous_RedirectsToLogin()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);

            var res = await NewClient(host).GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())));

            Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
        }

        [Fact]
        public async Task ResultPage_WithoutQuery_ShowsTheUnverifiedState()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("Không xác thực được kết quả từ VNPay.", await HtmlTextAsync(res));
        }

        [Fact]
        public async Task ResultPage_WrongSignature_ShowsNothingAboutTheInvoice()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            var query = SignedQuery(seed.HoaDonId, NewTransactionNo());
            var signature = query["vnp_SecureHash"];
            query["vnp_SecureHash"] = (signature[0] == '0' ? '1' : '0') + signature[1..];

            var res = await tenant.GetAsync(ResultPath + ToQueryString(query));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await HtmlTextAsync(res);
            Assert.Contains("Không xác thực được kết quả từ VNPay.", html);
            Assert.DoesNotContain(seed.MaHoaDon, html);
            Assert.DoesNotContain($"hoaDonId={seed.HoaDonId}", html);
            Assert.DoesNotContain("2.000.000", html);
            Assert.DoesNotContain("ktVnPayRecheck", html);
        }

        [Fact]
        public async Task ResultPage_InvoiceOfAnotherTenant_LooksTheSameAsAWrongSignature()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (other, _) = await LoginAsync(NewClient(host), seed.OtherTenant, "/KhachThue/HoaDon");

            var res = await other.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = await HtmlTextAsync(res);
            Assert.Contains("Không xác thực được kết quả từ VNPay.", html);
            Assert.DoesNotContain(seed.MaHoaDon, html);
            Assert.DoesNotContain($"hoaDonId={seed.HoaDonId}", html);
        }

        [Fact]
        public async Task ResultPage_Cancelled_ShowsTheCancelledState_WithALinkBackToTheInvoice()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo(), q => q["vnp_ResponseCode"] = "24")));

            var html = await HtmlTextAsync(res);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("Bạn đã hủy thanh toán.", html);
            Assert.Contains($"/KhachThue/HoaDon?hoaDonId={seed.HoaDonId}", html);
            Assert.Empty(await LedgerAsync(seed.HoaDonId));
        }

        [Fact]
        public async Task ResultPage_OtherFailureCode_ShowsTheDescription()
        {
            var seed = await SeedAsync();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo(), q => q["vnp_ResponseCode"] = "51")));

            var html = await HtmlTextAsync(res);
            Assert.Contains("Thanh toán chưa thành công", html);
            Assert.Contains("Tài khoản không đủ số dư để thực hiện giao dịch.", html);
        }

        [Fact]
        public async Task ResultPage_SuccessBeforeTheIpn_ShowsRecording_ThenTheReceiptOnceTheIpnWrote()
        {
            var seed = await SeedAsync();
            var transactionNo = NewTransactionNo();
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");
            var query = SignedQuery(seed.HoaDonId, transactionNo);
            var resultUrl = ResultPath + ToQueryString(query);

            var before = await tenant.GetAsync(resultUrl);

            var beforeHtml = await HtmlTextAsync(before);
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
            Assert.Contains("VNPay báo thanh toán thành công, hệ thống đang ghi nhận.", beforeHtml);
            Assert.Contains("ktVnPayRecheck", beforeHtml);
            Assert.Contains("/js/khachthue-vnpay.js", beforeHtml);
            Assert.Contains($"KtVnPay.watchResult({seed.HoaDonId})", beforeHtml);
            Assert.Empty(await LedgerAsync(seed.HoaDonId));

            // IPN tới sau ReturnUrl: cùng host, chữ ký đúng, cùng vnp_TransactionNo.
            using var ipn = await NewClient(host).GetAsync(IpnPath + ToQueryString(query));
            Assert.Contains("\"RspCode\":\"00\"", await ipn.Content.ReadAsStringAsync());
            var ledger = Assert.Single(await LedgerAsync(seed.HoaDonId));

            var after = await tenant.GetAsync(resultUrl);

            var afterHtml = await HtmlTextAsync(after);
            Assert.Contains("Đã ghi nhận thanh toán", afterHtml);
            Assert.Contains("2.000.000đ", afterHtml);
            Assert.Contains($"/KhachThue/HoaDon/BienLai/{ledger.LichSuThanhToanId}", afterHtml);
            Assert.DoesNotContain("ktVnPayRecheck", afterHtml);
            Assert.DoesNotContain("khachthue-vnpay.js", afterHtml);
        }

        [Fact]
        public async Task ResultPage_SuccessButTheInvoiceIsAlreadyPaid_ShowsNotRecorded()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 2_000_000m);
            using var host = WithVnPay(enabled: true);
            var (tenant, _) = await LoginAsync(NewClient(host), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync(ResultPath + ToQueryString(SignedQuery(seed.HoaDonId, NewTransactionNo())));

            var html = await HtmlTextAsync(res);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("VNPay báo đã thanh toán nhưng hóa đơn không còn nhận khoản này. Chủ trọ sẽ kiểm tra và liên hệ với bạn.", html);
            Assert.DoesNotContain("ktVnPayRecheck", html);
            Assert.DoesNotContain("khachthue-vnpay.js", html);
            Assert.Single(await LedgerAsync(seed.HoaDonId));
        }

        // ---------- Script và giao diện ----------

        [Fact]
        public async Task VnPayScript_HasASinglePostToTheVnPayEndpoint_WithTheAntiforgeryHeader_AndNoPolling()
        {
            var js = await (await NewClient(_factory.WithWebHostBuilder(_ => { })).GetAsync("/js/khachthue-vnpay.js")).Content.ReadAsStringAsync();

            Assert.Contains("RequestVerificationToken", js);
            Assert.Contains("/KhachThue/HoaDon/ThanhToan/VnPay/", js);
            Assert.Contains("HoaDonThanhToanCapNhat", js);
            Assert.Single(Regex.Matches(js, Regex.Escape("method: 'POST'")));
            Assert.DoesNotContain("setTimeout", js);
            Assert.DoesNotContain("setInterval", js);
            Assert.DoesNotContain("${", js);
            Assert.DoesNotContain("alert(", Regex.Replace(js, @"Swal\.fire", string.Empty));
            Assert.DoesNotContain("responseText", js);
            Assert.DoesNotContain("eval(", js);
            // Thông báo lỗi từ server chỉ đi vào text, không vào html của hộp thoại.
            Assert.DoesNotContain("html:", js);
        }

        [Fact]
        public async Task PanelScript_HasTheVnPayTab_AndStillNeverPosts()
        {
            var js = await (await NewClient(_factory.WithWebHostBuilder(_ => { })).GetAsync("/js/khachthue-thanhtoan.js")).Content.ReadAsStringAsync();

            Assert.Contains("vnPayEnabled", js);
            Assert.Contains("Thanh toán qua VNPay", js);
            Assert.Contains("Số tiền sẽ thanh toán", js);
            Assert.Contains("window.KtVnPay.start(", js);
            Assert.DoesNotContain("method: 'POST'", js);
            Assert.DoesNotContain("$.post", js);
        }

        [Fact]
        public async Task InvoicePage_LoadsTheVnPayScript_BeforeThePanelScript()
        {
            var seed = await SeedAsync();
            var (tenant, _) = await LoginAsync(NewClient(_factory.WithWebHostBuilder(_ => { })), seed.Tenant, "/KhachThue/HoaDon");

            var html = await (await tenant.GetAsync("/KhachThue/HoaDon")).Content.ReadAsStringAsync();

            var vnPay = html.IndexOf("/js/khachthue-vnpay.js", StringComparison.Ordinal);
            var panel = html.IndexOf("/js/khachthue-thanhtoan.js", StringComparison.Ordinal);
            Assert.True(vnPay >= 0, "Trang hóa đơn không nạp khachthue-vnpay.js");
            Assert.True(vnPay < panel, "khachthue-vnpay.js phải nạp trước khachthue-thanhtoan.js");
        }

        [Fact]
        public async Task ManagerInvoicePage_RendersTheVnPayBadge()
        {
            var seed = await SeedAsync();
            var (admin, _) = await LoginAsync(NewClient(_factory.WithWebHostBuilder(_ => { })), seed.Admin, "/QuanLyNhaTro/QuanLyHoaDon");

            var html = await (await admin.GetAsync("/QuanLyNhaTro/QuanLyHoaDon")).Content.ReadAsStringAsync();

            Assert.Contains("ls.laVnPay", html);
            Assert.Contains("'TXN '", html);
        }
    }
}
