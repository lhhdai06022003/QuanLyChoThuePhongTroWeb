using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
    [Collection(WebTestCollection.Name)]
    public class MeterAnomalyEndpointTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public MeterAnomalyEndpointTests(CustomWebApplicationFactory factory)
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

        private async Task<HttpClient> LoginAsync(string username, string password)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var csrf = ExtractAntiForgeryToken(await (await client.GetAsync("/QuanLyNhaTro/DangNhap")).Content.ReadAsStringAsync());
            var login = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", csrf),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            }));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            return client;
        }

        private static async Task AssignAsync(ApplicationDbContext db, int staffId, int branchId)
        {
            var admin = await db.NguoiDungs.FirstOrDefaultAsync(u => u.Role == Role.Admin);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staffId,
                ChiNhanhId = branchId,
                IsActive = true,
                NguoiPhanCongId = admin?.NguoiDungId ?? staffId,
                NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task KiemTraBatThuong_Anonymous_Redirects()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync("/DienNuoc/KiemTraBatThuong?phongTroId=1&thang=9&nam=2026&chiSoDienMoi=10");

            Assert.True(response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task KiemTraBatThuong_Tenant_Blocked()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"tenant_{suffix}";
            const string password = "TenantPassword123!";
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            Assert.True((await svc.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.KhachThue })).Success);

            try
            {
                var client = await LoginAsync(username, password);
                var response = await client.GetAsync("/DienNuoc/KiemTraBatThuong?phongTroId=1&thang=9&nam=2026&chiSoDienMoi=10");
                Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                var u = await db.NguoiDungs.FirstOrDefaultAsync(x => x.TenDangNhap == username);
                if (u != null)
                {
                    db.NguoiDungs.Remove(u);
                    await db.SaveChangesAsync();
                }
            }
        }

        [Fact]
        public async Task KiemTraBatThuong_StaffInBranch_WarnsWhenReadingDrops_ThenOtherBranch403()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch1 = new ChiNhanh { TenChiNhanh = $"CN1_{suffix}", MaChiNhanh = TestCodes.MaChiNhanh("A1"), DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            var branch2 = new ChiNhanh { TenChiNhanh = $"CN2_{suffix}", MaChiNhanh = TestCodes.MaChiNhanh("A2"), DiaChi = "B", SoDienThoai = "0900000002", MoTa = "M" };
            db.ChiNhanhs.AddRange(branch1, branch2);
            await db.SaveChangesAsync();
            var room1 = new PhongTro { ChiNhanhId = branch1.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            var room2 = new PhongTro { ChiNhanhId = branch2.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.AddRange(room1, room2);
            await db.SaveChangesAsync();
            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room1.PhongTroId,
                Thang = 9,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 100m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 50m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            Assert.True((await svc.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien })).Success);
            var staff = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);
            await AssignAsync(db, staff.NguoiDungId, branch1.ChiNhanhId);

            try
            {
                var client = await LoginAsync(username, password);

                // Điện mới 80 < cũ 100: bất thường; nước 60 hợp lệ.
                var ok = await client.GetAsync($"/DienNuoc/KiemTraBatThuong?phongTroId={room1.PhongTroId}&thang=9&nam=2026&chiSoDienMoi=80&chiSoNuocMoi=60");
                Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
                var json = await ok.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("ChiSoGiam", json.GetProperty("dien").GetProperty("maLyDo").GetString());
                Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("dien").GetProperty("moTa").GetString()));
                Assert.Equal(JsonValueKind.Null, json.GetProperty("nuoc").ValueKind);

                var forbidden = await client.GetAsync($"/DienNuoc/KiemTraBatThuong?phongTroId={room2.PhongTroId}&thang=9&nam=2026&chiSoDienMoi=80");
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            }
            finally
            {
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staff.NguoiDungId));
                db.NguoiDungs.Remove(staff);
                db.PhongTros.RemoveRange(room1, room2);
                db.ChiNhanhs.RemoveRange(branch1, branch2);
                await db.SaveChangesAsync();
            }
        }
    }
}
