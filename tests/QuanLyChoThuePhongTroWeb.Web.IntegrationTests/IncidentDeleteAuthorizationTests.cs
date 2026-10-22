using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
    public class IncidentDeleteAuthorizationTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public IncidentDeleteAuthorizationTests(CustomWebApplicationFactory factory)
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

        private static async Task<string> LoginStaffAsync(HttpClient client, string username, string password)
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
            var token = ExtractAntiForgeryToken(suCoHtml);
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Remove("RequestVerificationToken");
                client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
            }
            return token;
        }

        // Dựng chi nhánh 1 chứa phòng và sự cố (không tính phí), nhân viên được phân công vào chi nhánh chỉ định.
        private async Task<(int IncidentId, string Username, string Password)> SeedAsync(bool staffInIncidentBranch)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var suffix = Guid.NewGuid().ToString("N")[..8];

            var branch1 = new ChiNhanh
            {
                TenChiNhanh = $"CN_DEL1_{suffix}",
                MaChiNhanh = $"D{suffix[..5]}",
                DiaChi = "123 Duong",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta chi nhanh"
            };
            var branch2 = new ChiNhanh
            {
                TenChiNhanh = $"CN_DEL2_{suffix}",
                MaChiNhanh = $"E{suffix[..5]}",
                DiaChi = "456 Duong",
                SoDienThoai = "0900000002",
                MoTa = "Mo ta chi nhanh 2"
            };
            db.ChiNhanhs.AddRange(branch1, branch2);
            await db.SaveChangesAsync();

            var staffUsername = $"staff_del_{suffix}";
            const string staffPassword = "StaffPassword123!";
            await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = staffUsername,
                MatKhau = staffPassword,
                Role = AppRole.NhanVien
            });
            var staffUser = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == staffUsername);

            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staffUser.NguoiDungId,
                ChiNhanhId = staffInIncidentBranch ? branch1.ChiNhanhId : branch2.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = staffUser.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch1.ChiNhanhId,
                SoPhong = $"DL_{suffix[..4]}",
                GiaThue = 2000000m,
                DienTich = 20,
                MoTa = "Mo ta phong"
            };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue
            {
                HoVaTen = $"Tenant DEL {suffix}",
                Email = $"tenant_del_{suffix}@example.com",
                SoDienThoai = "0911000008",
                CCCD = $"079{suffix[..6]}"
            };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var incident = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Hong voi nuoc",
                MoTa = "Mo ta hong voi nuoc",
                TrangThai = TrangThaiSuCo.ChoTiepNhan,
                CongVaoHoaDon = false,
                ChiPhiSuaChua = 0m,
                NgayGui = DateTime.UtcNow
            };
            db.YeuCauSuCos.Add(incident);
            await db.SaveChangesAsync();

            return (incident.Id, staffUsername, staffPassword);
        }

        private async Task<bool> IsIncidentDeletedAsync(int incidentId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var incident = await db.YeuCauSuCos.AsNoTracking().FirstAsync(x => x.Id == incidentId);
            return incident.IsDeleted;
        }

        [Fact]
        public async Task Delete_StaffOfOtherBranch_ReturnsDeniedJson_IncidentNotDeleted()
        {
            var (incidentId, username, password) = await SeedAsync(staffInIncidentBranch: false);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            var token = await LoginStaffAsync(client, username, password);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("id", incidentId.ToString())
            });

            var response = await client.PostAsync("/QuanLyNhaTro/YeuCauSuCo/Delete", formContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Assert.False(root.GetProperty("success").GetBoolean());
            Assert.Equal("Bạn không có quyền xóa sự cố tại chi nhánh này.", root.GetProperty("message").GetString());
            Assert.False(await IsIncidentDeletedAsync(incidentId));
        }

        [Fact]
        public async Task Delete_MissingAntiforgery_Returns400_IncidentNotDeleted()
        {
            var (incidentId, username, password) = await SeedAsync(staffInIncidentBranch: true);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            await LoginStaffAsync(client, username, password);
            client.DefaultRequestHeaders.Remove("RequestVerificationToken");

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", incidentId.ToString())
            });

            var response = await client.PostAsync("/QuanLyNhaTro/YeuCauSuCo/Delete", formContent);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await IsIncidentDeletedAsync(incidentId));
        }
    }
}
