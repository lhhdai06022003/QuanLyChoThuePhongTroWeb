using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
    public class MeterImageStaffEndpointTests
    {
        private readonly CustomWebApplicationFactory _factory;
        private static readonly byte[] ValidJpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };

        public MeterImageStaffEndpointTests(CustomWebApplicationFactory factory)
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

        private static async Task AssignStaffToBranchAsync(ApplicationDbContext db, int staffUserId, int branchId)
        {
            var adminUser = await db.NguoiDungs.FirstOrDefaultAsync(u => u.Role == Role.Admin);
            int assignerId = adminUser?.NguoiDungId ?? staffUserId;
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staffUserId,
                ChiNhanhId = branchId,
                IsActive = true,
                NguoiPhanCongId = assignerId,
                NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        private async Task<(HttpClient client, string csrfToken)> LoginAsync(string username, string password)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
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

            var getPage = await client.GetAsync("/QuanLyNhaTro/YeuCauSuCo");
            var pageHtml = await getPage.Content.ReadAsStringAsync();
            var authCsrf = ExtractAntiForgeryToken(pageHtml);
            return (client, authCsrf);
        }

        private async Task<(HttpClient client, string csrfToken)> LoginTenantAsync(string username, string password)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
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

            return (client, csrf);
        }

        [Fact]
        public async Task AnhChiSo_Get_Anonymous_Redirects()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/DienNuoc/AnhChiSo?phongTroId=1&thang=9&nam=2026");
            Assert.True(response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AnhChiSo_Get_Tenant_Blocked()
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
            Assert.True(tenantUser.Success);

            try
            {
                var (client, _) = await LoginTenantAsync(username, password);
                var response = await client.GetAsync("/DienNuoc/AnhChiSo?phongTroId=1&thang=9&nam=2026");
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
        public async Task AnhChiSo_Get_StaffOtherBranch_400_NoData()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch1 = new ChiNhanh { TenChiNhanh = $"CN1_{suffix}", MaChiNhanh = $"C1{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            var branch2 = new ChiNhanh { TenChiNhanh = $"CN2_{suffix}", MaChiNhanh = $"C2{suffix}", DiaChi = "B", SoDienThoai = "0900000002", MoTa = "M" };
            db.ChiNhanhs.AddRange(branch1, branch2);
            await db.SaveChangesAsync();

            var roomInBranch2 = new PhongTro { ChiNhanhId = branch2.ChiNhanhId, SoPhong = $"P2_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(roomInBranch2);
            await db.SaveChangesAsync();

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch1.ChiNhanhId);

            try
            {
                var (client, _) = await LoginAsync(username, password);
                var response = await client.GetAsync($"/DienNuoc/AnhChiSo?phongTroId={roomInBranch2.PhongTroId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.False(json.GetProperty("success").GetBoolean());
                Assert.False(json.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null && data.ValueKind != JsonValueKind.Undefined);
            }
            finally
            {
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(roomInBranch2);
                db.ChiNhanhs.RemoveRange(branch1, branch2);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task AnhChiSo_Get_AssignedStaff_ReturnsImagesAndHints()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"t_{suffix}@test.com" };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract);

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
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

            var imgDocDuoc = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img1.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                GiaTriAIGoiY = 150.5m,
                DoTinCay = 0.95,
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = staffUser.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(imgDocDuoc);
            await db.SaveChangesAsync();

            try
            {
                var (client, _) = await LoginAsync(username, password);
                var response = await client.GetAsync($"/DienNuoc/AnhChiSo?phongTroId={room.PhongTroId}&thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.True(json.GetProperty("success").GetBoolean());
                var data = json.GetProperty("data");
                var anhDien = data.GetProperty("anhDien");
                Assert.True(anhDien.GetArrayLength() > 0);

                var firstImg = anhDien[0];
                Assert.Equal("95%", firstImg.GetProperty("doTinCay").GetString());
                Assert.True(firstImg.GetProperty("coTheXacNhan").GetBoolean());
                Assert.False(firstImg.GetProperty("canLyDo").GetBoolean());
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId));
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.HopDongs.Remove(contract);
                db.NguoiThues.Remove(tenant);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Theory]
        [InlineData("/DienNuoc/AnhChiSo/TaiLen", "multipart")]
        [InlineData("/DienNuoc/AnhChiSo/1/ThuLai", "empty")]
        [InlineData("/DienNuoc/AnhChiSo/1/XacNhan", "json")]
        [InlineData("/DienNuoc/AnhChiSo/1/SuaSo", "json")]
        public async Task AnhChiSo_Post_WithoutAntiforgery_400(string route, string contentType)
        {
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

                var request = new HttpRequestMessage(HttpMethod.Post, route);
                // Do NOT add RequestVerificationToken header

                if (contentType == "multipart")
                {
                    request.Content = new MultipartFormDataContent();
                }
                else if (contentType == "json")
                {
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                }
                else
                {
                    request.Content = new StringContent("");
                }

                var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally
            {
                db.NguoiDungs.Remove(staffUser);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiLen_ValidJpeg_CreatesImage_200()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"t_{suffix}@test.com" };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract);

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(curMonth.ToString()), "thang");
                multipart.Add(new StringContent(curYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo"); // Dien

                var fileContent = new ByteArrayContent(ValidJpegBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/AnhChiSo/TaiLen");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var createdImg = await verifyDb.AnhChiSoDongHos
                    .Include(x => x.DichVuDienNuocCuaPhong)
                    .FirstOrDefaultAsync(x => x.DichVuDienNuocCuaPhong.PhongTroId == room.PhongTroId && x.DichVuDienNuocCuaPhong.Thang == curMonth && x.DichVuDienNuocCuaPhong.Nam == curYear);
                Assert.NotNull(createdImg);
                Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, createdImg.TrangThaiXuLy);
            }
            finally
            {
                var p = await db.DichVuDienNuocCuaPhongs.FirstOrDefaultAsync(x => x.PhongTroId == room.PhongTroId && x.Thang == curMonth && x.Nam == curYear);
                if (p != null)
                {
                    db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == p.DichVuDienNuocCuaPhongId));
                    db.DichVuDienNuocCuaPhongs.Remove(p);
                }
                db.HopDongs.Remove(contract);
                db.NguoiThues.Remove(tenant);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiLen_GifOrMismatch_400_NoImage()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent("9"), "thang");
                multipart.Add(new StringContent("2026"), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                // GIF bytes
                var gifBytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00 };
                var fileContent = new ByteArrayContent(gifBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/gif");
                multipart.Add(fileContent, "file", "meter.gif");

                var req = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/AnhChiSo/TaiLen");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var imgCount = await verifyDb.AnhChiSoDongHos
                    .CountAsync(x => x.DichVuDienNuocCuaPhong.PhongTroId == room.PhongTroId);
                Assert.Equal(0, imgCount);
            }
            finally
            {
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiLen_Over6MB_Rejected()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent("9"), "thang");
                multipart.Add(new StringContent("2026"), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                // 6.5 MB file
                var bigBytes = new byte[6500000];
                Array.Copy(ValidJpegBytes, bigBytes, ValidJpegBytes.Length);

                var fileContent = new ByteArrayContent(bigBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/AnhChiSo/TaiLen");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            }
            finally
            {
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task TaiLen_FuturePeriod_400_RuleMessage()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
                var (futMonth, futYear) = curMonth == 12 ? (1, curYear + 1) : (curMonth + 1, curYear);

                var multipart = new MultipartFormDataContent();
                multipart.Add(new StringContent(room.PhongTroId.ToString()), "phongTroId");
                multipart.Add(new StringContent(futMonth.ToString()), "thang");
                multipart.Add(new StringContent(futYear.ToString()), "nam");
                multipart.Add(new StringContent("0"), "loaiDongHo");

                var fileContent = new ByteArrayContent(ValidJpegBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                multipart.Add(fileContent, "file", "meter.jpg");

                var req = new HttpRequestMessage(HttpMethod.Post, "/DienNuoc/AnhChiSo/TaiLen");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = multipart;

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(MeterUploadRules.ReasonRule1Future, json.GetProperty("message").GetString());
            }
            finally
            {
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task XacNhan_KhongDocDuoc_WithoutReason_400_And_WithReason_200_UpdatesPeriod()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
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

            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc,
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = staffUser.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                // 1. Without reason -> 400
                var failReq = new HttpRequestMessage(HttpMethod.Post, $"/DienNuoc/AnhChiSo/{img.AnhChiSoDongHoId}/XacNhan");
                failReq.Headers.Add("RequestVerificationToken", csrf);
                failReq.Content = JsonContent.Create(new { giaTriXacNhan = 150m, ghiChu = (string?)null });

                var failRes = await client.SendAsync(failReq);
                Assert.Equal(HttpStatusCode.BadRequest, failRes.StatusCode);

                // 2. With reason -> 200
                var okReq = new HttpRequestMessage(HttpMethod.Post, $"/DienNuoc/AnhChiSo/{img.AnhChiSoDongHoId}/XacNhan");
                okReq.Headers.Add("RequestVerificationToken", csrf);
                okReq.Content = JsonContent.Create(new { giaTriXacNhan = 150m, ghiChu = "Đọc mờ nên nhân viên kiểm tra lại" });

                var okRes = await client.SendAsync(okReq);
                Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedPeriod = await verifyDb.DichVuDienNuocCuaPhongs
                    .FirstOrDefaultAsync(x => x.PhongTroId == room.PhongTroId && x.Thang == curMonth && x.Nam == curYear);
                Assert.NotNull(updatedPeriod);
                Assert.Equal(150m, updatedPeriod.ChiSoDienMoi);
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId));
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task XacNhan_OnApprovedPeriod_UpdatesNguoiDuyet()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            var oldDate = DateTime.UtcNow.AddDays(-2);
            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = curMonth,
                Nam = curYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 110m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 55m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                NguoiDuyetId = staffUser.NguoiDungId,
                NgayDuyet = oldDate
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                GiaTriAIGoiY = 120m,
                DoTinCay = 0.9,
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = staffUser.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                var req = new HttpRequestMessage(HttpMethod.Post, $"/DienNuoc/AnhChiSo/{img.AnhChiSoDongHoId}/XacNhan");
                req.Headers.Add("RequestVerificationToken", csrf);
                req.Content = JsonContent.Create(new { giaTriXacNhan = 120m });

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedPeriod = await verifyDb.DichVuDienNuocCuaPhongs
                    .FirstOrDefaultAsync(x => x.PhongTroId == room.PhongTroId && x.Thang == curMonth && x.Nam == curYear);
                Assert.NotNull(updatedPeriod);
                Assert.Equal(staffUser.NguoiDungId, updatedPeriod.NguoiDuyetId);
                Assert.True(updatedPeriod.NgayDuyet > oldDate);
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId));
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task SuaSo_WithoutReason_400_And_Valid_200()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = curMonth,
                Nam = curYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 50m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 150m,
                NguoiXacNhanId = staffUser.NguoiDungId,
                NgayXacNhan = DateTime.UtcNow,
                DuocChonLamChiSoChinhThuc = true,
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = staffUser.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                // 1. Without reason -> 400
                var failReq = new HttpRequestMessage(HttpMethod.Post, $"/DienNuoc/AnhChiSo/{img.AnhChiSoDongHoId}/SuaSo");
                failReq.Headers.Add("RequestVerificationToken", csrf);
                failReq.Content = JsonContent.Create(new { giaTriMoi = 155m, lyDo = "" });

                var failRes = await client.SendAsync(failReq);
                Assert.Equal(HttpStatusCode.BadRequest, failRes.StatusCode);

                // 2. With reason -> 200
                var okReq = new HttpRequestMessage(HttpMethod.Post, $"/DienNuoc/AnhChiSo/{img.AnhChiSoDongHoId}/SuaSo");
                okReq.Headers.Add("RequestVerificationToken", csrf);
                okReq.Content = JsonContent.Create(new { giaTriMoi = 155m, lyDo = "Đính chính chỉ số theo biên bản" });

                var okRes = await client.SendAsync(okReq);
                Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedImg = await verifyDb.AnhChiSoDongHos.FindAsync(img.AnhChiSoDongHoId);
                Assert.NotNull(updatedImg);
                Assert.Equal(155m, updatedImg.GiaTriXacNhan);
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId));
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task ThuLai_KhongDocDuoc_200()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
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

            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.KhongDocDuoc,
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = staffUser.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();

            try
            {
                var (client, csrf) = await LoginAsync(username, password);

                var req = new HttpRequestMessage(HttpMethod.Post, $"/DienNuoc/AnhChiSo/{img.AnhChiSoDongHoId}/ThuLai");
                req.Headers.Add("RequestVerificationToken", csrf);

                var res = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                using var verifyScope = _factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedImg = await verifyDb.AnhChiSoDongHos.FindAsync(img.AnhChiSoDongHoId);
                Assert.NotNull(updatedImg);
                Assert.Equal(TrangThaiXuLyAnhChiSo.DocDuoc, updatedImg.TrangThaiXuLy);
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId));
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task GetDanhSachPhongs_IncludesImageSummary()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", SoDienThoai = "0911111111", CCCD = $"0791{suffix[..4]}", Email = $"t_{suffix}@test.com" };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract);

            var staffResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = curMonth,
                Nam = curYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 50m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                GiaTriXacNhan = 150m,
                NguoiXacNhanId = staffUser.NguoiDungId,
                NgayXacNhan = DateTime.UtcNow,
                DuocChonLamChiSoChinhThuc = true,
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = staffUser.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();

            try
            {
                var (client, _) = await LoginAsync(username, password);

                var res = await client.GetAsync($"/DienNuoc/GetDanhSachPhongs?chiNhanhId={branch.ChiNhanhId}&thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var list = await res.Content.ReadFromJsonAsync<List<DienNuocPhongRes>>();
                Assert.NotNull(list);
                var roomItem = list.FirstOrDefault(x => x.PhongTroId == room.PhongTroId);
                Assert.NotNull(roomItem);
                Assert.Equal(1, roomItem.SoAnhDien);
                Assert.Equal(AppTrangThaiAnhChiSo.DaXacNhan, roomItem.TrangThaiAnhDien);
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId));
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.HopDongs.Remove(contract);
                db.NguoiThues.Remove(tenant);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task AnhChiSo_Get_LockedPeriod_ReturnsKyBiKhoaTrue()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var username = $"staff_lock_{suffix}";
            const string password = "Password123!";

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "M" };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000m, DienTich = 20, MoTa = "M" };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue { HoVaTen = $"T_{suffix}", SoDienThoai = "0900000002", CCCD = $"079{suffix[..5]}", Email = $"t_{suffix}@test.com" };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var (curMonth, curYear) = MeterPeriodPolicy.CurrentVn(DateTime.UtcNow);
            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = new DateTime(curYear, curMonth, 1, 0, 0, 0, DateTimeKind.Utc),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong,
                TienThuePhong = 1000m,
                TienCocPhong = 1000m
            };
            db.HopDongs.Add(contract);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = curMonth,
                Nam = curYear,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150m,
                ChiSoNuocCu = 50m,
                ChiSoNuocMoi = 60m,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var invoice = new HoaDon
            {
                MaHoaDon = $"HDN_{suffix}",
                HopDongId = contract.HopDongId,
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                Thang = curMonth,
                Nam = curYear,
                TongTien = 500000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot
            };
            db.HoaDons.Add(invoice);

            var userRes = await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = password, Role = AppRole.NhanVien });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);
            await AssignStaffToBranchAsync(db, staffUser.NguoiDungId, branch.ChiNhanhId);

            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = LoaiDongHo.Dien,
                Url = "https://example.test/img.jpg",
                TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DocDuoc,
                GiaTriAIGoiY = 150m,
                DoTinCay = 0.9,
                NgayGui = DateTime.UtcNow,
                NguoiGuiId = staffUser.NguoiDungId
            };
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();

            try
            {
                var (client, _) = await LoginAsync(username, password);

                var res = await client.GetAsync($"/DienNuoc/AnhChiSo?phongTroId={room.PhongTroId}&thang={curMonth}&nam={curYear}");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                Assert.True(json.GetProperty("success").GetBoolean());
                var data = json.GetProperty("data");

                // Check field kyBiKhoa and lyDoKhoa
                Assert.True(data.GetProperty("kyBiKhoa").GetBoolean());
                Assert.Equal(MeterUploadRules.ReasonRule4LockedInvoice, data.GetProperty("lyDoKhoa").GetString());

                // All images must have coTheThuLai, coTheXacNhan, coTheSuaSo == false
                var anhDienList = data.GetProperty("anhDien").EnumerateArray().ToList();
                Assert.NotEmpty(anhDienList);
                foreach (var item in anhDienList)
                {
                    Assert.False(item.GetProperty("coTheThuLai").GetBoolean());
                    Assert.False(item.GetProperty("coTheXacNhan").GetBoolean());
                    Assert.False(item.GetProperty("coTheSuaSo").GetBoolean());
                }
            }
            finally
            {
                db.AnhChiSoDongHos.RemoveRange(db.AnhChiSoDongHos.Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId));
                db.HoaDons.Remove(invoice);
                db.DichVuDienNuocCuaPhongs.Remove(period);
                db.HopDongs.Remove(contract);
                db.NguoiThues.Remove(tenant);
                db.NhanVienChiNhanhs.RemoveRange(db.NhanVienChiNhanhs.Where(x => x.NguoiDungId == staffUser.NguoiDungId));
                db.NguoiDungs.Remove(staffUser);
                db.PhongTros.Remove(room);
                db.ChiNhanhs.Remove(branch);
                await db.SaveChangesAsync();
            }
        }
    }
}
