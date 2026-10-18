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
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
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
    // Kiểm bổ sung (tester) cho cổng khách thuê của thanh toán trực tuyến, đợt 1 phần 3: phân quyền với thành viên hợp đồng,
    // trạng thái hóa đơn chưa gửi, ảnh QR đúng số còn lại, dữ liệu do người dùng nhập đi vào tên file/HTML, id không hợp lệ,
    // và kiểm tĩnh JS (mọi chuỗi từ server đều qua esc()).
    [Collection(WebTestCollection.Name)]
    public class TenantOnlinePaymentSecurityTests
    {
        private const string Password = "Password123!";

        private readonly CustomWebApplicationFactory _factory;

        public TenantOnlinePaymentSecurityTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seed(int HoaDonId, string Room, string Tenant, string Member, string Other);

        private async Task<Seed> SeedAsync(
            string? soPhong = null,
            string? diaChi = null,
            decimal tongTien = 2_000_000m,
            TrangThaiPhatHanhHoaDon phatHanh = TrangThaiPhatHanhHoaDon.DaGui)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_S_{s}", MaChiNhanh = TestCodes.MaChiNhanh("S"), DiaChi = diaChi ?? $"1 Đường S {s}", SoDienThoai = "0900000003", MoTa = "S" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = soPhong ?? $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000011", CCCD = $"079{s[..6]}1", Email = $"k_{s}@test.com" };
            var member = new NguoiThue { HoVaTen = $"Thanh vien {s}", SoDienThoai = "0911000012", CCCD = $"079{s[..6]}2", Email = $"m_{s}@test.com" };
            var other = new NguoiThue { HoVaTen = $"Khac {s}", SoDienThoai = "0911000013", CCCD = $"079{s[..6]}3", Email = $"o_{s}@test.com" };
            db.PhongTros.Add(room);
            db.NguoiThues.AddRange(tenant, member, other);
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

            // Người ở cùng, được ghi trong hợp đồng nhưng không phải người đứng tên.
            db.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong { HopDongId = contract.HopDongId, NguoiThueId = member.NguoiThueId, NgayVao = DateTime.UtcNow.AddMonths(-1) });

            var invoice = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-SEC-{s}",
                Thang = 9,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiPhatHanh = phatHanh,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = phatHanh == TrangThaiPhatHanhHoaDon.DaGui ? DateTime.UtcNow.AddDays(-1) : null,
                HanThanhToan = DateTime.UtcNow.AddDays(5),
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền phòng", DonGia = tongTien, SoLuong = 1, TongTien = tongTien }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            var seed = new Seed(invoice.HoaDonId, room.SoPhong, $"tsec_{s}", $"msec_{s}", $"osec_{s}");
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Tenant, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = tenant.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Member, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = member.NguoiThueId })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.Other, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = other.NguoiThueId })).Success);
            return seed;
        }

        private async Task<int> AddLedgerAsync(int hoaDonId, decimal soTien, DateTime ngayUtc, bool isDeleted = false)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = new LichSuThanhToan
            {
                HoaDonId = hoaDonId,
                MaGiaoDich = $"GD-{Guid.NewGuid():N}"[..24],
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
                NgayThanhToan = ngayUtc,
                NgayXacNhan = ngayUtc,
                IsDeleted = isDeleted,
                GhiChu = string.Empty
            };
            db.LichSuThanhToans.Add(row);
            await db.SaveChangesAsync();
            return row.LichSuThanhToanId;
        }

        private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        private static string Token(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success) match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static async Task<HttpClient> LoginAsync(HttpClient client, string username)
        {
            var login = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", Token(await login.Content.ReadAsStringAsync())),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", Password)
            });
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/QuanLyNhaTro/DangNhap", content)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/KhachThue/HoaDon")).StatusCode);
            return client;
        }

        private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        // ---------- Phân quyền ----------

        [Fact]
        public async Task ContractMember_WhoIsNotTheNamedTenant_Gets404_OnPanelQrReceiptAndProofImage()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddHours(-3));
            var member = await LoginAsync(NewClient(), seed.Member);

            foreach (var url in new[]
            {
                $"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}",
                $"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}",
                $"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}?taiVe=true",
                $"/KhachThue/HoaDon/BienLai/{row}",
                "/KhachThue/HoaDon/ThanhToan/AnhMinhChung/1"
            })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(url)).StatusCode);
            }

            // Người đứng tên vẫn mở được, chứng tỏ 404 ở trên là do quyền chứ không do dữ liệu seed.
            var owner = await LoginAsync(NewClient(), seed.Tenant);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).StatusCode);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        public async Task InvoiceNotSentYet_PanelQrAndReceipt_AreHiddenFromTheOwner(TrangThaiPhatHanhHoaDon state)
        {
            var seed = await SeedAsync(phatHanh: state);
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddHours(-3));
            var owner = await LoginAsync(NewClient(), seed.Tenant);

            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).StatusCode);
        }

        [Theory]
        [InlineData("/KhachThue/HoaDon/ThanhToan/0")]
        [InlineData("/KhachThue/HoaDon/ThanhToan/-7")]
        [InlineData("/KhachThue/HoaDon/ThanhToan/2147483647")]
        [InlineData("/KhachThue/HoaDon/ThanhToan/QR/0")]
        [InlineData("/KhachThue/HoaDon/ThanhToan/QR/-7")]
        [InlineData("/KhachThue/HoaDon/ThanhToan/QR/2147483647")]
        [InlineData("/KhachThue/HoaDon/BienLai/0")]
        [InlineData("/KhachThue/HoaDon/BienLai/-7")]
        [InlineData("/KhachThue/HoaDon/ThanhToan/AnhMinhChung/0")]
        public async Task InvalidOrUnknownIds_Return404_NeverA500(string url)
        {
            var seed = await SeedAsync();
            var owner = await LoginAsync(NewClient(), seed.Tenant);

            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(url)).StatusCode);
        }

        [Fact]
        public async Task AnonymousUser_IsRedirectedToLogin_ForTheProofImageToo()
        {
            var res = await NewClient().GetAsync("/KhachThue/HoaDon/ThanhToan/AnhMinhChung/1");

            Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
        }

        // ---------- Tiền ----------

        [Fact]
        public async Task QrImage_IsGeneratedForTheRemainingAmount_AndChangesAfterAPartialPayment()
        {
            var seed = await SeedAsync();
            var owner = await LoginAsync(NewClient(), seed.Tenant);
            var qrUrl = $"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}";

            var full = await (await owner.GetAsync(qrUrl)).Content.ReadAsByteArrayAsync();
            await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddHours(-1));
            var afterPartial = await (await owner.GetAsync(qrUrl)).Content.ReadAsByteArrayAsync();
            // Tham số ?v= chỉ để phá cache, ảnh không phụ thuộc vào nó.
            var withCacheBuster = await (await owner.GetAsync(qrUrl + "?v=1500000")).Content.ReadAsByteArrayAsync();

            using var scope = _factory.Services.CreateScope();
            var vietQr = scope.ServiceProvider.GetRequiredService<IVietQRService>();
            var settings = scope.ServiceProvider.GetRequiredService<VietQrSettings>();
            byte[] Expected(decimal amount) => vietQr.GenerateQRCodePNGBytes(
                vietQr.GenerateVietQRString(settings.BankId, settings.AccountNumber, amount, $"TTPTHD{seed.HoaDonId}"));

            Assert.Equal(Expected(2_000_000m), full);
            Assert.Equal(Expected(1_500_000m), afterPartial);
            Assert.Equal(afterPartial, withCacheBuster);
            Assert.NotEqual(full, afterPartial);
        }

        [Fact]
        public async Task FractionalInvoiceTotal_CannotBePaid_NoQrAndNoDeeplinks()
        {
            var seed = await SeedAsync(tongTien: 1_500_000.5m);
            var owner = await LoginAsync(NewClient(), seed.Tenant);

            var panel = await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
            var data = (await JsonAsync(panel)).GetProperty("data");

            Assert.False(data.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Contains("số tiền lẻ", data.GetProperty("lyDoKhongTheThanhToan").GetString());
            Assert.Equal(0, data.GetProperty("deeplinks").GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}")).StatusCode);
        }

        [Fact]
        public async Task VoidingTheOnlyPayment_ReopensTheFullTransferBlock()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 2_000_000m, DateTime.UtcNow.AddHours(-2));
            var owner = await LoginAsync(NewClient(), seed.Tenant);

            var paid = (await JsonAsync(await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"))).GetProperty("data");
            Assert.False(paid.GetProperty("coTheThanhToan").GetBoolean());

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await db.LichSuThanhToans.SingleAsync(l => l.LichSuThanhToanId == row)).IsDeleted = true;
                await db.SaveChangesAsync();
            }

            var reopened = (await JsonAsync(await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"))).GetProperty("data");
            Assert.True(reopened.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Equal(2_000_000m, reopened.GetProperty("conLai").GetDecimal());
            Assert.Equal("2000000", reopened.GetProperty("soTienSaoChep").GetString());
            Assert.Equal(0, reopened.GetProperty("cacLanThanhToan").GetArrayLength());
            Assert.Equal(12, reopened.GetProperty("deeplinks").GetArrayLength());
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).StatusCode);
        }

        // ---------- Giờ ----------

        [Fact]
        public async Task PanelAndReceipt_ConvertUtcToVietnamTime_AcrossMidnight()
        {
            var seed = await SeedAsync();
            // 23:30 UTC ngày 24/10 là 06:30 ngày 25/10 giờ Việt Nam.
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, new DateTime(2026, 10, 24, 23, 30, 45, DateTimeKind.Utc));
            var owner = await LoginAsync(NewClient(), seed.Tenant);

            var entry = (await JsonAsync(await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}")))
                .GetProperty("data").GetProperty("cacLanThanhToan")[0];
            var receipt = WebUtility.HtmlDecode(await (await owner.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).Content.ReadAsStringAsync());

            Assert.Equal("25/10/2026 06:30", entry.GetProperty("ngayThanhToan").GetString());
            Assert.Contains("25/10/2026 06:30:45", receipt);
            Assert.DoesNotContain("24/10/2026 23:30", receipt);
        }

        // ---------- Dữ liệu do người dùng nhập ----------

        [Fact]
        public async Task HostileRoomName_DoesNotEscapeIntoTheQrFileNameHeader()
        {
            var seed = await SeedAsync(soPhong: "\"><b>x</b>\r\nĐ");
            var owner = await LoginAsync(NewClient(), seed.Tenant);

            var res = await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}?taiVe=true");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var fileName = res.Content.Headers.ContentDisposition?.FileName?.Trim('"');
            Assert.NotNull(fileName);
            Assert.Matches(@"^QR_Phong_[A-Za-z0-9_-]*HD\d+\.png$", fileName);
            Assert.EndsWith($"_HD{seed.HoaDonId}.png", fileName);
            var raw = string.Join("|", res.Content.Headers.ContentDisposition?.ToString() ?? string.Empty);
            Assert.DoesNotContain("<", raw);
            Assert.DoesNotContain("\r", raw);
            Assert.DoesNotContain("\n", raw);
        }

        [Fact]
        public async Task ReceiptPage_EncodesRoomAndBranchAddress_AndPanelJsonCarriesThemAsPlainData()
        {
            var seed = await SeedAsync(soPhong: "P<i>1</i>", diaChi: "<img src=x onerror=alert(1)>");
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddHours(-1));
            var owner = await LoginAsync(NewClient(), seed.Tenant);

            var raw = await (await owner.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).Content.ReadAsStringAsync();
            Assert.DoesNotContain("<img src=x", raw);
            Assert.DoesNotContain("<i>1</i>", raw);
            Assert.Contains("&lt;img src=x", raw);

            // Panel là JSON: dữ liệu giữ nguyên, việc mã hóa HTML do esc() trong JS làm (kiểm ở test tĩnh bên dưới).
            var data = (await JsonAsync(await owner.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"))).GetProperty("data");
            Assert.Equal("P<i>1</i>", data.GetProperty("tenPhong").GetString());
        }

        // ---------- Kiểm tĩnh JS ----------

        [Fact]
        public async Task PanelScript_NeverConcatenatesServerFieldsIntoHtmlWithoutEscaping()
        {
            var js = await (await NewClient().GetAsync("/js/khachthue-thanhtoan.js")).Content.ReadAsStringAsync();

            // Không nối thẳng trường nào của p/d/e/last/proof vào chuỗi HTML (mọi lần dùng phải nằm trong esc(), vnd() hoặc encodeURIComponent()).
            var rawConcat = Regex.Matches(js, @"\+\s*(?:p|d|e|last|proof|entry|res\.data)\.\w+");
            Assert.True(rawConcat.Count == 0, "Nối thẳng trường từ server vào HTML: " + string.Join(", ", rawConcat.Select(m => m.Value)));

            // Không dùng template literal để ghép HTML.
            Assert.DoesNotContain("${", js);

            // Hai chỗ đặt giá trị server vào thuộc tính HTML phải qua esc().
            Assert.Contains("href=\"' + esc(d.url)", js);
            Assert.Contains("data-copy=\"' + esc(copyValue)", js);

            // Không đưa phản hồi thô của server vào DOM, không eval.
            Assert.DoesNotContain("responseText", js);
            Assert.DoesNotContain("eval(", js);
            Assert.DoesNotContain("document.write", js);

            // esc() phải mã hóa đủ năm ký tự nguy hiểm.
            var escBody = Regex.Match(js, @"function esc\(value\)\s*\{.*?\n    \}", RegexOptions.Singleline).Value;
            foreach (var needle in new[] { "&amp;", "&lt;", "&gt;", "&quot;", "&#039;" })
            {
                Assert.Contains(needle, escBody);
            }

            // Không còn gọi sang các endpoint đã gỡ hay bất kỳ POST nào.
            Assert.DoesNotContain("$.post", js);
            Assert.DoesNotContain("method: 'POST'", js);
            Assert.DoesNotContain("type: 'POST'", js);
        }

        [Fact]
        public async Task PanelScript_DeeplinkGridIsLimitedToTwelveBanksFromTheServer_AndTheRecheckButtonAlwaysRenders()
        {
            var js = await (await NewClient().GetAsync("/js/khachthue-thanhtoan.js")).Content.ReadAsStringAsync();

            // Lưới dựng từ danh sách server trả về, không hard-code appId trong JS.
            Assert.DoesNotContain("dl.vietqr.io", js);
            Assert.Contains("(p.deeplinks || [])", js);
            // Nút kiểm tra lại không phụ thuộc trạng thái kết nối SignalR hay tham số nào.
            Assert.Contains("id=\"ktPayRecheck\"", js);
            Assert.DoesNotContain("setTimeout", js);
        }
    }
}
