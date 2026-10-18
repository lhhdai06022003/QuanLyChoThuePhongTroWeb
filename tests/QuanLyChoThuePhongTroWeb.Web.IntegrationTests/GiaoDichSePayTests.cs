using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
    // Màn Giao dịch SePay (đồng bộ bù) của thanh toán trực tuyến đợt 1 phần 4 (spec §4.3, §7.4): chỉ Admin, khóa nút khi chưa có API
    // token, chế độ chỉ đối chiếu / ghi sổ, chống trùng, kiểm khoảng ngày. Client API SePay là fake nên không có request mạng nào.
    [Collection(WebTestCollection.Name)]
    public class GiaoDichSePayTests
    {
        private const string Password = "Password123!";
        private const string PageUrl = "/QuanLyNhaTro/GiaoDichSePay";
        private const string SyncUrl = "/QuanLyNhaTro/GiaoDichSePay/DongBo";

        private readonly CustomWebApplicationFactory _factory;

        public GiaoDichSePayTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed class FakeSePayClient : ISePayTransactionClient
        {
            private int _calls;

            public ConcurrentQueue<ServiceResult<SePayTransactionPage>> Results { get; } = new();
            public ConcurrentQueue<(DateTime FromUtc, DateTime ToUtc, int Page)> Requests { get; } = new();
            public int CallCount => Volatile.Read(ref _calls);

            public Task<ServiceResult<SePayTransactionPage>> GetIncomingPageAsync(DateTime fromUtc, DateTime toUtc, int page, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _calls);
                Requests.Enqueue((fromUtc, toUtc, page));
                return Task.FromResult(Results.TryDequeue(out var next)
                    ? next
                    : ServiceResult<SePayTransactionPage>.Ok(new SePayTransactionPage(Array.Empty<SePayApiTransaction>(), false, 0)));
            }

            public void Page(bool hasMore, params SePayApiTransaction[] items)
                => Results.Enqueue(ServiceResult<SePayTransactionPage>.Ok(new SePayTransactionPage(items, hasMore, 0)));
        }

        private sealed record Seed(int HoaDonId, string MaHoaDon, string Staff, string Tenant, string Admin);

        private WebApplicationFactory<Program> WithClient(FakeSePayClient fake)
            => _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<ISePayTransactionClient>();
                s.AddSingleton<ISePayTransactionClient>(fake);
            }));

        private static HttpClient NewClient(WebApplicationFactory<Program> factory)
            => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static SePayApiTransaction Tx(
            int hoaDonId, string? reference, decimal amount = 2_000_000m, string? content = null, string type = "in",
            string account = CustomWebApplicationFactory.TestBankAccount, string? apiId = null)
            => new(apiId ?? Guid.NewGuid().ToString(), new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc), account, null, type, amount,
                content ?? $"TTPTHD{hoaDonId}", reference, null, "MBBank");

        private async Task<Seed> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_GD_{s}", MaChiNhanh = TestCodes.MaChiNhanh("G"), DiaChi = "1 Duong GD", SoDienThoai = "0900000005", MoTa = "GD" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000005", CCCD = $"079{s[..6]}5", Email = $"gd_{s}@test.com" };
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
                MaHoaDon = $"HD-GD-{s}",
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

            var seed = new Seed(invoice.HoaDonId, invoice.MaHoaDon, $"sgd_{s}", $"tgd_{s}", $"agd_{s}");
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Tenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = tenant.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Staff, MatKhau = Password, Role = AppRole.NhanVien })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Admin, MatKhau = Password, Role = AppRole.Admin })).Success);

            var staff = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == seed.Staff);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staff.NguoiDungId, NgayPhanCong = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return seed;
        }

        private static string Token(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success) match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // Đăng nhập rồi lấy token antiforgery của phiên từ một trang đã xác thực; trả cả nội dung trang.
        private static async Task<(HttpClient Client, string Token, string Html)> LoginAsync(HttpClient client, string username, string page)
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
            var html = await authed.Content.ReadAsStringAsync();
            return (client, Token(html), html);
        }

        private static HttpRequestMessage Sync(string? tuNgay, string? denNgay, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, SyncUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { tuNgay, denNgay }), Encoding.UTF8, "application/json")
            };
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

        // Cấu hình dùng chung cả bộ test: đặt trong try/finally rồi trả lại giá trị cũ.
        private async Task WithOptionsAsync(string apiToken, bool syncRecordEnabled, Func<Task> test)
        {
            var options = _factory.SePayOptionsInstance;
            var (oldToken, oldRecord) = (options.ApiToken, options.SyncRecordEnabled);
            options.ApiToken = apiToken;
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

        private async Task<List<LichSuThanhToan>> LedgerAsync(int hoaDonId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.LichSuThanhToans.AsNoTracking().Where(l => l.HoaDonId == hoaDonId).ToListAsync();
        }

        private static JsonElement RowFor(JsonElement data, string? reference)
            => data.GetProperty("rows").EnumerateArray().Single(r =>
                reference == null
                    ? r.GetProperty("maThamChieu").ValueKind == JsonValueKind.Null
                    : r.GetProperty("maThamChieu").GetString() == reference);

        // ---------- Phân quyền ----------

        [Fact]
        public async Task Staff_GetAndPost_AreRedirectedToLogin_AndTheClientIsNeverCalled()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var factory = WithClient(fake);
                var (staff, token, _) = await LoginAsync(NewClient(factory), seed.Staff, "/QuanLyNhaTro/DoiChieuThanhToan");

                var get = await staff.GetAsync(PageUrl);
                var post = await staff.SendAsync(Sync("2026-10-15", "2026-10-17", token));

                foreach (var res in new[] { get, post })
                {
                    Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
                    Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
                }

                Assert.Equal(0, fake.CallCount);
            });
        }

        [Fact]
        public async Task Tenant_GetAndPost_AreRedirectedToLogin_AndTheClientIsNeverCalled()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var factory = WithClient(fake);
                var (tenant, token, _) = await LoginAsync(NewClient(factory), seed.Tenant, "/KhachThue/HoaDon");

                var get = await tenant.GetAsync(PageUrl);
                var post = await tenant.SendAsync(Sync("2026-10-15", "2026-10-17", token));

                foreach (var res in new[] { get, post })
                {
                    Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
                    Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
                }

                Assert.Equal(0, fake.CallCount);
            });
        }

        [Fact]
        public async Task Anonymous_GetAndPost_AreRedirectedToLogin()
        {
            var fake = new FakeSePayClient();
            var client = NewClient(WithClient(fake));

            foreach (var res in new[] { await client.GetAsync(PageUrl), await client.SendAsync(Sync("2026-10-15", "2026-10-17", null)) })
            {
                Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
                Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
            }

            Assert.Equal(0, fake.CallCount);
        }

        // ---------- Trang ----------

        [Fact]
        public async Task Admin_Get_WithoutAnApiToken_ShowsTheHint_AndDisablesTheButton()
        {
            await WithOptionsAsync(string.Empty, false, async () =>
            {
                var seed = await SeedAsync();
                var (_, _, html) = await LoginAsync(NewClient(_factory), seed.Admin, PageUrl);

                Assert.Contains("SePay:ApiToken", html);
                Assert.Matches(@"<button[^>]*id=""btnDongBoSePay""[^>]*\sdisabled", html);
                Assert.Contains("Giao dịch SePay", WebUtility.HtmlDecode(html));
            });
        }

        [Fact]
        public async Task Admin_Get_WithAnApiToken_EnablesTheButton_AndFillsTheDefaultRange()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var (_, _, html) = await LoginAsync(NewClient(_factory), seed.Admin, PageUrl);

                Assert.DoesNotMatch(@"<button[^>]*id=""btnDongBoSePay""[^>]*\sdisabled", html);
                Assert.Contains("data-max-days=\"31\"", html);

                var today = DateTime.UtcNow.AddHours(7);
                var toText = Regex.Match(html, @"id=""sepayDenNgay""[^>]*value=""(\d{4}-\d{2}-\d{2})""").Groups[1].Value;
                var fromText = Regex.Match(html, @"id=""sepayTuNgay""[^>]*value=""(\d{4}-\d{2}-\d{2})""").Groups[1].Value;
                var to = DateTime.ParseExact(toText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var from = DateTime.ParseExact(fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                // Đến ngày là hôm nay theo giờ Việt Nam (cho phép lệch một ngày nếu test chạy sát nửa đêm); từ ngày sớm hơn đúng 2 ngày.
                Assert.InRange(to, today.Date.AddDays(-1), today.Date.AddDays(1));
                Assert.Equal(2, (to - from).Days);
            });
        }

        [Fact]
        public async Task Admin_Get_InCheckOnlyMode_ShowsTheBanner_ButNotWhenRecordingIsEnabled()
        {
            var seed = await SeedAsync();
            string checkOnly = string.Empty, recording = string.Empty;

            await WithOptionsAsync("token-test", false, async () =>
                checkOnly = (await LoginAsync(NewClient(_factory), seed.Admin, PageUrl)).Html);
            await WithOptionsAsync("token-test", true, async () =>
                recording = (await LoginAsync(NewClient(_factory), seed.Admin, PageUrl)).Html);

            Assert.Contains("SePay:SyncRecordEnabled", checkOnly);
            Assert.DoesNotContain("SePay:SyncRecordEnabled", recording);
        }

        [Fact]
        public async Task TheSidebar_ListsTheScreenForAdmin_ButNotForStaff()
        {
            var seed = await SeedAsync();

            var (_, _, adminHtml) = await LoginAsync(NewClient(_factory), seed.Admin, "/QuanLyNhaTro/DoiChieuThanhToan");
            var (_, _, staffHtml) = await LoginAsync(NewClient(_factory), seed.Staff, "/QuanLyNhaTro/DoiChieuThanhToan");

            Assert.Contains($"href=\"{PageUrl}\"", adminHtml);
            Assert.DoesNotContain(PageUrl, staffHtml);
        }

        // ---------- Đồng bộ ----------

        [Fact]
        public async Task Admin_Post_WithoutAnApiToken_Returns400_AndTheClientIsNotCalled()
        {
            await WithOptionsAsync(string.Empty, true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var res = await admin.SendAsync(Sync("2026-10-15", "2026-10-17", token));

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.Equal("Chưa cấu hình SePay:ApiToken nên chưa đồng bộ được.", (await JsonAsync(res)).GetProperty("message").GetString());
                Assert.Equal(0, fake.CallCount);
            });
        }

        [Fact]
        public async Task Admin_Post_RecordsNewTransactionsOnce_AndSecondRunReportsThemAsExisting()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var reference = NewReference();
                var apiId = Guid.NewGuid().ToString();
                var fake = new FakeSePayClient();
                fake.Page(true, Tx(seed.HoaDonId, reference, apiId: apiId));
                fake.Page(false, Tx(seed.HoaDonId, null));
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var first = await admin.SendAsync(Sync("2026-10-15", "2026-10-17", token));

                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
                var body = await first.Content.ReadAsStringAsync();
                Assert.DoesNotContain(apiId, body);
                Assert.DoesNotContain("token-test", body, StringComparison.OrdinalIgnoreCase);
                using (var doc = JsonDocument.Parse(body))
                {
                    var data = doc.RootElement.GetProperty("data");
                    Assert.True(data.GetProperty("hoanTat").GetBoolean());
                    Assert.False(data.GetProperty("chiDoiChieu").GetBoolean());
                    Assert.Equal(2, data.GetProperty("rows").GetArrayLength());
                    var recorded = RowFor(data, reference);
                    Assert.Equal("MoiGhiNhan", recorded.GetProperty("trangThai").GetString());
                    Assert.Equal("Mới ghi nhận", recorded.GetProperty("nhanTrangThai").GetString());
                    Assert.Equal(seed.HoaDonId, recorded.GetProperty("hoaDonId").GetInt32());
                    Assert.Equal(seed.MaHoaDon, recorded.GetProperty("maHoaDon").GetString());
                    Assert.Equal($"TTPTHD{seed.HoaDonId}", recorded.GetProperty("maChuyenKhoan").GetString());
                    Assert.Equal(2_000_000m, recorded.GetProperty("soTien").GetDecimal());
                    // 03:00 UTC = 10:00 giờ Việt Nam.
                    Assert.Equal("17/10/2026 10:00:00", recorded.GetProperty("thoiGian").GetString());
                    var missing = RowFor(data, null);
                    Assert.Equal("ThieuMaThamChieu", missing.GetProperty("trangThai").GetString());
                    Assert.Equal("Thiếu mã tham chiếu", missing.GetProperty("nhanTrangThai").GetString());
                    var totals = data.GetProperty("tongTheoTrangThai").EnumerateArray().ToDictionary(t => t.GetProperty("trangThai").GetString()!, t => t.GetProperty("soLuong").GetInt32());
                    Assert.Equal(8, totals.Count);
                    Assert.Equal(1, totals["MoiGhiNhan"]);
                    Assert.Equal(1, totals["ThieuMaThamChieu"]);
                    Assert.Equal(0, totals["DaCo"]);
                }

                // Đúng một dòng sổ, đúng khóa chống trùng của webhook, và không có dòng nào cho giao dịch thiếu mã.
                var ledger = Assert.Single(await LedgerAsync(seed.HoaDonId));
                Assert.Equal($"SEPAY-{reference}", ledger.MaGiaoDich);
                Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
                Assert.Equal(2, fake.CallCount);

                // Lần hai: cùng giao dịch trả về từ API.
                fake.Page(false, Tx(seed.HoaDonId, reference));
                var second = await admin.SendAsync(Sync("2026-10-15", "2026-10-17", token));
                var secondData = (await JsonAsync(second)).GetProperty("data");
                Assert.Equal("DaCo", RowFor(secondData, reference).GetProperty("trangThai").GetString());
                Assert.Single(await LedgerAsync(seed.HoaDonId));
            });
        }

        [Fact]
        public async Task Admin_Post_SendsTheFullNotificationsForARecordedRow_ButNoTelegramAlertForTheRest()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var recordedRef = NewReference();
                var unmatchedRef = NewReference();
                var otherAccountRef = NewReference();
                var fake = new FakeSePayClient();
                fake.Page(false,
                    Tx(seed.HoaDonId, unmatchedRef, content: "chuyen tien tro khong ma"),
                    Tx(seed.HoaDonId, otherAccountRef, account: "9999999999"),
                    Tx(seed.HoaDonId, recordedRef));
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var res = await admin.SendAsync(Sync("2026-10-15", "2026-10-17", token));

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var data = (await JsonAsync(res)).GetProperty("data");
                Assert.Equal("KhongKhopMa", RowFor(data, unmatchedRef).GetProperty("trangThai").GetString());
                var other = RowFor(data, otherAccountRef);
                Assert.Equal("BoQua", other.GetProperty("trangThai").GetString());
                Assert.Equal("Tiền vào tài khoản khác", other.GetProperty("ghiChu").GetString());
                Assert.Equal("MoiGhiNhan", RowFor(data, recordedRef).GetProperty("trangThai").GetString());

                var telegram = _factory.FakeTelegramInstance.Messages.ToList();
                Assert.DoesNotContain(telegram, m => m.Text.Contains(unmatchedRef));
                Assert.DoesNotContain(telegram, m => m.Text.Contains(otherAccountRef));
                Assert.Contains(telegram, m => m.Text.Contains(recordedRef) && m.Text.Contains("THU TIỀN TRỌ THÀNH CÔNG"));
                Assert.Single(_factory.FakeEmailServiceInstance.PaymentReceiptCalls.Where(c => c.Data.HoaDonId == seed.HoaDonId));
            });
        }

        [Fact]
        public async Task Admin_Post_InCheckOnlyMode_NeverWrites_AndReportsWhatTheWebhookAlreadyRecorded()
        {
            await WithOptionsAsync("token-test", false, async () =>
            {
                var seed = await SeedAsync();
                var newRef = NewReference();
                var knownRef = NewReference();
                var fake = new FakeSePayClient();
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                // Webhook đã ghi knownRef trước đó.
                using (var scope = _factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.LichSuThanhToans.Add(new LichSuThanhToan
                    {
                        HoaDonId = seed.HoaDonId,
                        MaGiaoDich = $"SEPAY-{knownRef}",
                        SoTienThanhToan = 500_000m,
                        PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
                        NgayThanhToan = DateTime.UtcNow.AddHours(-1),
                        NgayXacNhan = DateTime.UtcNow.AddHours(-1)
                    });
                    await db.SaveChangesAsync();
                }

                fake.Page(false, Tx(seed.HoaDonId, newRef), Tx(seed.HoaDonId, knownRef.ToLowerInvariant(), amount: 500_000m));
                var res = await admin.SendAsync(Sync("2026-10-15", "2026-10-17", token));

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var data = (await JsonAsync(res)).GetProperty("data");
                Assert.True(data.GetProperty("chiDoiChieu").GetBoolean());
                Assert.Equal("ChuaGhiSo", RowFor(data, newRef).GetProperty("trangThai").GetString());
                Assert.Equal("Chưa ghi sổ", RowFor(data, newRef).GetProperty("nhanTrangThai").GetString());
                Assert.Equal("DaCo", RowFor(data, knownRef.ToLowerInvariant()).GetProperty("trangThai").GetString());
                var ledger = Assert.Single(await LedgerAsync(seed.HoaDonId));
                Assert.Equal($"SEPAY-{knownRef}", ledger.MaGiaoDich);
                Assert.DoesNotContain(_factory.FakeTelegramInstance.Messages, m => m.Text.Contains(newRef));
                Assert.Empty(_factory.FakeEmailServiceInstance.PaymentReceiptCalls.Where(c => c.Data.HoaDonId == seed.HoaDonId));
            });
        }

        [Fact]
        public async Task Admin_Post_WhenALaterPageFails_ReturnsTheRowsSoFar_AndTheReason()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var reference = NewReference();
                var fake = new FakeSePayClient();
                fake.Page(true, Tx(seed.HoaDonId, reference));
                fake.Results.Enqueue(ServiceResult<SePayTransactionPage>.Fail("SePay trả mã 500."));
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var res = await admin.SendAsync(Sync("2026-10-15", "2026-10-17", token));

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var data = (await JsonAsync(res)).GetProperty("data");
                Assert.False(data.GetProperty("hoanTat").GetBoolean());
                Assert.Equal("SePay trả mã 500.", data.GetProperty("loiDongBo").GetString());
                Assert.Equal(1, data.GetProperty("rows").GetArrayLength());
            });
        }

        [Fact]
        public async Task Admin_Post_ConvertsTheVietnameseDatesToAUtcRangeThatCoversBothEndDays()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var res = await admin.SendAsync(Sync("2026-10-01", "2026-10-03", token));

                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var call = Assert.Single(fake.Requests);
                // 00:00:00 ngày 01/10 giờ Việt Nam = 17:00:00 ngày 30/09 UTC; 23:59:59 ngày 03/10 giờ Việt Nam = 16:59:59 ngày 03/10 UTC.
                Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), call.FromUtc);
                Assert.Equal(new DateTime(2026, 10, 3, 16, 59, 59, DateTimeKind.Utc), call.ToUtc);
                Assert.Equal(1, call.Page);
                Assert.True((await JsonAsync(res)).GetProperty("data").GetProperty("hoanTat").GetBoolean());
            });
        }

        // ---------- Đầu vào sai ----------

        [Fact]
        public async Task Admin_Post_RangeLongerThan31Days_Returns400_AndThe31DayRangeIsAccepted()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var tooLong = await admin.SendAsync(Sync("2026-09-01", "2026-10-02", token));
                var allowed = await admin.SendAsync(Sync("2026-09-01", "2026-10-01", token));

                Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
                Assert.Equal("Khoảng ngày tối đa 31 ngày.", (await JsonAsync(tooLong)).GetProperty("message").GetString());
                Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
                Assert.Equal(1, fake.CallCount);
            });
        }

        [Theory]
        [InlineData("khong-phai-ngay", "2026-10-17")]
        [InlineData("2026-10-15", "")]
        [InlineData(null, null)]
        [InlineData("17/10/2026", "2026-10-17")]
        public async Task Admin_Post_InvalidDates_Return400(string? tuNgay, string? denNgay)
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var res = await admin.SendAsync(Sync(tuNgay, denNgay, token));

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.Equal("Vui lòng chọn ngày hợp lệ.", (await JsonAsync(res)).GetProperty("message").GetString());
                Assert.Equal(0, fake.CallCount);
            });
        }

        [Fact]
        public async Task Admin_Post_ReversedDates_Return400()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var res = await admin.SendAsync(Sync("2026-10-17", "2026-10-15", token));

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.Equal("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.", (await JsonAsync(res)).GetProperty("message").GetString());
                Assert.Equal(0, fake.CallCount);
            });
        }

        [Fact]
        public async Task Admin_Post_WithoutAnAntiforgeryToken_Returns400_AndTheClientIsNotCalled()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var (admin, _, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);

                var res = await admin.SendAsync(Sync("2026-10-15", "2026-10-17", token: null));

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.Equal(0, fake.CallCount);
            });
        }

        [Fact]
        public async Task Admin_Post_WithAnEmptyBody_Returns400()
        {
            await WithOptionsAsync("token-test", true, async () =>
            {
                var seed = await SeedAsync();
                var fake = new FakeSePayClient();
                var (admin, token, _) = await LoginAsync(NewClient(WithClient(fake)), seed.Admin, PageUrl);
                var request = new HttpRequestMessage(HttpMethod.Post, SyncUrl) { Content = new StringContent("", Encoding.UTF8, "application/json") };
                request.Headers.Add("RequestVerificationToken", token);

                var res = await admin.SendAsync(request);

                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
                Assert.Equal(0, fake.CallCount);
            });
        }
    }
}
