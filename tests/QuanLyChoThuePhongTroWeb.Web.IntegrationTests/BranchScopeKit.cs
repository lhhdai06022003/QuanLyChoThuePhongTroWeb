using System;
using System.Collections.Generic;
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
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Dữ liệu hai chi nhánh dùng cho test phân quyền: nhân viên chỉ được phân công chi nhánh A.
    public sealed record BranchScopeWorld(
        int BranchA, int BranchB,
        int RoomA, int RoomB,
        int TenantA, int TenantB, int TenantFree,
        int ContractA, int ContractB, int MemberB,
        string BranchNameB, string RoomNumberB, string ContractCodeB,
        string Staff, string Admin);

    public static class BranchScopeKit
    {
        public const string Password = "Password123!";

        public static async Task<BranchScopeWorld> SeedAsync(CustomWebApplicationFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var branchA = new ChiNhanh { TenChiNhanh = $"CN_A_{s}", MaChiNhanh = $"A{s[..5]}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "A" };
            var branchB = new ChiNhanh { TenChiNhanh = $"CN_B_{s}", MaChiNhanh = $"B{s[..5]}", DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            db.ChiNhanhs.AddRange(branchA, branchB);
            await db.SaveChangesAsync();

            var roomA = new PhongTro { ChiNhanhId = branchA.ChiNhanhId, SoPhong = $"PA{s[..5]}", GiaThue = 2000000m, DienTich = 20, SoNguoiToiDa = 3, MoTa = "A", TrangThai = TrangThaiPhong.DaThue };
            var roomB = new PhongTro { ChiNhanhId = branchB.ChiNhanhId, SoPhong = $"PB{s[..5]}", GiaThue = 2500000m, DienTich = 22, SoNguoiToiDa = 3, MoTa = "B", TrangThai = TrangThaiPhong.DaThue };
            var tenantA = new NguoiThue { HoVaTen = $"Khach A {s}", SoDienThoai = Phone(), CCCD = $"079{s[..6]}1", Email = $"a_{s}@test.com" };
            var tenantB = new NguoiThue { HoVaTen = $"Khach B {s}", SoDienThoai = Phone(), CCCD = $"079{s[..6]}2", Email = $"b_{s}@test.com" };
            var tenantFree = new NguoiThue { HoVaTen = $"Khach tu do {s}", SoDienThoai = Phone(), CCCD = $"079{s[..6]}3", Email = $"f_{s}@test.com" };
            db.PhongTros.AddRange(roomA, roomB);
            db.NguoiThues.AddRange(tenantA, tenantB, tenantFree);
            await db.SaveChangesAsync();

            var contractA = NewContract($"HDA_{s}", roomA.PhongTroId, tenantA.NguoiThueId);
            var contractB = NewContract($"HDB_{s}", roomB.PhongTroId, tenantB.NguoiThueId);
            db.HopDongs.AddRange(contractA, contractB);
            await db.SaveChangesAsync();

            var memberB = new ChiTietThanhVienHopDong { HopDongId = contractB.HopDongId, NguoiThueId = tenantB.NguoiThueId, NgayVao = DateTime.UtcNow.AddMonths(-2) };
            db.ChiTietThanhVienHopDongs.Add(memberB);
            await db.SaveChangesAsync();

            var world = new BranchScopeWorld(
                branchA.ChiNhanhId, branchB.ChiNhanhId,
                roomA.PhongTroId, roomB.PhongTroId,
                tenantA.NguoiThueId, tenantB.NguoiThueId, tenantFree.NguoiThueId,
                contractA.HopDongId, contractB.HopDongId, memberB.ChiTietThanhVienHopDongId,
                branchB.TenChiNhanh, roomB.SoPhong, contractB.MaHopDong,
                $"nv_{s}", $"ad_{s}");

            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = world.Staff, MatKhau = Password, Role = AppRole.NhanVien })).Success);
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = world.Admin, MatKhau = Password, Role = AppRole.Admin })).Success);

            var staff = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == world.Staff);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId,
                ChiNhanhId = branchA.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = staff.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return world;
        }

        private static string Phone() => "09" + Random.Shared.Next(10000000, 99999999);

        private static HopDong NewContract(string code, int roomId, int tenantId) => new()
        {
            MaHopDong = code,
            PhongTroId = roomId,
            NguoiThueId = tenantId,
            ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-2),
            ThoiDiemKetThuc = DateTime.UtcNow.AddMonths(10),
            TienCocPhong = 2000000m,
            TienThuePhong = 2000000m,
            TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
        };

        public static HttpClient NewClient(WebApplicationFactory<Program> factory) =>
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        public static string Token(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success) match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // Đăng nhập rồi lấy token antiforgery từ một trang quản lý đã xác thực.
        public static async Task<(HttpClient Client, string Token)> LoginAsync(WebApplicationFactory<Program> factory, string username, string page = "/PhongTros/QuanLyPhongTro")
        {
            var client = NewClient(factory);
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
            var token = Token(await authed.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrEmpty(token), "Trang không có token antiforgery: " + page);
            return (client, token);
        }

        public static Task<HttpResponseMessage> PostFormAsync(HttpClient client, string token, string url, IDictionary<string, string> form)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
            req.Headers.Add("RequestVerificationToken", token);
            return client.SendAsync(req);
        }

        public static Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string token, HttpMethod method, string url, object? body)
        {
            var req = new HttpRequestMessage(method, url);
            if (body != null)
            {
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }
            req.Headers.Add("RequestVerificationToken", token);
            return client.SendAsync(req);
        }

        public static async Task<JsonElement> JsonAsync(HttpResponseMessage res)
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }

        public static async Task<T> ReadAsync<T>(CustomWebApplicationFactory factory, Func<ApplicationDbContext, Task<T>> read)
        {
            using var scope = factory.Services.CreateScope();
            return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }
    }
}
