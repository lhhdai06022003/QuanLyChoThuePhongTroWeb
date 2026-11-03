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
                Assert.Equal("Chan", json.GetProperty("dien").GetProperty("mucDo").GetString());
                Assert.Equal(JsonValueKind.Null, json.GetProperty("dien").GetProperty("chiSoGoiY").ValueKind);
                Assert.False(json.GetProperty("dien").GetProperty("coTheQuayVong").GetBoolean());
                Assert.True(json.GetProperty("dien").TryGetProperty("sanLuongTheoGoiY", out _));
                Assert.True(json.GetProperty("dien").TryGetProperty("tranCongTo", out _));
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

        // ---------- Bổ sung của Tester: gợi ý sửa số, quay vòng, mức cảnh báo qua HTTP ----------

        // Phòng A của BranchScopeKit có hợp đồng phủ trọn tháng 10/2026 nên trần chung (chưa đủ lịch sử) là 500 kWh / 30 m³.
        private async Task<(BranchScopeWorld World, System.Net.Http.HttpClient Client)> SeedPeriodAsync(decimal dienCu, decimal nuocCu)
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = w.RoomA, Thang = 10, Nam = 2026,
                    ChiSoDienCu = dienCu, ChiSoDienMoi = dienCu, ChiSoNuocCu = nuocCu, ChiSoNuocMoi = nuocCu,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
                });
                await db.SaveChangesAsync();
            }

            var (client, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff);
            return (w, client);
        }

        private static string Url(BranchScopeWorld w, string dien, string? nuoc = null)
            => $"/DienNuoc/KiemTraBatThuong?phongTroId={w.RoomA}&thang=10&nam=2026&chiSoDienMoi={dien}" + (nuoc == null ? "" : $"&chiSoNuocMoi={nuoc}");

        [Fact]
        public async Task KiemTraBatThuong_Rollover_ReturnsRolloverHintForBothMeters()
        {
            var (w, client) = await SeedPeriodAsync(dienCu: 9950m, nuocCu: 9990m);

            var res = await client.GetAsync(Url(w, "100", "3"));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var dien = json.GetProperty("dien");
            Assert.Equal("ChiSoGiam", dien.GetProperty("maLyDo").GetString());
            Assert.Equal("Chan", dien.GetProperty("mucDo").GetString());
            Assert.True(dien.GetProperty("coTheQuayVong").GetBoolean());
            Assert.Equal(10_000m, dien.GetProperty("tranCongTo").GetDecimal());
            Assert.Equal(150m, dien.GetProperty("sanLuongTheoGoiY").GetDecimal());
            Assert.Equal(JsonValueKind.Null, dien.GetProperty("chiSoGoiY").ValueKind);

            var nuoc = json.GetProperty("nuoc");
            Assert.True(nuoc.GetProperty("coTheQuayVong").GetBoolean());
            Assert.Equal(13m, nuoc.GetProperty("sanLuongTheoGoiY").GetDecimal());
        }

        [Fact]
        public async Task KiemTraBatThuong_DecimalPointShift_ReturnsRedLevelWithQuickFix()
        {
            var (w, client) = await SeedPeriodAsync(dienCu: 1500m, nuocCu: 50m);

            var res = await client.GetAsync(Url(w, "16082"));

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var dien = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dien");
            Assert.Equal("NghiSaiChuSo", dien.GetProperty("maLyDo").GetString());
            Assert.Equal("Do", dien.GetProperty("mucDo").GetString());
            Assert.Equal(1608.2m, dien.GetProperty("chiSoGoiY").GetDecimal());
            Assert.Equal(108.2m, dien.GetProperty("sanLuongTheoGoiY").GetDecimal());
            Assert.False(dien.GetProperty("coTheQuayVong").GetBoolean());
        }

        [Fact]
        public async Task KiemTraBatThuong_DecreaseWithTenfoldHint_SuggestsTimesTen()
        {
            var (w, client) = await SeedPeriodAsync(dienCu: 520m, nuocCu: 50m);

            var res = await client.GetAsync(Url(w, "53.4426"));

            var dien = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dien");
            Assert.Equal("Chan", dien.GetProperty("mucDo").GetString());
            Assert.Equal(534.426m, dien.GetProperty("chiSoGoiY").GetDecimal());
            Assert.Equal(14.426m, dien.GetProperty("sanLuongTheoGoiY").GetDecimal());
        }

        [Fact]
        public async Task KiemTraBatThuong_OverCeilingWithoutHistory_IsYellow_AndNormalReadingIsNull()
        {
            var (w, client) = await SeedPeriodAsync(dienCu: 100m, nuocCu: 50m);

            var yellow = await (await client.GetAsync(Url(w, "700"))).Content.ReadFromJsonAsync<JsonElement>();
            var normal = await (await client.GetAsync(Url(w, "120", "55"))).Content.ReadFromJsonAsync<JsonElement>();
            var zero = await (await client.GetAsync(Url(w, "100"))).Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("TieuThuBatThuong", yellow.GetProperty("dien").GetProperty("maLyDo").GetString());
            Assert.Equal("Vang", yellow.GetProperty("dien").GetProperty("mucDo").GetString());
            Assert.Equal(JsonValueKind.Null, normal.GetProperty("dien").ValueKind);
            Assert.Equal(JsonValueKind.Null, normal.GetProperty("nuoc").ValueKind);
            // Tiêu thụ bằng 0 khi chưa có lịch sử: server không cảnh báo (nhắc xem lại là việc của giao diện).
            Assert.Equal(JsonValueKind.Null, zero.GetProperty("dien").ValueKind);
        }
    }
}
