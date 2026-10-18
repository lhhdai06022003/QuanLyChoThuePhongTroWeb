using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Cổng khách thuê của thanh toán trực tuyến, đợt 1 phần 3 (spec thanh toán trực tuyến §3.1-§3.4, §8):
    // panel QR theo số còn lại, tải ảnh QR, biên lai web của khách và quản lý, endpoint tạo lượt/nộp minh chứng đã gỡ.
    [Collection(WebTestCollection.Name)]
    public class TenantOnlinePaymentPortalTests
    {
        private const string Password = "Password123!";
        private const string ReceiptTitle = "BIÊN LAI XÁC NHẬN THU TIỀN PHÒNG TRỌ";
        // 20:00 UTC ngày 24/10 là 03:00 ngày 25/10 giờ Việt Nam: kiểm cả việc đổi múi giờ qua nửa đêm.
        private static readonly DateTime DueDateUtc = new(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc);

        private readonly CustomWebApplicationFactory _factory;

        public TenantOnlinePaymentPortalTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seed(
            int HoaDonId, string MaHoaDon, string Room, string TenantName, string BranchName,
            string Tenant, string OtherTenant, string StaffA, string StaffB, string Admin);

        private async Task<Seed> SeedAsync(string? soPhong = null, string? tenantName = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branchA = new ChiNhanh { TenChiNhanh = $"CN_A_{s}", MaChiNhanh = TestCodes.MaChiNhanh("A"), DiaChi = $"1 Đường A {s}", SoDienThoai = "0900000001", MoTa = "A" };
            var branchB = new ChiNhanh { TenChiNhanh = $"CN_B_{s}", MaChiNhanh = TestCodes.MaChiNhanh("B"), DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            db.ChiNhanhs.AddRange(branchA, branchB);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branchA.ChiNhanhId, SoPhong = soPhong ?? $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = tenantName ?? $"Khach {s}", SoDienThoai = "0911000001", CCCD = $"079{s[..6]}1", Email = $"k_{s}@test.com" };
            var other = new NguoiThue { HoVaTen = $"Khac {s}", SoDienThoai = "0911000002", CCCD = $"079{s[..6]}2", Email = $"o_{s}@test.com" };
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
                MaHoaDon = $"HD-ONL-{s}",
                Thang = 9,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow.AddDays(-1),
                HanThanhToan = DueDateUtc,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền phòng", DonGia = 2000000m, SoLuong = 1, TongTien = 2000000m }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            var seed = new Seed(invoice.HoaDonId, invoice.MaHoaDon, room.SoPhong, tenant.HoVaTen, branchA.TenChiNhanh,
                $"tonl_{s}", $"oonl_{s}", $"sa_{s}", $"sb_{s}", $"ad_{s}");
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

        // Ghi thẳng một dòng sổ (không qua webhook) để dựng các trạng thái trả một phần, trả đủ, đã hủy ghi nhận.
        private async Task<int> AddLedgerAsync(
            int hoaDonId, decimal soTien, DateTime ngayThanhToanUtc, string? maGiaoDich = null, bool isDeleted = false,
            string ghiChu = "", PhuongThucThanhToan phuongThuc = PhuongThucThanhToan.ChuyenKhoan)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = new LichSuThanhToan
            {
                HoaDonId = hoaDonId,
                MaGiaoDich = maGiaoDich ?? $"GD-{Guid.NewGuid():N}"[..24],
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = ngayThanhToanUtc,
                NgayXacNhan = ngayThanhToanUtc,
                IsDeleted = isDeleted,
                GhiChu = ghiChu
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

        private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        // Razor mã hóa ký tự ngoài ASCII (Đ, Ã, ...) thành &#x...; nên so khớp chữ tiếng Việt phải giải mã HTML trước.
        private static async Task<string> HtmlTextAsync(HttpResponseMessage res) => WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

        private async Task<JsonElement> PanelAsync(HttpClient client, int hoaDonId)
        {
            var res = await client.GetAsync($"/KhachThue/HoaDon/ThanhToan/{hoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return (await JsonAsync(res)).GetProperty("data");
        }

        // ---------- Panel thanh toán ----------

        [Fact]
        public async Task Panel_Owner_ReturnsRemainingAmountTransferCodeAndDeeplinks()
        {
            var seed = await SeedAsync();
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var data = await PanelAsync(tenant, seed.HoaDonId);

            Assert.Equal(seed.HoaDonId, data.GetProperty("hoaDonId").GetInt32());
            Assert.Equal(seed.MaHoaDon, data.GetProperty("maHoaDon").GetString());
            Assert.Equal(seed.Room, data.GetProperty("tenPhong").GetString());
            Assert.Equal(seed.BranchName, data.GetProperty("tenChiNhanh").GetString());
            Assert.Equal(9, data.GetProperty("thang").GetInt32());
            Assert.Equal(2026, data.GetProperty("nam").GetInt32());
            Assert.Equal(2_000_000m, data.GetProperty("tongTien").GetDecimal());
            Assert.Equal(0m, data.GetProperty("daThu").GetDecimal());
            Assert.Equal(2_000_000m, data.GetProperty("conLai").GetDecimal());
            Assert.Equal("25/10/2026", data.GetProperty("hanThanhToan").GetString());
            Assert.True(data.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("lyDoKhongTheThanhToan").ValueKind);
            Assert.Equal($"TTPTHD{seed.HoaDonId}", data.GetProperty("maChuyenKhoan").GetString());
            Assert.Equal("MB", data.GetProperty("tenNganHang").GetString());
            Assert.Equal(CustomWebApplicationFactory.TestBankAccount, data.GetProperty("soTaiKhoan").GetString());
            Assert.Equal("NHA TRO TEST", data.GetProperty("chuTaiKhoan").GetString());
            Assert.Equal("2000000", data.GetProperty("soTienSaoChep").GetString());
            Assert.Equal(0, data.GetProperty("cacLanThanhToan").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("minhChungChoDoiChieu").ValueKind);

            var links = data.GetProperty("deeplinks").EnumerateArray().ToList();
            Assert.Equal(12, links.Count);
            Assert.All(links, link =>
            {
                var url = link.GetProperty("url").GetString()!;
                Assert.StartsWith("https://dl.vietqr.io/pay?", url);
                Assert.Contains("am=2000000", url);
                Assert.Contains($"tn=TTPTHD{seed.HoaDonId}", url);
                Assert.Contains($"ba={CustomWebApplicationFactory.TestBankAccount}@MB", url);
            });
            Assert.Equal("MBBank", links[0].GetProperty("tenNganHang").GetString());
            Assert.Equal("VIB", links[11].GetProperty("tenNganHang").GetString());
        }

        [Fact]
        public async Task Panel_And_Qr_OtherTenant_404()
        {
            var seed = await SeedAsync();
            var (other, _) = await LoginAsync(NewClient(), seed.OtherTenant, "/KhachThue/HoaDon");

            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}?taiVe=true")).StatusCode);
        }

        [Fact]
        public async Task Panel_PartiallyPaid_ExcludesVoidedRow()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddDays(-2), phuongThuc: PhuongThucThanhToan.TienMat);
            await AddLedgerAsync(seed.HoaDonId, 300_000m, DateTime.UtcNow.AddDays(-1), isDeleted: true);
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var data = await PanelAsync(tenant, seed.HoaDonId);

            Assert.Equal(500_000m, data.GetProperty("daThu").GetDecimal());
            Assert.Equal(1_500_000m, data.GetProperty("conLai").GetDecimal());
            Assert.Equal("1500000", data.GetProperty("soTienSaoChep").GetString());
            Assert.True(data.GetProperty("coTheThanhToan").GetBoolean());
            var entries = data.GetProperty("cacLanThanhToan").EnumerateArray().ToList();
            var entry = Assert.Single(entries);
            Assert.Equal(1, entry.GetProperty("dotThu").GetInt32());
            Assert.Equal(500_000m, entry.GetProperty("soTien").GetDecimal());
            Assert.Equal("Tiền mặt", entry.GetProperty("phuongThuc").GetString());
            Assert.Matches(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}$", entry.GetProperty("ngayThanhToan").GetString()!);
            Assert.All(data.GetProperty("deeplinks").EnumerateArray(), link => Assert.Contains("am=1500000", link.GetProperty("url").GetString()));
        }

        [Fact]
        public async Task Panel_FullyPaid_NoTransferBlock_QrIs400()
        {
            var seed = await SeedAsync();
            await AddLedgerAsync(seed.HoaDonId, 2_000_000m, DateTime.UtcNow.AddHours(-1));
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var data = await PanelAsync(tenant, seed.HoaDonId);

            Assert.Equal(0m, data.GetProperty("conLai").GetDecimal());
            Assert.False(data.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Contains("đã được thanh toán đủ", data.GetProperty("lyDoKhongTheThanhToan").GetString());
            Assert.Equal(0, data.GetProperty("deeplinks").GetArrayLength());
            Assert.Equal(1, data.GetProperty("cacLanThanhToan").GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}")).StatusCode);
        }

        [Fact]
        public async Task Panel_InvoiceCancelledAfterSending_Is200ButCannotPay_AndQrIs400()
        {
            var seed = await SeedAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var invoice = await db.HoaDons.SingleAsync(h => h.HoaDonId == seed.HoaDonId);
                invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                invoice.IsDeleted = true;
                await db.SaveChangesAsync();
            }

            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");
            var data = await PanelAsync(tenant, seed.HoaDonId);

            Assert.False(data.GetProperty("coTheThanhToan").GetBoolean());
            Assert.Equal("Hóa đơn không còn ở trạng thái nhận thanh toán.", data.GetProperty("lyDoKhongTheThanhToan").GetString());
            Assert.Equal(0, data.GetProperty("deeplinks").GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}")).StatusCode);
        }

        [Fact]
        public async Task Panel_LegacyProofWaitingForReview_IsReported_AndTheProofImageStaysReadable()
        {
            var seed = await SeedAsync();
            int minhChungId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;
                var request = YeuCauThanhToanHoaDon.Tao(seed.HoaDonId, $"TT{seed.HoaDonId}{Guid.NewGuid():N}"[..18].ToUpperInvariant(), 2000000m, now.AddHours(24), null, now.AddHours(-1));
                request.NopMinhChung("https://example.test/p.jpg", "payment-proofs/p", null, now.AddMinutes(-30), null, now.AddMinutes(-20));
                db.YeuCauThanhToanHoaDons.Add(request);
                await db.SaveChangesAsync();
                minhChungId = request.MinhChungThanhToanHoaDons.Single().MinhChungThanhToanHoaDonId;
            }

            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");
            var data = await PanelAsync(tenant, seed.HoaDonId);

            var notice = data.GetProperty("minhChungChoDoiChieu");
            Assert.Equal(minhChungId, notice.GetProperty("minhChungId").GetInt32());
            Assert.Equal(2_000_000m, notice.GetProperty("soTien").GetDecimal());
            Assert.Matches(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}$", notice.GetProperty("ngayNop").GetString()!);
            Assert.True(data.GetProperty("coTheThanhToan").GetBoolean());

            var image = await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/AnhMinhChung/{minhChungId}");
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/jpeg", image.Content.Headers.ContentType?.MediaType);

            var (other, _) = await LoginAsync(NewClient(), seed.OtherTenant, "/KhachThue/HoaDon");
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/ThanhToan/AnhMinhChung/{minhChungId}")).StatusCode);
        }

        // ---------- Ảnh QR ----------

        [Fact]
        public async Task Qr_Inline_IsPng_WithoutAttachment()
        {
            var seed = await SeedAsync();
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
            Assert.Null(res.Content.Headers.ContentDisposition);
            var bytes = await res.Content.ReadAsByteArrayAsync();
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
        }

        [Fact]
        public async Task Qr_Download_IsAttachment_WithAsciiFileName()
        {
            var s = Guid.NewGuid().ToString("N")[..4];
            var seed = await SeedAsync(soPhong: $"Phòng Đ{s}");
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var res = await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}?taiVe=true");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
            Assert.Equal("attachment", res.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal($"QR_Phong_Phong_D{s}_HD{seed.HoaDonId}.png", res.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        }

        // ---------- Biên lai của khách thuê ----------

        [Fact]
        public async Task Receipt_Tenant_ShowsInstalmentRunningTotalAndSurplus()
        {
            var seed = await SeedAsync();
            // 02:30:15 UTC ngày 03/10 là 09:30:15 giờ Việt Nam.
            var first = await AddLedgerAsync(seed.HoaDonId, 500_000m, new DateTime(2026, 10, 3, 2, 30, 15, DateTimeKind.Utc), phuongThuc: PhuongThucThanhToan.TienMat);
            var second = await AddLedgerAsync(seed.HoaDonId, 700_000m, new DateTime(2026, 10, 4, 2, 30, 15, DateTimeKind.Utc));
            var third = await AddLedgerAsync(seed.HoaDonId, 800_000m, new DateTime(2026, 10, 5, 2, 30, 15, DateTimeKind.Utc), ghiChu: PaymentNoteTags.TienThua(100_000m, "chuyển dư"));
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var page1 = await HtmlTextAsync(await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{first}"));
            var page2 = await HtmlTextAsync(await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{second}"));
            var page3 = await HtmlTextAsync(await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{third}"));

            Assert.Contains("03/10/2026 09:30:15", page1);
            Assert.Contains("04/10/2026 09:30:15", page2);
            Assert.Contains("05/10/2026 09:30:15", page3);
            Assert.Contains("XÁC NHẬN THU ĐỢT 1", page1);
            Assert.DoesNotContain("ĐÃ THU TIỀN", page1);
            Assert.Contains("500.000đ", page1);
            Assert.Contains("1.500.000đ", page1);

            Assert.Contains("XÁC NHẬN THU ĐỢT 2", page2);
            Assert.DoesNotContain("ĐÃ THU TIỀN", page2);
            Assert.Contains("1.200.000đ", page2);
            Assert.Contains("800.000đ", page2);

            Assert.Contains("ĐÃ THU TIỀN", page3);
            Assert.DoesNotContain("XÁC NHẬN THU ĐỢT", page3);
            Assert.Contains("Tiền thừa 100.000đ", page3);
            Assert.DoesNotContain("Tiền thừa", page1 + page2);

            foreach (var page in new[] { page1, page2, page3 })
            {
                Assert.Contains(ReceiptTitle, page);
                Assert.Contains("window.print()", page);
                Assert.Contains($"{seed.MaHoaDon} (#HD{seed.HoaDonId})", page);
                Assert.Contains(seed.Room, page);
                Assert.Contains(seed.TenantName, page);
                Assert.Contains(seed.BranchName, page);
                Assert.Contains("Tháng 9/2026", page);
                Assert.Contains("2.000.000đ", page);
                Assert.Contains("/css/bienlai.css", page);
                Assert.Contains($"href=\"/KhachThue/HoaDon?hoaDonId={seed.HoaDonId}\"", page);
            }
        }

        [Fact]
        public async Task Receipt_ShowsTheBankReferenceOnlyForSePayRowsKeyedByAReference()
        {
            var seed = await SeedAsync();
            // Mã giao dịch là duy nhất trong sổ (chưa xóa) và DB test được dùng lại giữa các lần chạy, nên mỗi lần một mã mới.
            var unique = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var cashCode = $"TM-{unique}";
            var bankReference = $"FT{unique}";
            var idKey = $"SEPAY-ID-{unique}";
            var cash = await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddDays(-3), maGiaoDich: cashCode, phuongThuc: PhuongThucThanhToan.TienMat);
            var bank = await AddLedgerAsync(seed.HoaDonId, 700_000m, DateTime.UtcNow.AddDays(-2), maGiaoDich: $"SEPAY-{bankReference}");
            var byId = await AddLedgerAsync(seed.HoaDonId, 800_000m, DateTime.UtcNow.AddDays(-1), maGiaoDich: idKey);
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var cashPage = await HtmlTextAsync(await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{cash}"));
            var bankPage = await HtmlTextAsync(await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{bank}"));
            var byIdPage = await HtmlTextAsync(await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{byId}"));

            Assert.DoesNotContain("Mã tham chiếu ngân hàng", cashPage);
            Assert.Contains("Mã giao dịch", cashPage);
            Assert.Contains(cashCode, cashPage);
            Assert.Contains("Tiền mặt", cashPage);

            Assert.Contains("Mã tham chiếu ngân hàng", bankPage);
            Assert.Contains($">{bankReference}<", bankPage);
            Assert.Contains($"SEPAY-{bankReference}", bankPage);
            Assert.Contains("Chuyển khoản 24/7 (VietQR, tự động)", bankPage);

            Assert.DoesNotContain("Mã tham chiếu ngân hàng", byIdPage);
            Assert.Contains(idKey, byIdPage);
        }

        [Fact]
        public async Task Receipt_EncodesTextTheUserControls()
        {
            var seed = await SeedAsync(tenantName: "<b>Khach</b> & \"Co\"");
            var row = await AddLedgerAsync(seed.HoaDonId, 2_000_000m, DateTime.UtcNow.AddHours(-1));
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var raw = await (await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).Content.ReadAsStringAsync();

            Assert.DoesNotContain("<b>Khach</b>", raw);
            Assert.Contains("&lt;b&gt;Khach&lt;/b&gt;", raw);
        }

        [Fact]
        public async Task Receipt_Tenant_OtherTenant_And_VoidedRow_404()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddDays(-2));
            var voided = await AddLedgerAsync(seed.HoaDonId, 300_000m, DateTime.UtcNow.AddDays(-1), isDeleted: true);

            var (other, _) = await LoginAsync(NewClient(), seed.OtherTenant, "/KhachThue/HoaDon");
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).StatusCode);

            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");
            Assert.Equal(HttpStatusCode.OK, (await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{row}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync($"/KhachThue/HoaDon/BienLai/{voided}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync("/KhachThue/HoaDon/BienLai/2147483647")).StatusCode);
        }

        [Fact]
        public async Task Receipt_AndPanel_AreRedirectedToLogin_ForAnonymousAndStaff()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 500_000m, DateTime.UtcNow.AddDays(-1));
            var tenantUrls = new[]
            {
                $"/KhachThue/HoaDon/BienLai/{row}",
                $"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}",
                $"/KhachThue/HoaDon/ThanhToan/QR/{seed.HoaDonId}"
            };

            var anonymous = NewClient();
            foreach (var url in tenantUrls.Append($"/QuanLyNhaTro/HoaDon/BienLai/{row}"))
            {
                var res = await anonymous.GetAsync(url);
                Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
                Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
            }

            var (staff, _) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");
            foreach (var url in tenantUrls)
            {
                var res = await staff.GetAsync(url);
                Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
                Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
            }
        }

        // ---------- Biên lai phía quản lý ----------

        [Fact]
        public async Task Receipt_Manager_BranchScoped()
        {
            var seed = await SeedAsync();
            var row = await AddLedgerAsync(seed.HoaDonId, 2_000_000m, DateTime.UtcNow.AddDays(-1));
            var voided = await AddLedgerAsync(seed.HoaDonId, 300_000m, DateTime.UtcNow.AddDays(-2), isDeleted: true);
            var url = $"/QuanLyNhaTro/HoaDon/BienLai/{row}";

            var (staffA, _) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");
            var ok = await staffA.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var page = await HtmlTextAsync(ok);
            Assert.Contains(ReceiptTitle, page);
            Assert.Contains("ĐÃ THU TIỀN", page);
            Assert.Contains("window.print()", page);
            Assert.Contains($"href=\"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={seed.HoaDonId}\"", page);
            Assert.Equal(HttpStatusCode.NotFound, (await staffA.GetAsync($"/QuanLyNhaTro/HoaDon/BienLai/{voided}")).StatusCode);

            var (staffB, _) = await LoginAsync(NewClient(), seed.StaffB, "/QuanLyNhaTro/DoiChieuThanhToan");
            Assert.Equal(HttpStatusCode.Forbidden, (await staffB.GetAsync(url)).StatusCode);

            var (admin, _) = await LoginAsync(NewClient(), seed.Admin, "/QuanLyNhaTro/DoiChieuThanhToan");
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/QuanLyNhaTro/HoaDon/BienLai/{voided}")).StatusCode);

            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");
            var denied = await tenant.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", denied.Headers.Location!.ToString());
        }

        // ---------- Endpoint khách tạo lượt và nộp minh chứng đã gỡ ----------

        // Không còn action nào nhận các POST này. /ThanhToan/Tao và /ThanhToan/NopMinhChung có cùng số đoạn với route GET
        // /ThanhToan/{hoaDonId:int} nên ASP.NET Core trả 405 (đường dẫn khớp nhưng sai phương thức, ràng buộc int chưa được xét);
        // /ThanhToan/Huy/1 không khớp route nào nên trả 404. Cả hai đều có nghĩa là endpoint đã gỡ và không có gì được tạo.
        private static void AssertEndpointGone(HttpResponseMessage response)
        {
            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"Endpoint đã gỡ phải trả 404 hoặc 405, thực tế {(int)response.StatusCode}.");
        }

        [Fact]
        public async Task RemovedTenantEndpoints_Return404Or405_AndCreateNothing()
        {
            var seed = await SeedAsync();
            var (tenant, token) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var create = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/HoaDon/ThanhToan/Tao")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { hoaDonId = seed.HoaDonId }), Encoding.UTF8, "application/json")
            };
            create.Headers.Add("RequestVerificationToken", token);
            AssertEndpointGone(await tenant.SendAsync(create));

            var cancel = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/HoaDon/ThanhToan/Huy/1");
            cancel.Headers.Add("RequestVerificationToken", token);
            Assert.Equal(HttpStatusCode.NotFound, (await tenant.SendAsync(cancel)).StatusCode);

            var form = new MultipartFormDataContent { { new StringContent("1"), "yeuCauId" } };
            var upload = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/HoaDon/ThanhToan/NopMinhChung") { Content = form };
            upload.Headers.Add("RequestVerificationToken", token);
            AssertEndpointGone(await tenant.SendAsync(upload));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.YeuCauThanhToanHoaDons.AnyAsync(y => y.HoaDonId == seed.HoaDonId));
        }

        // ---------- Đăng nhập quay lại đúng hóa đơn ----------

        [Fact]
        public async Task Login_WithInvoiceReturnUrl_ReturnsToInvoice()
        {
            var seed = await SeedAsync();
            var invoiceUrl = $"/KhachThue/HoaDon?hoaDonId={seed.HoaDonId}";
            var client = NewClient();

            var anonymous = await client.GetAsync(invoiceUrl);
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
            var location = anonymous.Headers.Location!.ToString();
            Assert.Contains("ReturnUrl=", location, StringComparison.OrdinalIgnoreCase);

            // Trang đăng nhập mang theo ReturnUrl trong ô ẩn, đúng như khách bấm link trong email.
            var loginPage = await client.GetAsync(location);
            var loginHtml = WebUtility.HtmlDecode(await loginPage.Content.ReadAsStringAsync());
            Assert.Contains($"name=\"returnUrl\" value=\"{invoiceUrl}\"", loginHtml);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", Token(loginHtml)),
                new KeyValuePair<string, string>("TenDangNhap", seed.Tenant),
                new KeyValuePair<string, string>("MatKhau", Password),
                new KeyValuePair<string, string>("ReturnUrl", invoiceUrl)
            });
            var login = await client.PostAsync("/QuanLyNhaTro/DangNhap", content);

            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal(invoiceUrl, login.Headers.Location!.OriginalString);
        }

        // ---------- Tài nguyên giao diện ----------

        [Fact]
        public async Task PanelScript_HasNoLegacyEndpoints_NoPolling_AndListensOnTheSharedHub()
        {
            var client = NewClient();
            var js = await (await client.GetAsync("/js/khachthue-thanhtoan.js")).Content.ReadAsStringAsync();

            Assert.DoesNotContain("/Tao", js);
            Assert.DoesNotContain("/Huy/", js);
            Assert.DoesNotContain("NopMinhChung", js);
            Assert.DoesNotContain("setInterval", js);
            Assert.DoesNotContain("alert(", Regex.Replace(js, @"Swal\.fire|\.alert-|alert-", string.Empty));
            Assert.Contains("HoaDonThanhToanCapNhat", js);
            Assert.Contains("ThongBaoHub.on", js);
            Assert.Contains("esc(", js);

            var hub = await (await client.GetAsync("/js/thongbao.js")).Content.ReadAsStringAsync();
            Assert.Contains("window.ThongBaoHub", hub);
            Assert.Contains("attach(connection)", hub);
        }

        [Fact]
        public async Task IndexPage_WiresThePanelToTheSharedHub_AndNoLongerCallsTheRemovedStopApi()
        {
            var seed = await SeedAsync();
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var html = await (await tenant.GetAsync("/KhachThue/HoaDon")).Content.ReadAsStringAsync();

            Assert.Contains("/js/khachthue-thanhtoan.js", html);
            Assert.Contains("KtThanhToan.watch(", html);
            Assert.Contains("dangXemHoaDonId", html);
            Assert.DoesNotContain("KtThanhToan.stop", html);
            Assert.DoesNotContain("QR theo lượt", html);
        }
    }
}
