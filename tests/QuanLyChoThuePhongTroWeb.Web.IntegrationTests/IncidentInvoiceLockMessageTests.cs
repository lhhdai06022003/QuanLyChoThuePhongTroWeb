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
    public class IncidentInvoiceLockMessageTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public IncidentInvoiceLockMessageTests(CustomWebApplicationFactory factory)
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

        [Fact]
        public async Task UpdateStatus_CostChange_InvoiceLocked_ReturnsServiceMessage()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var suffix = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_SC_{suffix}",
                MaChiNhanh = $"C{suffix[..5]}",
                DiaChi = "123 Duong",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta chi nhanh"
            };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var staffUsername = $"staff_sc_{suffix}";
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
                ChiNhanhId = branch.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = staffUser.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"SC_{suffix[..4]}",
                GiaThue = 2000000m,
                DienTich = 20,
                MoTa = "Mo ta phong"
            };
            db.PhongTros.Add(room);
            await db.SaveChangesAsync();

            var tenant = new NguoiThue
            {
                HoVaTen = $"Tenant SC {suffix}",
                Email = $"tenant_sc_{suffix}@example.com",
                SoDienThoai = "0911000009",
                CCCD = $"079{suffix[..6]}"
            };
            db.NguoiThues.Add(tenant);
            await db.SaveChangesAsync();

            var contract = new HopDong
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                MaHopDong = $"HD_SC_{suffix}",
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-3),
                TienCocPhong = 2000000m,
                TienThuePhong = 2000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            db.HopDongs.Add(contract);
            await db.SaveChangesAsync();

            var month = 8;
            var year = 2026;
            var invoice = new HoaDon
            {
                HopDongId = contract.HopDongId,
                MaHoaDon = $"HD_SC_{month}_{year}_{suffix}",
                Thang = month,
                Nam = year,
                TongTien = 2000000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                NgayTao = DateTime.UtcNow.AddDays(-10)
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();

            // Incident completed in month 8/2026
            var incident = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Hong dien",
                MoTa = "Mo ta hong dien",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 50000m,
                NgayGui = new DateTime(year, month, 5, 0, 0, 0, DateTimeKind.Utc),
                NgayXuLy = new DateTime(year, month, 10, 0, 0, 0, DateTimeKind.Utc)
            };
            db.YeuCauSuCos.Add(incident);
            await db.SaveChangesAsync();

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            var token = await LoginStaffAsync(client, staffUsername, staffPassword);

            // POST /QuanLyNhaTro/YeuCauSuCo/UpdateStatus to change ChiPhiSuaChua
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Id", incident.Id.ToString()),
                new KeyValuePair<string, string>("TrangThai", ((int)AppTrangThaiSuCo.DaHoanThanh).ToString()),
                new KeyValuePair<string, string>("ChiPhiSuaChua", "75000"),
                new KeyValuePair<string, string>("CongVaoHoaDon", "true"),
                new KeyValuePair<string, string>("LyDoTuChoi", ""),
                new KeyValuePair<string, string>("GhiChuAdmin", "Ghi chu")
            });

            var response = await client.PostAsync("/QuanLyNhaTro/YeuCauSuCo/UpdateStatus", formContent);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Assert.False(root.GetProperty("success").GetBoolean());
            var message = root.GetProperty("message").GetString() ?? "";
            Assert.Contains("đã gửi duyệt hoặc đã chốt. Admin cần trả lại hoặc hủy hóa đơn trước khi sửa chi phí sự cố.", message);
        }
    }
}
