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
    public class InvoiceLifecycleTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public InvoiceLifecycleTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GuiDuyet_AssignedStaff_Returns200()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (branch, room, tenant, contract, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.Nhap);

            var staffUsername = $"staff_{suffix}";
            const string staffPassword = "StaffPassword123!";
            await CreateAndAssignStaffAsync(staffUsername, staffPassword, branch.ChiNhanhId);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            var token = await LoginAsync(client, staffUsername, staffPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/GuiDuyet/{invoice.HoaDonId}");
            req.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(content);
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, json.RootElement.GetProperty("trangThaiPhatHanh").GetInt32());
        }

        [Fact]
        public async Task Chot_Staff_Returns400_Admin_Returns200()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (branch, room, tenant, contract, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.ChoDuyet);

            var staffUsername = $"staff_{suffix}";
            const string staffPassword = "StaffPassword123!";
            await CreateAndAssignStaffAsync(staffUsername, staffPassword, branch.ChiNhanhId);

            var staffClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            var staffToken = await LoginAsync(staffClient, staffUsername, staffPassword);

            // 1. Staff cố tình chốt -> 400
            var staffReq = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/Chot/{invoice.HoaDonId}");
            staffReq.Headers.Add("RequestVerificationToken", staffToken);
            var staffRes = await staffClient.SendAsync(staffReq);
            Assert.Equal(HttpStatusCode.BadRequest, staffRes.StatusCode);

            // 2. Admin chốt -> 200
            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            var adminToken = await LoginAsync(adminClient, adminUsername, adminPassword);

            var adminReq = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/Chot/{invoice.HoaDonId}");
            adminReq.Headers.Add("RequestVerificationToken", adminToken);
            var adminRes = await adminClient.SendAsync(adminReq);
            Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);

            var adminContent = await adminRes.Content.ReadAsStringAsync();
            using var adminJson = JsonDocument.Parse(adminContent);
            Assert.True(adminJson.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.DaChot, adminJson.RootElement.GetProperty("trangThaiPhatHanh").GetInt32());
        }

        [Fact]
        public async Task TraLai_WithoutReason_Returns400()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (branch, room, tenant, contract, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.ChoDuyet);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            var adminToken = await LoginAsync(adminClient, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/TraLai/{invoice.HoaDonId}")
            {
                Content = JsonContent.Create(new { lyDo = "   " })
            };
            req.Headers.Add("RequestVerificationToken", adminToken);

            var response = await adminClient.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(content);
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("lý do", json.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Post_WithoutAntiforgery_IsRejected()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (branch, room, tenant, contract, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.Nhap);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            await LoginAsync(adminClient, adminUsername, adminPassword);

            // Gửi POST không có token header
            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/GuiDuyet/{invoice.HoaDonId}");
            var response = await adminClient.SendAsync(req);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private async Task<(ChiNhanh, PhongTro, NguoiThue, HopDong, HoaDon)> SeedInvoiceHierarchyAsync(string suffix, TrangThaiPhatHanhHoaDon status)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_{suffix}",
                MaChiNhanh = $"C_{suffix}",
                DiaChi = "Dia chi",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta"
            };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"P_{suffix}",
                GiaThue = 2000000m,
                DienTich = 20,
                MoTa = "Mo ta"
            };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue
            {
                HoVaTen = $"Khach_{suffix}",
                SoDienThoai = $"091{suffix[..6]}",
                Email = $"tenant_{suffix}@example.com",
                CCCD = $"079{suffix[..6]}"
            };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1),
                TienCocPhong = 2000000m,
                TienThuePhong = 2000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            db.HopDongs.Add(contract);
            await db.SaveChangesAsync();

            var invoice = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD-{suffix}",
                Thang = 9,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = status,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayTao = DateTime.UtcNow,
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new ChiTietHoaDon { TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1, DichVuId = 1 }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            return (branch, room, tenant, contract, invoice);
        }

        private async Task CreateAndAssignStaffAsync(string username, string password, int branchId)
        {
            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var res = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.NhanVien
            });
            Assert.True(res.Success);

            var staff = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);
            var admin = await db.NguoiDungs.FirstOrDefaultAsync(u => u.Role == Role.Admin);
            int assignerId = admin?.NguoiDungId ?? staff.NguoiDungId;

            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId,
                ChiNhanhId = branchId,
                IsActive = true,
                NguoiPhanCongId = assignerId,
                NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        private async Task CreateAdminAsync(string username, string password)
        {
            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var res = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.Admin
            });
            Assert.True(res.Success);
        }

        [Fact]
        public async Task Tenant_CannotViewDraftDetail_OrVietQR_ByGuessingId()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (branch, room, tenant, contract, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            var tenantUsername = $"tnt_{suffix}";
            const string tenantPassword = "TenantPassword123!";
            await CreateTenantUserAsync(tenantUsername, tenantPassword, tenant.NguoiThueId);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            await LoginTenantAsync(client, tenantUsername, tenantPassword);

            // 1. Tenant cố tình xem chi tiết hóa đơn DaChot -> 404 NotFound
            var detailRes = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet/{invoice.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, detailRes.StatusCode);

            // 2. Tenant cố tình lấy mã QR cho hóa đơn DaChot -> 400 BadRequest
            var qrRes = await client.GetAsync($"/KhachThue/HoaDon/GetVietQR?hoaDonId={invoice.HoaDonId}");
            Assert.Equal(HttpStatusCode.BadRequest, qrRes.StatusCode);
        }

        private async Task CreateTenantUserAsync(string username, string password, int nguoiThueId)
        {
            using var scope = _factory.Services.CreateScope();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var res = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.KhachThue,
                NguoiThueId = nguoiThueId
            });
            Assert.True(res.Success);
        }

        private static async Task LoginTenantAsync(HttpClient client, string username, string password)
        {
            var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
            var antiForgeryToken = ExtractAntiForgeryToken(loginHtml);

            var loginContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            });

            var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
            Assert.Contains("/KhachThue", loginResponse.Headers.Location?.ToString() ?? "");
        }

        private static async Task<string> LoginAsync(HttpClient client, string username, string password)
        {
            var getLoginResponse = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var loginHtml = await getLoginResponse.Content.ReadAsStringAsync();
            var antiForgeryToken = ExtractAntiForgeryToken(loginHtml);

            var loginContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", password)
            });

            var loginResponse = await client.PostAsync("/QuanLyNhaTro/DangNhap", loginContent);
            Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

            var getSuCo = await client.GetAsync("/QuanLyNhaTro/YeuCauSuCo");
            var suCoHtml = await getSuCo.Content.ReadAsStringAsync();
            return ExtractAntiForgeryToken(suCoHtml);
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
    }
}
