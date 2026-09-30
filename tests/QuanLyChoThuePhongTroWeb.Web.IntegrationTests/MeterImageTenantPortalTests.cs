using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Models.ChiSoDienNuoc;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class MeterImageTenantPortalTests
    {
        private readonly CustomWebApplicationFactory _factory;
        private static readonly byte[] ValidJpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };

        public MeterImageTenantPortalTests(CustomWebApplicationFactory factory)
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

        private async Task<(HttpClient client, string csrfToken)> LoginAsync(string username, string password, WebApplicationFactory<Program>? factory = null)
        {
            var f = factory ?? _factory;
            var client = f.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var getLogin = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var loginHtml = await getLogin.Content.ReadAsStringAsync();
            var csrf = ExtractAntiForgeryToken(loginHtml);

            var loginContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", csrf),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            });

            var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

            // Fetch a page to extract the authenticated CSRF token
            var getPage = await client.GetAsync("/KhachThue/ChiSoDienNuoc");
            var pageHtml = await getPage.Content.ReadAsStringAsync();
            var authCsrf = ExtractAntiForgeryToken(pageHtml);
            if (string.IsNullOrWhiteSpace(authCsrf))
            {
                authCsrf = csrf;
            }
            return (client, authCsrf);
        }

        [Fact]
        public async Task Page_Anonymous_Redirects_And_Staff_Blocked()
        {
            var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var anonRes = await anonClient.GetAsync("/KhachThue/ChiSoDienNuoc");
            Assert.True(anonRes.StatusCode == HttpStatusCode.Redirect || anonRes.StatusCode == HttpStatusCode.Unauthorized);

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            try
            {
                var (client, _) = await LoginAsync(username, password);
                var staffRes = await client.GetAsync("/KhachThue/ChiSoDienNuoc");
                Assert.NotEqual(HttpStatusCode.OK, staffRes.StatusCode);
            }
            finally
            {
                db.NguoiDungs.Remove(staffUser);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task Page_Tenant_200_ContainsNoNumberInput()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"tenant_{suffix}";
            const string password = "TenantPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var tenantUser = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.KhachThue
            });
            var u = await db.NguoiDungs.FirstAsync(x => x.TenDangNhap == username);

            try
            {
                var (client, _) = await LoginAsync(username, password);
                var response = await client.GetAsync("/KhachThue/ChiSoDienNuoc");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var html = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("type=\"number\"", html);
                Assert.Contains("capture=\"environment\"", html);
            }
            finally
            {
                db.NguoiDungs.Remove(u);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task Phong_TenantA_SeesOnlyP1()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            var room2 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.AddRange(room1, room2);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            var tenantB = new NguoiThue { HoVaTen = $"TB_{suffix}", SoDienThoai = "0922222222", CCCD = $"0792{suffix[..4]}", Email = $"tb_{suffix}@test.com" };
            db.NguoiThues.AddRange(tenantA, tenantB);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            var contract2 = new HopDong
            {
                MaHopDong = $"HD2_{suffix}",
                PhongTroId = room2.PhongTroId,
                NguoiThueId = tenantB.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.AddRange(contract1, contract2);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, _) = await LoginAsync(usernameA, password);
                var res = await client.GetAsync($"/KhachThue/ChiSoDienNuoc/Phong?thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                Assert.True(json.GetProperty("success").GetBoolean());
                var data = json.GetProperty("data");
                Assert.Equal(1, data.GetArrayLength());
                Assert.Equal(room1.PhongTroId, data[0].GetProperty("phongTroId").GetInt32());
            }
            finally
            {
                db.HopDongs.RemoveRange(contract1, contract2);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.RemoveRange(tenantA, tenantB);
                db.PhongTros.RemoveRange(room1, room2);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task Ky_TenantA_P2_400_NoData()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room2 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room2);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);

            try
            {
                var (client, _) = await LoginAsync(usernameA, password);
                var res = await client.GetAsync($"/KhachThue/ChiSoDienNuoc/Ky?phongTroId={room2.PhongTroId}&thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                Assert.False(json.GetProperty("success").GetBoolean());
            }
            finally
            {
                db.NguoiDungs.Remove(userA);
                db.PhongTros.Remove(room2);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task Ky_Member_P1_200()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameM = $"member_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            var memberM = new NguoiThue { HoVaTen = $"TM_{suffix}", SoDienThoai = "0933333333", CCCD = $"0793{suffix[..4]}", Email = $"tm_{suffix}@test.com" };
            db.NguoiThues.AddRange(tenantA, memberM);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);
            await db.SaveChangesAsync();

            var tv = new ChiTietThanhVienHopDong
            {
                HopDongId = contract1.HopDongId,
                NguoiThueId = memberM.NguoiThueId
            };
            db.ChiTietThanhVienHopDongs.Add(tv);
            await db.SaveChangesAsync();

            var userMRes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameM, MatKhau = password, Role = AppRole.KhachThue });
            var userM = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameM);
            userM.NguoiThueId = memberM.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, _) = await LoginAsync(usernameM, password);
                var res = await client.GetAsync($"/KhachThue/ChiSoDienNuoc/Ky?phongTroId={room1.PhongTroId}&thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                Assert.True(json.GetProperty("success").GetBoolean());
            }
            finally
            {
                db.ChiTietThanhVienHopDongs.Remove(tv);
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userM);
                db.NguoiThues.RemoveRange(tenantA, memberM);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task Ky_Tenant_HidesStaffFields()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);
            await db.SaveChangesAsync();

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room1.PhongTroId,
                Thang = curMonth,
                Nam = curYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 100m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 50m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;

            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                GiaTriAIGoiY = 150m,
                DoTinCay = 0.95,
                ThongBaoLoi = "Secret error",
                GhiChuXacNhan = "Secret note",
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = userA.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();

            try
            {
                var (client, _) = await LoginAsync(usernameA, password);
                var res = await client.GetAsync($"/KhachThue/ChiSoDienNuoc/Ky?phongTroId={room1.PhongTroId}&thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var rawJson = await res.Content.ReadAsStringAsync();
                Assert.DoesNotContain("doTinCay", rawJson);
                Assert.DoesNotContain("thongBaoLoi", rawJson);
                Assert.DoesNotContain("ghiChuXacNhan", rawJson);
            }
            finally
            {
                db.AnhChiSoDongHos.Remove(img);
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task Ky_LabelsFollowSpec()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);
            await db.SaveChangesAsync();

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room1.PhongTroId,
                Thang = curMonth,
                Nam = curYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 100m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 50m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;

            // Seed images representing various statuses
            var imgMoi = new AnhChiSoDongHo { DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId, LoaiDongHo = LoaiDongHo.Dien, Url = "https://example.test/1.jpg", TrangThaiXuLy = TrangThaiXuLyAnhChiSo.MoiTaiLen, NgayGui = DateTime.UtcNow, NguoiGuiId = userA.NguoiDungId };
            var imgDoc = new AnhChiSoDongHo { DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId, LoaiDongHo = LoaiDongHo.Dien, Url = "https://example.test/2.jpg", TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc, GiaTriAIGoiY = 123.4m, NgayGui = DateTime.UtcNow.AddMinutes(1), NguoiGuiId = userA.NguoiDungId };
            var imgKoDoc = new AnhChiSoDongHo { DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId, LoaiDongHo = LoaiDongHo.Dien, Url = "https://example.test/3.jpg", TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc, NgayGui = DateTime.UtcNow.AddMinutes(2), NguoiGuiId = userA.NguoiDungId };
            var imgXacNhan = new AnhChiSoDongHo { DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId, LoaiDongHo = LoaiDongHo.Dien, Url = "https://example.test/4.jpg", TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan, GiaTriXacNhan = 130m, NguoiXacNhanId = userA.NguoiDungId, NgayXacNhan = DateTime.UtcNow, DuocChonLamChiSoChinhThuc = true, NgayGui = DateTime.UtcNow.AddMinutes(3), NguoiGuiId = userA.NguoiDungId };
            var imgThayThe = new AnhChiSoDongHo { DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId, LoaiDongHo = LoaiDongHo.Dien, Url = "https://example.test/5.jpg", TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaThayThe, NgayGui = DateTime.UtcNow.AddMinutes(4), NguoiGuiId = userA.NguoiDungId };

            db.AnhChiSoDongHos.AddRange(imgMoi, imgDoc, imgKoDoc, imgXacNhan, imgThayThe);
            await db.SaveChangesAsync();

            try
            {
                var (client, _) = await LoginAsync(usernameA, password);
                var res = await client.GetAsync($"/KhachThue/ChiSoDienNuoc/Ky?phongTroId={room1.PhongTroId}&thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                var images = json.GetProperty("data").GetProperty("anhDien").EnumerateArray().ToList();

                var map = images.ToDictionary(x => x.GetProperty("id").GetInt32(), x => x.GetProperty("nhanTrangThai").GetString());

                Assert.Equal("Đang nhận diện", map[imgMoi.AnhChiSoDongHoId]);
                Assert.Contains("AI gợi ý", map[imgDoc.AnhChiSoDongHoId]!);
                Assert.Equal("Không đọc được, vui lòng chụp lại", map[imgKoDoc.AnhChiSoDongHoId]);
                Assert.Contains("Đã xác nhận", map[imgXacNhan.AnhChiSoDongHoId]!);
                Assert.Equal("Đã được thay bằng ảnh khác", map[imgThayThe.AnhChiSoDongHoId]);
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(imgMoi, imgDoc, imgKoDoc, imgXacNhan, imgThayThe);
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_WithoutAntiforgery_400()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);

            try
            {
                var (client, _) = await LoginAsync(usernameA, password);
                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Content = new MultipartFormDataContent();

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            }
            finally
            {
                db.NguoiDungs.Remove(userA);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_Valid_200_ImageCreated()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(usernameA, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room1.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(curMonth.ToString()), "thang");
                multipart.Add(new StringContent(curYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                var fileContent = new ByteArrayContent(ValidJpegBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var img = await verifyDb.AnhChiSoDongHos
                    .Include(x => x.DichVuDienNuocCuaPhong)
                    .FirstOrDefaultAsync(x => x.DichVuDienNuocCuaPhong.PhongTroId == room1.PhongTroId && x.DichVuDienNuocCuaPhong.Thang == curMonth && x.DichVuDienNuocCuaPhong.Nam == curYear);
                Assert.NotNull(img);
                Assert.Equal(userA.NguoiDungId, img.NguoiGuiId);
            }
            finally
            {
                var p = await db.DichVuDienNuocCuaPhongs.FirstOrDefaultAsync(x => x.PhongTroId == room1.PhongTroId && x.Thang == curMonth && x.Nam == curYear);
                if (p != null)
                {
                    db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == p.DichVuDienNuocCuaPhongId));
                    db.DichVuDienNuocCuaPhongs.Remove(p);
                }
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_P2_400_NoImage()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room2 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room2);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);

            try
            {
                var (client, csrf) = await LoginAsync(usernameA, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room2.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(curMonth.ToString()), "thang");
                multipart.Add(new StringContent(curYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                var fileContent = new ByteArrayContent(ValidJpegBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var count = await verifyDb.AnhChiSoDongHos.CountAsync(x => x.DichVuDienNuocCuaPhong.PhongTroId == room2.PhongTroId);
                Assert.Equal(0, count);
            }
            finally
            {
                db.NguoiDungs.Remove(userA);
                db.PhongTros.Remove(room2);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_TwoMonthsAgo_400_Rule2()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var (twoMonthsAgoM, twoMonthsAgoY) = curMonth <= 2 ? (curMonth + 10, curYear - 1) : (curMonth - 2, curYear);

            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(twoMonthsAgoY, twoMonthsAgoM, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(usernameA, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room1.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(twoMonthsAgoM.ToString()), "thang");
                multipart.Add(new StringContent(twoMonthsAgoY.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                var fileContent = new ByteArrayContent(ValidJpegBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(MeterUploadRules.ReasonRule2TenantWindow, json.GetProperty("message").GetString());
            }
            finally
            {
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_PeriodApproved_400_Rule5()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room1.PhongTroId,
                Thang = curMonth,
                Nam = curYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 110m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 55m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(usernameA, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room1.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(curMonth.ToString()), "thang");
                multipart.Add(new StringContent(curYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                var fileContent = new ByteArrayContent(ValidJpegBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(MeterUploadRules.ReasonRule5PeriodApproved, json.GetProperty("message").GetString());
            }
            finally
            {
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_PrevPeriodNotApproved_400_Rule6()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var (prevMonth, prevYear) = curMonth == 1 ? (12, curYear - 1) : (curMonth - 1, curYear);

            // Contract active during previous period and current period
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(prevYear, prevMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);

            // Previous period is NOT approved (Nhap)
            var prevPeriod = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room1.PhongTroId,
                Thang = prevMonth,
                Nam = prevYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 100m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 50m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            db.DichVuDienNuocCuaPhongs.Add(prevPeriod);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(usernameA, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room1.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(curMonth.ToString()), "thang");
                multipart.Add(new StringContent(curYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                var fileContent = new ByteArrayContent(ValidJpegBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                var expectedReason = MeterUploadRules.FormatPreviousPeriodNotApprovedReason(prevMonth, prevYear, curMonth, curYear);
                Assert.Equal(expectedReason, json.GetProperty("message").GetString());
            }
            finally
            {
                db.DichVuDienNuocCuaPhongs.Remove(prevPeriod);
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_BadFile_400()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenantA_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(usernameA, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room1.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(curMonth.ToString()), "thang");
                multipart.Add(new StringContent(curYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                // Invalid text content disguised as jpg
                var badBytes = System.Text.Encoding.UTF8.GetBytes("This is not an image");
                var fileContent = new ByteArrayContent(badBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "fake.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            }
            finally
            {
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiAnh_UsesConfiguredMaxFileSize()
        {
            var customFactory = _factory.WithWebHostBuilder(b => b.UseSetting("MeterImageOptions:MaxFileSizeBytes", "1024"));

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var usernameA = $"tenant_cfg_{suffix}";
            const string password = "Password123!";

            using var scope = customFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P1_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room1);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue { HoVaTen = $"TA_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"ta_{suffix}@test.com" };
            db.NguoiThues.Add(tenantA);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);

            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract1);
            await db.SaveChangesAsync();

            var userARes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = usernameA, MatKhau = password, Role = AppRole.KhachThue });
            var userA = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == usernameA);
            userA.NguoiThueId = tenantA.NguoiThueId;
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(usernameA, password, customFactory);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room1.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(curMonth.ToString()), "thang");
                multipart.Add(new StringContent(curYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                var validJpeg2Kb = new byte[2048];
                Array.Copy(ValidJpegBytes, validJpeg2Kb, ValidJpegBytes.Length);

                var fileContent = new ByteArrayContent(validJpeg2Kb);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/ChiSoDienNuoc/TaiAnh");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                var message = json.GetProperty("message").GetString();
                Assert.NotNull(message);
                Assert.StartsWith("Ảnh vượt quá dung lượng cho phép", message);
            }
            finally
            {
                db.HopDongs.Remove(contract1);
                db.NguoiDungs.Remove(userA);
                db.NguoiThues.Remove(tenantA);
                db.PhongTros.Remove(room1);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }
    }
}
