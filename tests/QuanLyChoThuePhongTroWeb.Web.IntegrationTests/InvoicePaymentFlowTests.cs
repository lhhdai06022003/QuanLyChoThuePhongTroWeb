using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    // Luồng thanh toán hóa đơn qua HTTP (spec thanh toán §32, Web.IntegrationTests).
    [Collection(WebTestCollection.Name)]
    public class InvoicePaymentFlowTests
    {
        private const string Password = "Password123!";
        private readonly CustomWebApplicationFactory _factory;

        public InvoicePaymentFlowTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed record Seed(
            int HoaDonId, int BranchId, string Tenant, string OtherTenant, string StaffA, string StaffB, string Admin);

        private async Task<Seed> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branchA = new ChiNhanh { TenChiNhanh = $"CN_A_{s}", MaChiNhanh = $"A{s[..5]}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "A" };
            var branchB = new ChiNhanh { TenChiNhanh = $"CN_B_{s}", MaChiNhanh = $"B{s[..5]}", DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            db.ChiNhanhs.AddRange(branchA, branchB);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branchA.ChiNhanhId, SoPhong = $"P_{s[..5]}", GiaThue = 2000000m, DienTich = 20, MoTa = "P" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {s}", SoDienThoai = "0911000001", CCCD = $"079{s[..6]}1", Email = $"k_{s}@test.com" };
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
                MaHoaDon = $"HD-PAY-{s}",
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

            var seed = new Seed(invoice.HoaDonId, branchA.ChiNhanhId, $"tpay_{s}", $"opay_{s}", $"sa_{s}", $"sb_{s}", $"ad_{s}");
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

        private async Task<YeuCauThanhToanHoaDon> SeedRequestAsync(int hoaDonId, TrangThaiYeuCauThanhToan trangThai, DateTime createdUtc)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var code = $"TT{hoaDonId}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var request = YeuCauThanhToanHoaDon.Tao(hoaDonId, code, 2000000m, createdUtc.AddHours(24), null, createdUtc);
            if (trangThai == TrangThaiYeuCauThanhToan.DaHuy)
            {
                request.Huy(null, createdUtc.AddMinutes(1), "khách hủy");
            }
            else if (trangThai == TrangThaiYeuCauThanhToan.HetHan)
            {
                request.DanhDauHetHan(createdUtc.AddHours(25));
            }

            db.YeuCauThanhToanHoaDons.Add(request);
            await db.SaveChangesAsync();
            return request;
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

        private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string token, string url, object body)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            if (token != null) req.Headers.Add("RequestVerificationToken", token);
            return client.SendAsync(req);
        }

        private static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        private static string VnNow(int minutesOffset = 0) =>
            DateTime.UtcNow.AddHours(7).AddMinutes(minutesOffset).ToString("yyyy-MM-dd'T'HH:mm");

        [Fact]
        public async Task TenantPaysByQr_StaffScopeEnforced_AdminConfirms_InvoicePaid()
        {
            var seed = await SeedAsync();
            var bankCode = $"FT-WEB-{Guid.NewGuid().ToString("N")[..8]}";
            var (tenant, tenantToken) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            // Tạo lượt và lấy QR theo lượt
            var created = await PostJsonAsync(tenant, tenantToken, "/KhachThue/HoaDon/ThanhToan/Tao", new { hoaDonId = seed.HoaDonId });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var yeuCauId = (await JsonAsync(created)).GetProperty("data").GetProperty("yeuCauId").GetInt32();

            var qr = await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{yeuCauId}");
            Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
            Assert.Equal("image/png", qr.Content.Headers.ContentType?.MediaType);

            // Nộp minh chứng (storage giả)
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };
            var form = new MultipartFormDataContent
            {
                { new StringContent(yeuCauId.ToString()), "yeuCauId" },
                { new StringContent(VnNow(-5)), "ngayChuyen" },
                { new StringContent(bankCode), "maGiaoDich" }
            };
            var file = new ByteArrayContent(jpeg);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", "bienlai.jpg");
            var upload = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/HoaDon/ThanhToan/NopMinhChung") { Content = form };
            upload.Headers.Add("RequestVerificationToken", tenantToken);
            var submitted = await tenant.SendAsync(upload);
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
            var minhChungId = (await JsonAsync(submitted)).GetProperty("data").GetProperty("minhChungId").GetInt32();

            var panel = await JsonAsync(await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"));
            Assert.Equal((int)AppTrangThaiYeuCauThanhToan.DangDoiChieu, panel.GetProperty("data").GetProperty("luotGanNhat").GetProperty("trangThaiHieuLuc").GetInt32());

            // Khách khác không thấy hóa đơn, QR hay ảnh
            var (other, _) = await LoginAsync(NewClient(), seed.OtherTenant, "/KhachThue/HoaDon");
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/ThanhToan/QR/{yeuCauId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/KhachThue/HoaDon/ThanhToan/AnhMinhChung/{minhChungId}")).StatusCode);

            // Nhân viên chi nhánh khác: 403
            var (staffB, staffBToken) = await LoginAsync(NewClient(), seed.StaffB, "/QuanLyNhaTro/DoiChieuThanhToan");
            Assert.Equal(HttpStatusCode.Forbidden, (await staffB.GetAsync($"/DoiChieuThanhToan/ChiTiet/{minhChungId}")).StatusCode);
            var forbiddenConfirm = await PostJsonAsync(staffB, staffBToken, "/DoiChieuThanhToan/XacNhan",
                new { minhChungId, soTienThucNhan = 2000000, ngayGiaoDich = VnNow(-4) });
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenConfirm.StatusCode);

            // Nhân viên đúng chi nhánh xem được nhưng không cấu hình trả một phần
            var (staffA, staffAToken) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");
            Assert.Equal(HttpStatusCode.OK, (await staffA.GetAsync($"/DoiChieuThanhToan/ChiTiet/{minhChungId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staffA.GetAsync($"/DoiChieuThanhToan/AnhMinhChung/{minhChungId}")).StatusCode);
            var partial = await PostJsonAsync(staffA, staffAToken, $"/HoaDon/CauHinhThanhToanMotPhan/{seed.HoaDonId}", new { choPhep = true, soTienToiThieu = 500000 });
            Assert.Equal(HttpStatusCode.Forbidden, partial.StatusCode);

            // Admin xác nhận đúng số
            var (admin, adminToken) = await LoginAsync(NewClient(), seed.Admin, "/QuanLyNhaTro/DoiChieuThanhToan");
            var confirmed = await PostJsonAsync(admin, adminToken, "/DoiChieuThanhToan/XacNhan",
                new { minhChungId, soTienThucNhan = 2000000, maGiaoDich = bankCode, ngayGiaoDich = VnNow(-4) });
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = await db.HoaDons.AsNoTracking().FirstAsync(h => h.HoaDonId == seed.HoaDonId);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, invoice.TrangThaiHoaDon);
            var ledger = await db.LichSuThanhToans.AsNoTracking().SingleAsync(l => l.HoaDonId == seed.HoaDonId);
            Assert.Equal(minhChungId, ledger.MinhChungThanhToanHoaDonId);
            Assert.Equal(bankCode, ledger.MaGiaoDich);
        }

        [Fact]
        public async Task Lookup_FindsExpiredAndCancelled_ButNotForOtherBranchStaff()
        {
            var seed = await SeedAsync();
            var expired = await SeedRequestAsync(seed.HoaDonId, TrangThaiYeuCauThanhToan.HetHan, DateTime.UtcNow.AddDays(-3));
            var cancelled = await SeedRequestAsync(seed.HoaDonId, TrangThaiYeuCauThanhToan.DaHuy, DateTime.UtcNow.AddDays(-2));

            var (staffA, _) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/DoiChieuThanhToan");
            var a = await staffA.GetAsync($"/DoiChieuThanhToan/TraCuu?ma={expired.MaYeuCau.ToLowerInvariant()}");
            var b = await staffA.GetAsync($"/DoiChieuThanhToan/TraCuu?ma={cancelled.MaYeuCau}");
            Assert.Equal(HttpStatusCode.OK, a.StatusCode);
            Assert.Equal("Hết hạn", (await JsonAsync(a)).GetProperty("data").GetProperty("luotThanhToan").GetProperty("trangThaiText").GetString());
            Assert.Equal("Đã hủy", (await JsonAsync(b)).GetProperty("data").GetProperty("luotThanhToan").GetProperty("trangThaiText").GetString());

            var (staffB, _) = await LoginAsync(NewClient(), seed.StaffB, "/QuanLyNhaTro/DoiChieuThanhToan");
            Assert.Equal(HttpStatusCode.NotFound, (await staffB.GetAsync($"/DoiChieuThanhToan/TraCuu?ma={expired.MaYeuCau}")).StatusCode);
        }

        [Fact]
        public async Task Panel_ShowsOverdueRequestAsExpired_AndAllowsNewRequest()
        {
            var seed = await SeedAsync();
            await SeedRequestAsync(seed.HoaDonId, TrangThaiYeuCauThanhToan.ChoThanhToan, DateTime.UtcNow.AddHours(-30));

            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");
            var panel = (await JsonAsync(await tenant.GetAsync($"/KhachThue/HoaDon/ThanhToan/{seed.HoaDonId}"))).GetProperty("data");

            Assert.Equal((int)AppTrangThaiYeuCauThanhToan.HetHan, panel.GetProperty("luotGanNhat").GetProperty("trangThaiHieuLuc").GetInt32());
            Assert.True(panel.GetProperty("coTheTaoLuot").GetBoolean());
        }

        [Fact]
        public async Task Post_WithoutAntiforgery_IsRejected()
        {
            var seed = await SeedAsync();
            var (tenant, _) = await LoginAsync(NewClient(), seed.Tenant, "/KhachThue/HoaDon");

            var res = await PostJsonAsync(tenant, null!, "/KhachThue/HoaDon/ThanhToan/Tao", new { hoaDonId = seed.HoaDonId });

            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.YeuCauThanhToanHoaDons.AnyAsync(y => y.HoaDonId == seed.HoaDonId));
        }

        [Fact]
        public async Task ManualCollection_AndVoid_UseNewContract()
        {
            var seed = await SeedAsync();
            var (staffA, staffAToken) = await LoginAsync(NewClient(), seed.StaffA, "/QuanLyNhaTro/QuanLyHoaDon");

            var collected = await PostJsonAsync(staffA, staffAToken, "/HoaDon/ThuTien", new
            {
                hoaDonId = seed.HoaDonId,
                soTienThucNhan = 500000,
                phuongThucThanhToan = 0,
                ngayThanhToan = VnNow(-1),
                ghiChu = "Thu một phần tiền mặt"
            });
            Assert.Equal(HttpStatusCode.OK, collected.StatusCode);
            var lichSuId = (await JsonAsync(collected)).GetProperty("data").GetProperty("lichSuThanhToanId").GetInt32();

            // Nhân viên không hủy ghi nhận được
            var staffVoid = new HttpRequestMessage(HttpMethod.Delete, $"/LichSuThanhToan/HuyThanhToan/{lichSuId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { lyDo = "sai" }), Encoding.UTF8, "application/json")
            };
            staffVoid.Headers.Add("RequestVerificationToken", staffAToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await staffA.SendAsync(staffVoid)).StatusCode);

            var (admin, adminToken) = await LoginAsync(NewClient(), seed.Admin, "/QuanLyNhaTro/LichSuThanhToan");
            var adminVoid = new HttpRequestMessage(HttpMethod.Delete, $"/LichSuThanhToan/HuyThanhToan/{lichSuId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { lyDo = "Ghi nhầm" }), Encoding.UTF8, "application/json")
            };
            adminVoid.Headers.Add("RequestVerificationToken", adminToken);
            Assert.Equal(HttpStatusCode.OK, (await admin.SendAsync(adminVoid)).StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await db.HoaDons.AsNoTracking().FirstAsync(h => h.HoaDonId == seed.HoaDonId)).TrangThaiHoaDon);
            Assert.True(await db.LichSuTrangThaiHoaDons.AnyAsync(l => l.HoaDonId == seed.HoaDonId && l.LyDo!.StartsWith("Hủy ghi nhận thanh toán")));
        }
    }
}
