using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Kiểm nối DI thật: khách tải đủ ảnh điện và nước qua HTTP thì hệ thống tự tạo hóa đơn nháp và báo nhân viên.
    [Collection(WebTestCollection.Name)]
    public class AutoInvoiceTriggerTests
    {
        private static readonly byte[] ValidJpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };

        private readonly CustomWebApplicationFactory _factory;

        public AutoInvoiceTriggerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static string ExtractAntiForgeryToken(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success)
            {
                match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            }
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private async Task<(HttpClient Client, string Csrf)> LoginAsync(string username, string password)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var loginPage = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var csrf = ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());
            var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", csrf),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var page = await client.GetAsync("/KhachThue/ChiSoDienNuoc");
            var authCsrf = ExtractAntiForgeryToken(await page.Content.ReadAsStringAsync());
            return (client, string.IsNullOrWhiteSpace(authCsrf) ? csrf : authCsrf);
        }

        private sealed class Seed
        {
            public string TenantUsername { get; init; } = string.Empty;
            public string Password { get; init; } = string.Empty;
            public string TenantEmail { get; init; } = string.Empty;
            public PhongTro Room { get; init; } = null!;
            public HopDong Contract { get; init; } = null!;
            public NguoiDung Staff { get; init; } = null!;
            public int Thang { get; init; }
            public int Nam { get; init; }
        }

        private async Task<Seed> SeedAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            const string password = "Password123!";

            var branch = new ChiNhanh { TenChiNhanh = $"CN_AI_{suffix}", MaChiNhanh = TestCodes.MaChiNhanh("A"), DiaChi = "A", SoDienThoai = "0900000077", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            // Chuỗi tự tạo dừng với lý do ThieuDonGia khi chi nhánh chưa có đơn giá điện/nước cho kỳ có tiêu thụ.
            var dien = new DichVu { TenDichVu = $"Dien {suffix}", DonVi = "kWh", LoaiDichVu = LoaiDichVu.Dien };
            var nuoc = new DichVu { TenDichVu = $"Nuoc {suffix}", DonVi = "m3", LoaiDichVu = LoaiDichVu.Nuoc };
            db.DichVus.AddRange(dien, nuoc);
            await db.SaveChangesAsync();
            db.DichVuChiNhanhs.AddRange(
                new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = dien.DichVuId, GiaDichVu = 3500m },
                new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = nuoc.DichVuId, GiaDichVu = 20000m });
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"AI_{suffix}", GiaThue = 3_000_000m, DienTich = 20, MoTa = "M" };
            var tenant = new NguoiThue { HoVaTen = $"Khach AI {suffix}", SoDienThoai = "0911111111", CCCD = $"0793{suffix[..4]}", Email = $"ai_{suffix}@test.com" };
            db.AddRange(room, tenant);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            // 00:00 ngày 1 giờ Việt Nam của tháng hiện tại, tính bằng UTC.
            var contract = new HopDong
            {
                MaHopDong = $"HDAI_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.SpecifyKind(new DateTime(curYear, curMonth, 1, 0, 0, 0).AddHours(-7), DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 3_000_000m,
                TienCocPhong = 3_000_000m
            };
            db.HopDongs.Add(contract);
            await db.SaveChangesAsync();

            var tenantUser = $"tai_{suffix}";
            var staffUser = $"sai_{suffix}";
            await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = tenantUser, MatKhau = password, Role = AppRole.KhachThue, NguoiThueId = tenant.NguoiThueId });
            await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = staffUser, MatKhau = password, Role = AppRole.NhanVien });
            var staff = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == staffUser);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId,
                ChiNhanhId = branch.ChiNhanhId,
                NguoiPhanCongId = staff.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            });
            await db.SaveChangesAsync();

            return new Seed
            {
                TenantUsername = tenantUser,
                Password = password,
                TenantEmail = tenant.Email,
                Room = room,
                Contract = contract,
                Staff = staff,
                Thang = curMonth,
                Nam = curYear
            };
        }

        private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string csrf, Seed seed, int loaiDongHo)
        {
            var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(seed.Room.PhongTroId.ToString()), "phongTroId");
            multipart.Add(new StringContent(seed.Thang.ToString()), "thang");
            multipart.Add(new StringContent(seed.Nam.ToString()), "nam");
            multipart.Add(new StringContent(loaiDongHo.ToString()), "loaiDongHo");
            var file = new ByteArrayContent(ValidJpegBytes);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            multipart.Add(file, "file", "meter.jpg");

            var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh") { Content = multipart };
            req.Headers.Add("RequestVerificationToken", csrf);
            return await client.SendAsync(req);
        }

        private async Task<List<HoaDon>> InvoicesAsync(Seed seed)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.HoaDons.AsNoTracking().Where(h => h.HopDongId == seed.Contract.HopDongId && !h.IsDeleted).ToListAsync();
        }

        [Fact]
        public async Task TenantUploadsElectricityThenWater_CreatesDraftInvoice_NotifiesStaff_AndSendsNothingToTenant()
        {
            var ocr = _factory.FakeMeterOcrServiceInstance;
            try
            {
                ocr.DefaultSuggestedValue = 20m;
                ocr.DefaultConfidence = 0.95;
                var seed = await SeedAsync();
                var (client, csrf) = await LoginAsync(seed.TenantUsername, seed.Password);

                var first = await UploadAsync(client, csrf, seed, 0);
                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
                Assert.Empty(await InvoicesAsync(seed));

                var second = await UploadAsync(client, csrf, seed, 1);
                Assert.Equal(HttpStatusCode.OK, second.StatusCode);

                var invoice = Assert.Single(await InvoicesAsync(seed));
                Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, invoice.TrangThaiPhatHanh);
                Assert.Null(invoice.HanPhanHoi);
                Assert.DoesNotContain(_factory.FakeEmailServiceInstance.InvoiceEmailCalls,
                    c => c.MaHoaDon == invoice.MaHoaDon || c.ToEmail == seed.TenantEmail);

                using (var scope = _factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var staffNotices = await db.ThongBaos.AsNoTracking()
                        .Where(t => t.NguoiDungId == seed.Staff.NguoiDungId).ToListAsync();
                    var notice = Assert.Single(staffNotices, t => t.TieuDe.Contains("có hóa đơn tự động chờ gửi duyệt"));
                    Assert.Equal($"Phòng {seed.Room.SoPhong} kỳ {seed.Thang:00}/{seed.Nam}: có hóa đơn tự động chờ gửi duyệt", notice.TieuDe);
                    var linkHoaDon = $"/QuanLyNhaTro/QuanLyHoaDon?chiNhanhId={seed.Room.ChiNhanhId}&thang={seed.Thang}&nam={seed.Nam}&hoaDonId={invoice.HoaDonId}";
                    Assert.Equal(linkHoaDon, notice.LinkDieuHuong);
                    Assert.Empty(await db.ThongBaos.AsNoTracking()
                        .Where(t => t.NguoiDung!.NguoiThueId == seed.Contract.NguoiThueId).ToListAsync());
                }

                // Hóa đơn nháp chưa hiện cho khách.
                var list = await client.GetAsync("/KhachThue/HoaDon/GetDanhSach");
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);
                using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
                Assert.DoesNotContain(doc.RootElement.EnumerateArray(), x => x.GetProperty("hoaDonId").GetInt32() == invoice.HoaDonId);

                // Link trong thông báo gửi nhân viên mở được trang quản lý hóa đơn.
                var (staffClient, _) = await LoginAsync(seed.Staff.TenDangNhap, seed.Password);
                var page = await staffClient.GetAsync($"/QuanLyNhaTro/QuanLyHoaDon?chiNhanhId={seed.Room.ChiNhanhId}&thang={seed.Thang}&nam={seed.Nam}&hoaDonId={invoice.HoaDonId}");
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            }
            finally
            {
                ocr.Reset();
            }
        }

        [Fact]
        public async Task TenantUpload_WithLowConfidence_CreatesNoInvoice_AndNoEmail()
        {
            var ocr = _factory.FakeMeterOcrServiceInstance;
            try
            {
                ocr.DefaultSuggestedValue = 20m;
                ocr.DefaultConfidence = 0.5;
                var seed = await SeedAsync();
                var (client, csrf) = await LoginAsync(seed.TenantUsername, seed.Password);

                Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, csrf, seed, 0)).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, csrf, seed, 1)).StatusCode);

                Assert.Empty(await InvoicesAsync(seed));
                Assert.DoesNotContain(_factory.FakeEmailServiceInstance.InvoiceEmailCalls, c => c.ToEmail == seed.TenantEmail);

                // Link trong thông báo gửi nhân viên phải mở được trang chốt điện nước, không phải 404.
                string? link;
                using (var scope = _factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    link = await db.ThongBaos.AsNoTracking()
                        .Where(t => t.NguoiDungId == seed.Staff.NguoiDungId && t.TieuDe.Contains("chưa tạo được hóa đơn tự động"))
                        .Select(t => t.LinkDieuHuong)
                        .FirstOrDefaultAsync();
                }
                Assert.Equal($"/QuanLyNhaTro/ChotDienNuoc?chiNhanhId={seed.Room.ChiNhanhId}&thang={seed.Thang}&nam={seed.Nam}&phongTroId={seed.Room.PhongTroId}", link);

                var (staffClient, _) = await LoginAsync(seed.Staff.TenDangNhap, seed.Password);
                var page = await staffClient.GetAsync(link);
                Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            }
            finally
            {
                ocr.Reset();
            }
        }
    }
}
