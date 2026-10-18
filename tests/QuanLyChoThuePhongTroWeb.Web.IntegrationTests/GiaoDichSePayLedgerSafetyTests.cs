using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
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
    // Bổ sung cho GiaoDichSePayTests: hậu quả của cờ SePay:SyncRecordEnabled lên thông báo trong app và trạng thái hóa đơn, và các màn
    // quản lý hiển thị đúng mã chuyển khoản TTPTHD{Id} (Q1). Client SePay là fake, không có request mạng.
    [Collection(WebTestCollection.Name)]
    public class GiaoDichSePayLedgerSafetyTests
    {
        private const string Password = "Password123!";
        private const string SyncUrl = "/QuanLyNhaTro/GiaoDichSePay/DongBo";

        private readonly CustomWebApplicationFactory _factory;

        public GiaoDichSePayLedgerSafetyTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed class FakeSePayClient : ISePayTransactionClient
        {
            public ConcurrentQueue<ServiceResult<SePayTransactionPage>> Results { get; } = new();
            public int Calls;

            public Task<ServiceResult<SePayTransactionPage>> GetIncomingPageAsync(DateTime fromUtc, DateTime toUtc, int page, CancellationToken ct = default)
            {
                Interlocked.Increment(ref Calls);
                return Task.FromResult(Results.TryDequeue(out var next)
                    ? next
                    : ServiceResult<SePayTransactionPage>.Ok(new SePayTransactionPage(Array.Empty<SePayApiTransaction>(), false, 0)));
            }

            public void Page(params SePayApiTransaction[] items)
                => Results.Enqueue(ServiceResult<SePayTransactionPage>.Ok(new SePayTransactionPage(items, false, 0)));
        }

        private sealed record Seed(int HoaDonId, string MaHoaDon, string Admin);

        private async Task<Seed> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_LS_{s}", MaChiNhanh = TestCodes.MaChiNhanh("L"), DiaChi = "1 Duong LS", SoDienThoai = "0900000008", MoTa = "LS" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000008", CCCD = $"079{s[..6]}8", Email = $"ls_{s}@test.com" };
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
                MaHoaDon = $"HD-LS-{s}",
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

            var admin = $"als_{s}";
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = admin, MatKhau = Password, Role = AppRole.Admin })).Success);
            return new Seed(invoice.HoaDonId, invoice.MaHoaDon, admin);
        }

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

        private static HttpClient NewClient(WebApplicationFactory<Program> factory)
            => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        private async Task WithOptionsAsync(bool syncRecordEnabled, Func<Task> test)
        {
            var options = _factory.SePayOptionsInstance;
            var (oldToken, oldRecord) = (options.ApiToken, options.SyncRecordEnabled);
            options.ApiToken = "token-test";
            options.SyncRecordEnabled = syncRecordEnabled;
            try
            {
                await test();
            }
            finally
            {
                options.ApiToken = oldToken;
                options.SyncRecordEnabled = oldRecord;
            }
        }

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static HttpRequestMessage Sync(string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, SyncUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { tuNgay = "2026-10-15", denNgay = "2026-10-17" }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("RequestVerificationToken", token);
            return request;
        }

        private async Task<(int Notifications, int Ledger, TrangThaiHoaDon Status)> SnapshotAsync(Seed seed)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notifications = await db.ThongBaos.AsNoTracking().CountAsync(t => t.NoiDung.Contains(seed.MaHoaDon));
            var ledger = await db.LichSuThanhToans.AsNoTracking().CountAsync(l => l.HoaDonId == seed.HoaDonId);
            var status = (await db.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seed.HoaDonId)).TrangThaiHoaDon;
            return (notifications, ledger, status);
        }

        private static SePayApiTransaction Tx(int hoaDonId, string reference)
            => new(Guid.NewGuid().ToString(), new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc), CustomWebApplicationFactory.TestBankAccount, null, "in",
                2_000_000m, $"TTPTHD{hoaDonId}", reference, null, "MBBank");

        [Fact]
        public async Task CheckOnlyMode_CreatesNoInAppNotification_NoLedgerRow_AndLeavesTheInvoiceUnpaid_WhileRecordModeDoesAllThree()
        {
            var seed = await SeedAsync();
            var before = await SnapshotAsync(seed);
            Assert.Equal((0, 0, TrangThaiHoaDon.ChuaThanhToan), before);

            await WithOptionsAsync(syncRecordEnabled: false, async () =>
            {
                var fake = new FakeSePayClient();
                fake.Page(Tx(seed.HoaDonId, NewReference()));
                var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
                {
                    s.RemoveAll<ISePayTransactionClient>();
                    s.AddSingleton<ISePayTransactionClient>(fake);
                }));
                var (admin, token) = await LoginAsync(NewClient(factory), seed.Admin, "/QuanLyNhaTro/GiaoDichSePay");

                var res = await admin.SendAsync(Sync(token));

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                Assert.Equal(1, fake.Calls);
                Assert.Equal(before, await SnapshotAsync(seed));
            });

            await WithOptionsAsync(syncRecordEnabled: true, async () =>
            {
                var fake = new FakeSePayClient();
                fake.Page(Tx(seed.HoaDonId, NewReference()));
                var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
                {
                    s.RemoveAll<ISePayTransactionClient>();
                    s.AddSingleton<ISePayTransactionClient>(fake);
                }));
                var (admin, token) = await LoginAsync(NewClient(factory), seed.Admin, "/QuanLyNhaTro/GiaoDichSePay");

                var res = await admin.SendAsync(Sync(token));

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var after = await SnapshotAsync(seed);
                Assert.True(after.Notifications > 0, "Ghi sổ qua đồng bộ bù phải tạo thông báo trong app.");
                Assert.Equal(1, after.Ledger);
                Assert.Equal(TrangThaiHoaDon.DaThanhToan, after.Status);
            });
        }

        // ---------- Q1: mọi màn hiển thị QR của quản lý đều chỉ nội dung TTPTHD{Id} ----------

        [Theory]
        [InlineData("/QuanLyNhaTro/QuanLyHoaDon")]
        [InlineData("/QuanLyNhaTro/LichSuThanhToan")]
        [InlineData("/PhongTros/QuanLyPhongTro")]
        public async Task ManagerScreensThatShowTheQr_TellTheTenantToUseTheTransferCode_NotTheOldInvoiceCodeText(string page)
        {
            var seed = await SeedAsync();
            var (admin, _) = await LoginAsync(NewClient(_factory), seed.Admin, "/QuanLyNhaTro/GiaoDichSePay");

            var res = await admin.GetAsync(page);

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
            Assert.Contains("TTPTHD", html);
            Assert.DoesNotContain("THANH TOAN", html);
        }

        [Fact]
        public async Task TheInvoiceScreen_OffersResendingAReceiptAndLabelsSePayRows()
        {
            var seed = await SeedAsync();
            var (admin, _) = await LoginAsync(NewClient(_factory), seed.Admin, "/QuanLyNhaTro/GiaoDichSePay");

            var page = WebUtility.HtmlDecode(await (await admin.GetAsync("/QuanLyNhaTro/QuanLyHoaDon")).Content.ReadAsStringAsync());
            var script = await (await admin.GetAsync("/js/hoadon-thanhtoan-quanly.js")).Content.ReadAsStringAsync();

            Assert.Contains("hd-resend-receipt", page);
            Assert.Contains("Gửi lại biên lai", page);
            Assert.Contains("/QuanLyNhaTro/HoaDon/BienLai/", page);
            Assert.Contains("badge bg-green-lt me-1\">SePay", page);
            Assert.Contains("RequestVerificationToken", script);
            Assert.Contains("/GuiLai", script);
            Assert.Contains("resendReceipt", script);
        }
    }
}
