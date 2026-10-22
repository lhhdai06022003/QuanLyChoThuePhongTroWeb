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
    public class InvoicePublicationTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public InvoicePublicationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(ChiNhanh branch, PhongTro room, NguoiThue tenant, HopDong contract, HoaDon invoice)> SeedInvoiceHierarchyAsync(
            string suffix, TrangThaiPhatHanhHoaDon status, bool withTenantAccount = false)
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

            if (withTenantAccount)
            {
                db.NguoiDungs.Add(new NguoiDung
                {
                    TenDangNhap = $"tnt_{suffix}",
                    MatKhauHash = "hash",
                    Role = Role.KhachThue,
                    NguoiThueId = tenant.NguoiThueId,
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

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

        private async Task<string> CreateAdminAsync(string username, string password)
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
            return username;
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

        private static string ExtractAntiForgeryToken(string html)
        {
            var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
            if (!match.Success)
            {
                match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
            }

            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static HttpClient NewClient(CustomWebApplicationFactory factory) =>
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        // ---------- CongBo: phân quyền và antiforgery ----------

        [Fact]
        public async Task CongBo_Unauthenticated_RedirectsToDangNhap()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            var client = NewClient(_factory);
            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("DangNhap", response.Headers.Location?.ToString() ?? "");
        }

        [Fact]
        public async Task CongBo_KhachThue_IsBlocked()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, tenant, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            var tenantUsername = $"tnt_{suffix}";
            const string tenantPassword = "TenantPassword123!";
            await CreateTenantUserAsync(tenantUsername, tenantPassword, tenant.NguoiThueId);

            var client = NewClient(_factory);
            await LoginTenantAsync(client, tenantUsername, tenantPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };
            var response = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("DangNhap", response.Headers.Location?.ToString() ?? "");
        }

        [Fact]
        public async Task CongBo_MissingAntiforgery_Returns400()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };
            // Không thêm header RequestVerificationToken

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("empty")]
        [InlineData("nonPositive")]
        [InlineData("tooMany")]
        public async Task CongBo_InvalidIds_Returns400_DbUnchanged(string kind)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            object? body = kind switch
            {
                "null" => new { hoaDonIds = (int[]?)null },
                "empty" => new { hoaDonIds = Array.Empty<int>() },
                "nonPositive" => new { hoaDonIds = new[] { invoice.HoaDonId, 0 } },
                "tooMany" => new { hoaDonIds = Enumerable.Range(int.MaxValue - 50, 51).ToArray() },
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo") { Content = JsonContent.Create(body) };
            req.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reloaded = await db.HoaDons.FirstAsync(h => h.HoaDonId == invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, reloaded.TrangThaiPhatHanh);
        }

        // ---------- CongBo: đường chạy thuận lợi và các biến thể ----------

        [Fact]
        public async Task CongBo_Admin_DaChot_Returns200_PublishesNotifiesEmails()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, tenant, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot, withTenantAccount: true);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };
            req.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var item = json.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.Equal("Đã công bố", item.GetProperty("congBo").GetString());
            Assert.True(item.GetProperty("congBoThanhCong").GetBoolean());
            Assert.Equal("Đã tạo thông báo", item.GetProperty("thongBaoCong").GetString());
            Assert.Equal("Đã gửi", item.GetProperty("email").GetString());
            Assert.Matches(@"^\d{2}/\d{2}/\d{4}$", item.GetProperty("hanThanhToan").GetString());
            Assert.Equal(1, json.RootElement.GetProperty("soDaCongBo").GetInt32());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reloaded = await db.HoaDons.FirstAsync(h => h.HoaDonId == invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, reloaded.TrangThaiPhatHanh);

            var nguoiDung = await db.NguoiDungs.FirstAsync(u => u.NguoiThueId == tenant.NguoiThueId);
            var thongBao = await db.ThongBaos.SingleAsync(t => t.NguoiDungId == nguoiDung.NguoiDungId);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={invoice.HoaDonId}", thongBao.LinkDieuHuong);

            var emailCall = Assert.Single(_factory.FakeEmailServiceInstance.InvoiceEmailCalls.Where(c => c.ToEmail == tenant.Email));
            Assert.NotEmpty(emailCall.HanThanhToan);
        }

        [Fact]
        public async Task CongBo_Repeat_ReturnsAlreadyPublished_NoExtraEmailOrNotification()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, tenant, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot, withTenantAccount: true);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            async Task<HttpResponseMessage> PostCongBo()
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
                {
                    Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
                };
                req.Headers.Add("RequestVerificationToken", token);
                return await client.SendAsync(req);
            }

            var first = await PostCongBo();
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var emailCountAfterFirst = _factory.FakeEmailServiceInstance.InvoiceEmailCalls.Count(c => c.ToEmail == tenant.Email);

            var second = await PostCongBo();
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);

            using var json = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            var item = json.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.Equal("Đã công bố trước đó", item.GetProperty("congBo").GetString());
            Assert.Equal("Không gửi", item.GetProperty("email").GetString());

            var emailCountAfterSecond = _factory.FakeEmailServiceInstance.InvoiceEmailCalls.Count(c => c.ToEmail == tenant.Email);
            Assert.Equal(emailCountAfterFirst, emailCountAfterSecond);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDung = await db.NguoiDungs.FirstAsync(u => u.NguoiThueId == tenant.NguoiThueId);
            var thongBaoCount = await db.ThongBaos.CountAsync(t => t.NguoiDungId == nguoiDung.NguoiDungId);
            Assert.Equal(1, thongBaoCount);
        }

        [Fact]
        public async Task CongBo_Batch_OneEmailFails_Returns200_BothDaGui()
        {
            var suffix1 = Guid.NewGuid().ToString("N")[..8];
            var suffix2 = Guid.NewGuid().ToString("N")[..8];
            var (_, _, tenantOk, _, invoiceOk) = await SeedInvoiceHierarchyAsync(suffix1, TrangThaiPhatHanhHoaDon.DaChot);
            var (_, _, tenantFail, _, invoiceFail) = await SeedInvoiceHierarchyAsync(suffix2, TrangThaiPhatHanhHoaDon.DaChot);

            _factory.FakeEmailServiceInstance.ResultByAddress[tenantFail.Email] = (false, "smtp lỗi giả lập");

            var adminUsername = $"adm_{suffix1}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoiceOk.HoaDonId, invoiceFail.HoaDonId } })
            };
            req.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
            var itemOk = items.Single(i => i.GetProperty("hoaDonId").GetInt32() == invoiceOk.HoaDonId);
            var itemFail = items.Single(i => i.GetProperty("hoaDonId").GetInt32() == invoiceFail.HoaDonId);

            Assert.Equal("Đã gửi", itemOk.GetProperty("email").GetString());
            Assert.Equal("Lỗi gửi", itemFail.GetProperty("email").GetString());
            Assert.DoesNotContain("smtp lỗi giả lập", itemFail.GetProperty("emailChiTiet").GetString() ?? "");

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reloadedOk = await db.HoaDons.FirstAsync(h => h.HoaDonId == invoiceOk.HoaDonId);
            var reloadedFail = await db.HoaDons.FirstAsync(h => h.HoaDonId == invoiceFail.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, reloadedOk.TrangThaiPhatHanh);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, reloadedFail.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task CongBo_NoPortalAccount_StillDaGui()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot, withTenantAccount: false);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };
            req.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var item = json.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.Equal("Khách chưa có tài khoản cổng", item.GetProperty("thongBaoCong").GetString());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reloaded = await db.HoaDons.FirstAsync(h => h.HoaDonId == invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, reloaded.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task CongBo_NoEmail_ReturnsKhongCoEmail()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var contract = await db.HopDongs.FirstAsync(h => h.HopDongId == invoice.HopDongId);
                var tenant = await db.NguoiThues.FirstAsync(t => t.NguoiThueId == contract.NguoiThueId);
                tenant.Email = " ";
                await db.SaveChangesAsync();
            }

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };
            req.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var item = json.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.Equal("Không có email", item.GetProperty("email").GetString());
        }

        [Fact]
        public async Task CongBo_StaffOtherBranch_Returns200_RowForbidden_StaysDaChot()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (branchOfInvoice, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            // Nhân viên được phân công một chi nhánh KHÁC chi nhánh của hóa đơn
            var otherBranchSuffix = Guid.NewGuid().ToString("N")[..8];
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var otherBranch = new ChiNhanh
            {
                TenChiNhanh = $"CN_other_{otherBranchSuffix}",
                MaChiNhanh = $"C_{otherBranchSuffix}",
                DiaChi = "Dia chi khac",
                SoDienThoai = "0900000099",
                MoTa = "Mo ta"
            };
            db.ChiNhanhs.Add(otherBranch);
            await db.SaveChangesAsync();

            var staffUsername = $"staff_{suffix}";
            const string staffPassword = "StaffPassword123!";
            await CreateAndAssignStaffAsync(staffUsername, staffPassword, otherBranch.ChiNhanhId);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, staffUsername, staffPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };
            req.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var item = json.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.False(item.GetProperty("congBoThanhCong").GetBoolean());
            Assert.Contains("không có quyền", item.GetProperty("congBo").GetString() ?? "", StringComparison.OrdinalIgnoreCase);

            var reloaded = await db.HoaDons.FirstAsync(h => h.HoaDonId == invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, reloaded.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task TenantPortal_SeesInvoiceOnlyAfterPublish()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, tenant, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            var tenantUsername = $"tnt_{suffix}";
            const string tenantPassword = "TenantPassword123!";
            await CreateTenantUserAsync(tenantUsername, tenantPassword, tenant.NguoiThueId);

            var tenantClient = NewClient(_factory);
            await LoginTenantAsync(tenantClient, tenantUsername, tenantPassword);

            var beforeRes = await tenantClient.GetAsync("/KhachThue/HoaDon/GetDanhSach");
            Assert.Equal(HttpStatusCode.OK, beforeRes.StatusCode);
            var beforeContent = await beforeRes.Content.ReadAsStringAsync();
            Assert.DoesNotContain(invoice.MaHoaDon, beforeContent);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var adminClient = NewClient(_factory);
            var token = await LoginAsync(adminClient, adminUsername, adminPassword);
            var congBoReq = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/CongBo")
            {
                Content = JsonContent.Create(new { hoaDonIds = new[] { invoice.HoaDonId } })
            };
            congBoReq.Headers.Add("RequestVerificationToken", token);
            var congBoRes = await adminClient.SendAsync(congBoReq);
            Assert.Equal(HttpStatusCode.OK, congBoRes.StatusCode);

            var afterRes = await tenantClient.GetAsync("/KhachThue/HoaDon/GetDanhSach");
            Assert.Equal(HttpStatusCode.OK, afterRes.StatusCode);
            var afterContent = await afterRes.Content.ReadAsStringAsync();
            Assert.Contains(invoice.MaHoaDon, afterContent);
        }

        // ---------- SendEmail (gửi lại email) ----------

        [Fact]
        public async Task SendEmail_MissingAntiforgery_Returns400()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaGui);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/SendEmail/{invoice.HoaDonId}");
            var response = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SendEmail_DaGui_Returns200_Sent()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, tenant, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaGui);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/SendEmail/{invoice.HoaDonId}");
            req.Headers.Add("RequestVerificationToken", token);
            var response = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Đã gửi", json.RootElement.GetProperty("email").GetString());
            Assert.Equal(tenant.Email, json.RootElement.GetProperty("recipientEmail").GetString());
            Assert.Equal(invoice.MaHoaDon, json.RootElement.GetProperty("invoiceCode").GetString());
        }

        [Fact]
        public async Task SendEmail_DaChot_Returns400()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaChot);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/SendEmail/{invoice.HoaDonId}");
            req.Headers.Add("RequestVerificationToken", token);
            var response = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reloaded = await db.HoaDons.FirstAsync(h => h.HoaDonId == invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, reloaded.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task SendEmail_DaHuy_Returns400()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaHuy);

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/SendEmail/{invoice.HoaDonId}");
            req.Headers.Add("RequestVerificationToken", token);
            var response = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("đã bị hủy", content);
        }

        [Fact]
        public async Task SendEmail_NoEmail_Returns200_KhongCoEmail()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var (_, _, _, _, invoice) = await SeedInvoiceHierarchyAsync(suffix, TrangThaiPhatHanhHoaDon.DaGui);

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var contract = await db.HopDongs.FirstAsync(h => h.HopDongId == invoice.HopDongId);
                var tenant = await db.NguoiThues.FirstAsync(t => t.NguoiThueId == contract.NguoiThueId);
                tenant.Email = "   ";
                await db.SaveChangesAsync();
            }

            var adminUsername = $"adm_{suffix}";
            const string adminPassword = "AdminPassword123!";
            await CreateAdminAsync(adminUsername, adminPassword);

            var client = NewClient(_factory);
            var token = await LoginAsync(client, adminUsername, adminPassword);

            var req = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/SendEmail/{invoice.HoaDonId}");
            req.Headers.Add("RequestVerificationToken", token);
            var response = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Không có email", json.RootElement.GetProperty("email").GetString());
        }
    }
}
