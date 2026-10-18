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
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Cổng quản lý của thanh toán trực tuyến đợt 1 phần 4 (spec §6.3, §7.2): gửi lại biên lai theo quyền chi nhánh, nhãn SePay và mã
    // tham chiếu trong chi tiết hóa đơn, trang lỗi biên lai thân thiện, QR của quản lý dùng mã TTPTHD{Id} và số còn lại.
    [Collection(WebTestCollection.Name)]
    public class ManagerReceiptTests
    {
        private const string Password = "Password123!";

        private readonly CustomWebApplicationFactory _factory;

        public ManagerReceiptTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seed(
            int HoaDonId, string MaHoaDon, string TenantEmail,
            string Tenant, string OtherTenant, string StaffA, string StaffB, string Admin);

        private async Task<Seed> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branchA = new ChiNhanh { TenChiNhanh = $"CN_A_{s}", MaChiNhanh = TestCodes.MaChiNhanh("A"), DiaChi = $"1 Đường A {s}", SoDienThoai = "0900000001", MoTa = "A" };
            var branchB = new ChiNhanh { TenChiNhanh = $"CN_B_{s}", MaChiNhanh = TestCodes.MaChiNhanh("B"), DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            db.ChiNhanhs.AddRange(branchA, branchB);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branchA.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var email = $"mr_{s}@test.com";
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000006", CCCD = $"079{s[..6]}6", Email = email };
            var other = new NguoiThue { HoVaTen = $"Khac {s}", SoDienThoai = "0911000007", CCCD = $"079{s[..6]}7", Email = $"mo_{s}@test.com" };
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
                MaHoaDon = $"HD-MR-{s}",
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

            var seed = new Seed(invoice.HoaDonId, invoice.MaHoaDon, email, $"tmr_{s}", $"omr_{s}", $"sa_{s}", $"sb_{s}", $"ad_{s}");
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Tenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = tenant.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.OtherTenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = other.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.StaffA, MatKhau = Password, Role = AppRole.NhanVien })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.StaffB, MatKhau = Password, Role = AppRole.NhanVien })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Admin, MatKhau = Password, Role = AppRole.Admin })).Success);

            var staffA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == seed.StaffA);
            var staffB = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == seed.StaffB);
            db.NhanVienChiNhanhs.AddRange(
                new NhanVienChiNhanh { NguoiDungId = staffA.NguoiDungId, ChiNhanhId = branchA.ChiNhanhId, IsActive = true, NguoiPhanCongId = staffA.NguoiDungId, NgayPhanCong = DateTime.UtcNow },
                new NhanVienChiNhanh { NguoiDungId = staffB.NguoiDungId, ChiNhanhId = branchB.ChiNhanhId, IsActive = true, NguoiPhanCongId = staffB.NguoiDungId, NgayPhanCong = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return seed;
        }

        private async Task<int> AddLedgerAsync(
            int hoaDonId, decimal soTien, string maGiaoDich, DateTime ngayThanhToanUtc, bool isDeleted = false,
            PhuongThucThanhToan phuongThuc = PhuongThucThanhToan.ChuyenKhoan)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = new LichSuThanhToan
            {
                HoaDonId = hoaDonId,
                MaGiaoDich = maGiaoDich,
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = ngayThanhToanUtc,
                NgayXacNhan = ngayThanhToanUtc,
                IsDeleted = isDeleted
            };
            db.LichSuThanhToans.Add(row);
            await db.SaveChangesAsync();
            return row.LichSuThanhToanId;
        }

        private static string NewKey(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

        private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        private static string Token(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success) match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // Đăng nhập rồi lấy token antiforgery của phiên từ một trang đã xác thực.
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

        private static HttpRequestMessage Resend(int lichSuThanhToanId, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/QuanLyNhaTro/HoaDon/BienLai/{lichSuThanhToanId}/GuiLai");
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

        private List<(string ToEmail, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.PaymentReceiptEmail Data)> ReceiptsOf(int lichSuThanhToanId)
            => _factory.FakeEmailServiceInstance.PaymentReceiptCalls.Where(c => c.Data.LichSuThanhToanId == lichSuThanhToanId).ToList();

        // Razor mã hóa ký tự ngoài ASCII thành &#x...; nên so khớp chữ tiếng Việt phải giải mã HTML trước.
        private static async Task<string> HtmlTextAsync(HttpResponseMessage res) => WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

        // ---------- Gửi lại biên lai ----------

        [Fact]
        public async Task Resend_StaffOfTheBranch_Returns200_AndSendsOneReceiptToTheContractHolder()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var (staff, token) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await staff.SendAsync(Resend(row, token));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var json = await JsonAsync(res);
            Assert.True(json.GetProperty("success").GetBoolean());
            Assert.Equal("Đã gửi lại biên lai.", json.GetProperty("message").GetString());
            var (toEmail, data) = Assert.Single(ReceiptsOf(row));
            Assert.Equal(seed.TenantEmail, toEmail);
            Assert.Equal(seed.HoaDonId, data.HoaDonId);
            Assert.Equal($"https://tro.test/KhachThue/HoaDon/BienLai/{row}", data.LinkXemBienLai);
            Assert.Equal("Chuyển khoản 24/7 (VietQR, tự động)", data.PhuongThuc);
            Assert.False(string.IsNullOrWhiteSpace(data.MaThamChieu));
        }

        [Fact]
        public async Task Resend_ACashRow_IsAllowed_WithoutABankReference()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("TM"), DateTime.UtcNow.AddHours(-2), phuongThuc: PhuongThucThanhToan.TienMat);
            var (staff, token) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await staff.SendAsync(Resend(row, token));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var data = Assert.Single(ReceiptsOf(row)).Data;
            Assert.Equal("Tiền mặt", data.PhuongThuc);
            Assert.Null(data.MaThamChieu);
        }

        [Fact]
        public async Task Resend_Admin_Returns200()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var (admin, token) = await LoginAsync(NewClient(), seed.Admin, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await admin.SendAsync(Resend(row, token));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Single(ReceiptsOf(row));
        }

        [Fact]
        public async Task Resend_StaffOfAnotherBranch_Returns403_AndSendsNothing()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var (staffB, token) = await LoginAsync(NewClient(), seed.StaffB, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await staffB.SendAsync(Resend(row, token));

            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal("Bạn không có quyền gửi biên lai này.", (await JsonAsync(res)).GetProperty("message").GetString());
            Assert.Empty(ReceiptsOf(row));
        }

        [Fact]
        public async Task Resend_AVoidedRow_Returns400_AndSendsNothing()
        {
            var seed = await SeedAsync();
            var voided = await AddLedgerAsync(seed.HoaDonId, 300_000m, NewKey("SEPAY-HUY"), DateTime.UtcNow.AddHours(-3), isDeleted: true);
            var (admin, token) = await LoginAsync(NewClient(), seed.Admin, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await admin.SendAsync(Resend(voided, token));

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Equal("Khoản thanh toán đã hủy ghi nhận, không gửi biên lai.", (await JsonAsync(res)).GetProperty("message").GetString());
            Assert.Empty(ReceiptsOf(voided));
        }

        [Fact]
        public async Task Resend_AnUnknownRow_Returns404()
        {
            var seed = await SeedAsync();
            var (admin, token) = await LoginAsync(NewClient(), seed.Admin, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await admin.SendAsync(Resend(int.MaxValue, token));

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task Resend_WithoutAnAntiforgeryToken_Returns400_AndSendsNothing()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var (staff, _) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await staff.SendAsync(Resend(row, token: null));

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Empty(ReceiptsOf(row));
        }

        [Fact]
        public async Task Resend_TenantAndAnonymous_AreRedirectedToLogin()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var (tenant, token) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            foreach (var res in new[] { await tenant.SendAsync(Resend(row, token)), await NewClient().SendAsync(Resend(row, null)) })
            {
                Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
                Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
            }

            Assert.Empty(ReceiptsOf(row));
        }

        [Fact]
        public async Task Resend_WhenTheEmailServiceFails_Returns400WithAFriendlyMessage()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var (staff, token) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");
            var results = _factory.FakeEmailServiceInstance.ResultByAddress;
            results[seed.TenantEmail] = (false, "SMTP lỗi");
            try
            {
                var res = await staff.SendAsync(Resend(row, token));

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.Equal("Không gửi được email biên lai. Vui lòng thử lại sau.", (await JsonAsync(res)).GetProperty("message").GetString());
            }
            finally
            {
                results.TryRemove(seed.TenantEmail, out _);
            }
        }

        // ---------- Chi tiết hóa đơn: nhãn SePay và mã tham chiếu ----------

        [Fact]
        public async Task InvoiceDetail_MarksSePayRowsAndExposesTheBankReference_ButNotForCashOrTheIdKey()
        {
            var seed = await SeedAsync();
            var reference = "FT" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            var bank = await AddLedgerAsync(seed.HoaDonId, 500_000m, $"SEPAY-{reference}", DateTime.UtcNow.AddHours(-3));
            var byId = await AddLedgerAsync(seed.HoaDonId, 400_000m, $"SEPAY-ID-{Random.Shared.Next(1000, 999999)}", DateTime.UtcNow.AddHours(-2));
            var cash = await AddLedgerAsync(seed.HoaDonId, 300_000m, NewKey("TM"), DateTime.UtcNow.AddHours(-1), phuongThuc: PhuongThucThanhToan.TienMat);
            var voided = await AddLedgerAsync(seed.HoaDonId, 100_000m, $"SEPAY-{reference}X", DateTime.UtcNow.AddHours(-4), isDeleted: true);
            var (staff, _) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await staff.GetAsync($"/HoaDon/GetById/{seed.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var rows = (await JsonAsync(res)).GetProperty("lichSuThanhToans").EnumerateArray().ToList();
            Assert.Equal(3, rows.Count);
            JsonElement Row(int id) => rows.Single(r => r.GetProperty("lichSuThanhToanId").GetInt32() == id);

            Assert.True(Row(bank).GetProperty("laSePay").GetBoolean());
            Assert.Equal(reference, Row(bank).GetProperty("maThamChieu").GetString());
            Assert.True(Row(byId).GetProperty("laSePay").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Row(byId).GetProperty("maThamChieu").ValueKind);
            Assert.False(Row(cash).GetProperty("laSePay").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Row(cash).GetProperty("maThamChieu").ValueKind);
            Assert.DoesNotContain(rows, r => r.GetProperty("lichSuThanhToanId").GetInt32() == voided);
        }

        // ---------- Trang lỗi biên lai thân thiện ----------

        // Trang lỗi là HTML (không phải JSON), đúng mã HTTP theo loại lỗi, có nút quay lại; trả nội dung đã giải mã để so chữ tiếng Việt.
        private static async Task<string> AssertFriendlyPageAsync(HttpResponseMessage res, HttpStatusCode status, string backUrl)
        {
            Assert.Equal(status, res.StatusCode);
            Assert.Equal("text/html", res.Content.Headers.ContentType?.MediaType);
            var text = await HtmlTextAsync(res);
            Assert.Contains($"href=\"{backUrl}\"", text);
            Assert.DoesNotContain("\"success\"", text);
            return text;
        }

        [Fact]
        public async Task TenantReceipt_OfAnotherTenant_Or_Id0_Is404_AsAFriendlyHtmlPage()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var (other, _) = await LoginAsync(NewClient(), seed.OtherTenant, "/KhachThue/HoaDon");

            foreach (var url in new[] { $"/KhachThue/HoaDon/BienLai/{row}", "/KhachThue/HoaDon/BienLai/0" })
            {
                var text = await AssertFriendlyPageAsync(await other.GetAsync(url), HttpStatusCode.NotFound, "/KhachThue/HoaDon");

                Assert.Contains("Không tìm thấy biên lai", text);
                Assert.Contains("Về danh sách hóa đơn", text);
            }
        }

        [Fact]
        public async Task TenantReceipt_OfAVoidedRow_Is404_AsAFriendlyHtmlPage()
        {
            var seed = await SeedAsync();
            var voided = await AddLedgerAsync(seed.HoaDonId, 300_000m, NewKey("SEPAY-HUY"), DateTime.UtcNow.AddHours(-3), isDeleted: true);
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var text = await AssertFriendlyPageAsync(await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{voided}"), HttpStatusCode.NotFound, "/KhachThue/HoaDon");

            Assert.Contains("Không tìm thấy biên lai", text);
        }

        [Fact]
        public async Task ManagerReceipt_OfAnotherBranch_Is403_AsAFriendlyHtmlPage_AndAVoidedRowIs404()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("SEPAY-FT"), DateTime.UtcNow.AddHours(-2));
            var voided = await AddLedgerAsync(seed.HoaDonId, 300_000m, NewKey("SEPAY-HUY"), DateTime.UtcNow.AddHours(-3), isDeleted: true);

            var (staffB, _) = await LoginAsync(NewClient(), seed.StaffB, "/QuanLyNhaTro/DoiChieuThanhToan");
            var deniedText = await AssertFriendlyPageAsync(
                await staffB.GetAsync($"/QuanLyNhaTro/HoaDon/BienLai/{row}"), HttpStatusCode.Forbidden, "/QuanLyNhaTro/QuanLyHoaDon");
            Assert.Contains("Bạn không có quyền xem biên lai này", deniedText);
            Assert.Contains("Biên lai này thuộc chi nhánh bạn chưa được phân công", deniedText);

            var (staffA, _) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");
            var goneText = await AssertFriendlyPageAsync(
                await staffA.GetAsync($"/QuanLyNhaTro/HoaDon/BienLai/{voided}"), HttpStatusCode.NotFound, "/QuanLyNhaTro/QuanLyHoaDon");
            Assert.Contains("Không tìm thấy biên lai", goneText);
        }

        // ---------- QR của quản lý (Q1): mã TTPTHD{Id} và số còn lại ----------

        private sealed class RecordingVietQr : IVietQRService
        {
            public List<(decimal Amount, string Memo)> Calls { get; } = new();

            public string GenerateVietQRString(string bankIdOrBin, string accountNumber, decimal amount, string memo)
            {
                Calls.Add((amount, memo));
                return "QR";
            }

            public byte[] GenerateQRCodePNGBytes(string payload) => new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        }

        [Fact]
        public async Task ManagerQr_UsesTheTransferCode_AndTheRemainingAmount()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 500_000m, NewKey("TM"), DateTime.UtcNow.AddHours(-2), phuongThuc: PhuongThucThanhToan.TienMat);
            var qr = new RecordingVietQr();
            var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IVietQRService>();
                s.AddSingleton<IVietQRService>(qr);
            }));
            var (staff, _) = await LoginAsync(factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }),
                seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await staff.GetAsync($"/HoaDon/GetVietQR?hoaDonId={seed.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
            var call = Assert.Single(qr.Calls);
            Assert.Equal($"TTPTHD{seed.HoaDonId}", call.Memo);
            Assert.Equal(1_500_000m, call.Amount);
        }

        [Fact]
        public async Task ManagerQr_OfAFullyPaidInvoice_IsRefused()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 2_000_000m, NewKey("TM"), DateTime.UtcNow.AddHours(-2), phuongThuc: PhuongThucThanhToan.TienMat);
            var (staff, _) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");

            var res = await staff.GetAsync($"/HoaDon/GetVietQR?hoaDonId={seed.HoaDonId}");

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
    }
}
