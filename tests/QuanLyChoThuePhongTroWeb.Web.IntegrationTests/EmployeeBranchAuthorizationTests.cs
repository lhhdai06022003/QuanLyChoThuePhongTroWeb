using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
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
    public class EmployeeBranchAuthorizationTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public EmployeeBranchAuthorizationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Staff_AssignedToBranch1_CannotAccess_Branch2_ReadRoutes_Or_SendEmail()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var username = $"staff_{suffix}";
            const string password = "StaffPassword123!";

            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            // 1. Create two branches
            var branch1 = new ChiNhanh
            {
                TenChiNhanh = $"CN1_{suffix}",
                MaChiNhanh = $"C1{suffix}",
                DiaChi = "Dia chi 1",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta CN 1"
            };
            var branch2 = new ChiNhanh
            {
                TenChiNhanh = $"CN2_{suffix}",
                MaChiNhanh = $"C2{suffix}",
                DiaChi = "Dia chi 2",
                SoDienThoai = "0900000002",
                MoTa = "Mo ta CN 2"
            };
            dbContext.ChiNhanhs.AddRange(branch1, branch2);
            await dbContext.SaveChangesAsync();

            // 2. Create rooms, tenants, contracts, and invoices for both branches
            var room1 = new PhongTro
            {
                ChiNhanhId = branch1.ChiNhanhId,
                SoPhong = $"P1_{suffix}",
                GiaThue = 2000000m,
                DienTich = 20,
                MoTa = "Mo ta phong 1"
            };
            var room2 = new PhongTro
            {
                ChiNhanhId = branch2.ChiNhanhId,
                SoPhong = $"P2_{suffix}",
                GiaThue = 2000000m,
                DienTich = 20,
                MoTa = "Mo ta phong 2"
            };
            dbContext.PhongTros.AddRange(room1, room2);

            var tenant1 = new NguoiThue { HoVaTen = $"T1_{suffix}", SoDienThoai = "0911111111", Email = $"t1_{suffix}@test.com", CCCD = $"0791{suffix.Substring(0, 4)}" };
            var tenant2 = new NguoiThue { HoVaTen = $"T2_{suffix}", SoDienThoai = "0922222222", Email = $"t2_{suffix}@test.com", CCCD = $"0792{suffix.Substring(0, 4)}" };
            dbContext.NguoiThues.AddRange(tenant1, tenant2);
            await dbContext.SaveChangesAsync();

            var contract1 = new HopDong
            {
                MaHopDong = $"HD1_{suffix}",
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenant1.NguoiThueId,
                TienThuePhong = 2000000m,
                TienCocPhong = 2000000m,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-2),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            var contract2 = new HopDong
            {
                MaHopDong = $"HD2_{suffix}",
                PhongTroId = room2.PhongTroId,
                NguoiThueId = tenant2.NguoiThueId,
                TienThuePhong = 2000000m,
                TienCocPhong = 2000000m,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-2),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            dbContext.HopDongs.AddRange(contract1, contract2);
            await dbContext.SaveChangesAsync();

            var inv1 = new HoaDon
            {
                MaHoaDon = $"INV1_{suffix}",
                HopDongId = contract1.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                IsDeleted = false
            };
            var inv2 = new HoaDon
            {
                MaHoaDon = $"INV2_{suffix}",
                HopDongId = contract2.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 2000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                IsDeleted = false
            };
            dbContext.HoaDons.AddRange(inv1, inv2);
            await dbContext.SaveChangesAsync();

            // 3. Create staff user and assign only to Branch 1
            var createResult = await nguoiDungService.AddAsync(new CreateNguoiDungReq
            {
                TenDangNhap = username,
                MatKhau = password,
                Role = AppRole.NhanVien
            });
            Assert.True(createResult.Success);

            var staffUser = await dbContext.NguoiDungs.FirstAsync(u => u.TenDangNhap == username);

            // Find an admin user for NguoiPhanCongId
            var adminUser = await dbContext.NguoiDungs.FirstOrDefaultAsync(u => u.Role == Role.Admin);
            int adminId = adminUser?.NguoiDungId ?? staffUser.NguoiDungId;

            dbContext.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staffUser.NguoiDungId,
                ChiNhanhId = branch1.ChiNhanhId,
                IsActive = true,
                NguoiPhanCongId = adminId
            });
            await dbContext.SaveChangesAsync();

            try
            {
                // 4. Log in as staff
                var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

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
                var authedToken = ExtractAntiForgeryToken(suCoHtml);
                Assert.False(string.IsNullOrWhiteSpace(authedToken));

                // 5. Test Meter Reading:
                // Branch 2: Staff is NOT assigned -> Returns empty list
                var meterBranch2Res = await client.GetAsync($"/DienNuoc/GetDanhSachPhongs?chiNhanhId={branch2.ChiNhanhId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.OK, meterBranch2Res.StatusCode);
                var meterBranch2Content = await meterBranch2Res.Content.ReadAsStringAsync();
                var meterBranch2List = JsonSerializer.Deserialize<List<DienNuocPhongRes>>(meterBranch2Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Assert.NotNull(meterBranch2List);
                Assert.Empty(meterBranch2List);

                var previewBranch2Res = await client.GetAsync($"/HoaDon/PreviewPhatSinh?chiNhanhId={branch2.ChiNhanhId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.OK, previewBranch2Res.StatusCode);
                var previewBranch2 = JsonSerializer.Deserialize<List<PhatSinhPreviewRes>>(
                    await previewBranch2Res.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Assert.NotNull(previewBranch2);
                Assert.Empty(previewBranch2);

                var listBranch2Req = new HttpRequestMessage(HttpMethod.Post, "/HoaDon/GetList")
                {
                    Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("chiNhanhId", branch2.ChiNhanhId.ToString()),
                        new KeyValuePair<string, string>("thang", "9"),
                        new KeyValuePair<string, string>("nam", "2026"),
                        new KeyValuePair<string, string>("trangThai", "-1"),
                        new KeyValuePair<string, string>("draw", "1"),
                        new KeyValuePair<string, string>("start", "0"),
                        new KeyValuePair<string, string>("length", "10")
                    })
                };
                listBranch2Req.Headers.Add("RequestVerificationToken", authedToken);
                var listBranch2Res = await client.SendAsync(listBranch2Req);
                Assert.Equal(HttpStatusCode.OK, listBranch2Res.StatusCode);
                using (var listBranch2Json = JsonDocument.Parse(await listBranch2Res.Content.ReadAsStringAsync()))
                {
                    Assert.Equal(0, listBranch2Json.RootElement.GetProperty("recordsFiltered").GetInt32());
                    Assert.Empty(listBranch2Json.RootElement.GetProperty("data").EnumerateArray());
                }

                var unpaidBranch2Res = await client.GetAsync($"/HoaDon/GetUnpaidList?chiNhanhId={branch2.ChiNhanhId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.OK, unpaidBranch2Res.StatusCode);
                using (var unpaidBranch2Json = JsonDocument.Parse(await unpaidBranch2Res.Content.ReadAsStringAsync()))
                {
                    Assert.Empty(unpaidBranch2Json.RootElement.EnumerateArray());
                }

                // Branch 1: Staff is assigned -> Returns room list
                var meterBranch1Res = await client.GetAsync($"/DienNuoc/GetDanhSachPhongs?chiNhanhId={branch1.ChiNhanhId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.OK, meterBranch1Res.StatusCode);

                // 6. Test Invoice Read Routes for Branch 2:
                // GetById: must return 404
                var getByIdRes = await client.GetAsync($"/HoaDon/GetById/{inv2.HoaDonId}");
                Assert.Equal(HttpStatusCode.NotFound, getByIdRes.StatusCode);

                // ExportPdf: must return 404
                var exportPdfRes = await client.GetAsync($"/HoaDon/ExportPdf/{inv2.HoaDonId}");
                Assert.Equal(HttpStatusCode.NotFound, exportPdfRes.StatusCode);

                // ExportExcel: must return 404
                var exportExcelRes = await client.GetAsync($"/HoaDon/ExportExcel/{inv2.HoaDonId}");
                Assert.Equal(HttpStatusCode.NotFound, exportExcelRes.StatusCode);

                // GetVietQR: must return 404
                var getQrRes = await client.GetAsync($"/HoaDon/GetVietQR?hoaDonId={inv2.HoaDonId}");
                Assert.Equal(HttpStatusCode.NotFound, getQrRes.StatusCode);

                // 6. Test SendEmail
                var sendEmailReq = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/SendEmail/{inv2.HoaDonId}");
                sendEmailReq.Headers.Add("RequestVerificationToken", authedToken);
                var sendEmailRes = await client.SendAsync(sendEmailReq);
                Assert.Equal(HttpStatusCode.BadRequest, sendEmailRes.StatusCode);
                var sendEmailContent = await sendEmailRes.Content.ReadAsStringAsync();
                Assert.Contains("không có quyền", sendEmailContent, StringComparison.OrdinalIgnoreCase);

                // 7. Test Invoice Read Routes for Branch 1 (Assigned):
                var getByIdBranch1Res = await client.GetAsync($"/HoaDon/GetById/{inv1.HoaDonId}");
                Assert.Equal(HttpStatusCode.OK, getByIdBranch1Res.StatusCode);

                var unpaidBranch1Res = await client.GetAsync($"/HoaDon/GetUnpaidList?chiNhanhId={branch1.ChiNhanhId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.OK, unpaidBranch1Res.StatusCode);
                Assert.Contains(inv1.MaHoaDon, await unpaidBranch1Res.Content.ReadAsStringAsync(), StringComparison.Ordinal);

                // 8. Revoke the assignment and verify the same authenticated session fails closed.
                var assignment = await dbContext.NhanVienChiNhanhs
                    .SingleAsync(x => x.NguoiDungId == staffUser.NguoiDungId && x.ChiNhanhId == branch1.ChiNhanhId);
                assignment.IsActive = false;
                await dbContext.SaveChangesAsync();

                var revokedInvoiceRes = await client.GetAsync($"/HoaDon/GetById/{inv1.HoaDonId}");
                Assert.Equal(HttpStatusCode.NotFound, revokedInvoiceRes.StatusCode);

                var revokedMeterRes = await client.GetAsync($"/DienNuoc/GetDanhSachPhongs?chiNhanhId={branch1.ChiNhanhId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.OK, revokedMeterRes.StatusCode);
                var revokedMeterList = JsonSerializer.Deserialize<List<DienNuocPhongRes>>(
                    await revokedMeterRes.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Assert.NotNull(revokedMeterList);
                Assert.Empty(revokedMeterList);

                var revokedUnpaidRes = await client.GetAsync($"/HoaDon/GetUnpaidList?chiNhanhId={branch1.ChiNhanhId}&thang=9&nam=2026");
                Assert.Equal(HttpStatusCode.OK, revokedUnpaidRes.StatusCode);
                using var revokedUnpaidJson = JsonDocument.Parse(await revokedUnpaidRes.Content.ReadAsStringAsync());
                Assert.Empty(revokedUnpaidJson.RootElement.EnumerateArray());
            }
            finally
            {
                // Cleanup
                try
                {
                    using var cleanScope = _factory.Services.CreateScope();
                    var cleanDb = cleanScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var cleanUserService = cleanScope.ServiceProvider.GetRequiredService<INguoiDungService>();

                    var u = await cleanDb.NguoiDungs.FirstOrDefaultAsync(x => x.TenDangNhap == username);
                    if (u != null)
                    {
                        var assignments = cleanDb.NhanVienChiNhanhs.Where(x => x.NguoiDungId == u.NguoiDungId);
                        cleanDb.NhanVienChiNhanhs.RemoveRange(assignments);
                        await cleanDb.SaveChangesAsync();
                        await cleanUserService.DeleteAsync(u.NguoiDungId);
                    }

                    cleanDb.HoaDons.RemoveRange(inv1, inv2);
                    cleanDb.HopDongs.RemoveRange(contract1, contract2);
                    cleanDb.PhongTros.RemoveRange(room1, room2);
                    cleanDb.NguoiThues.RemoveRange(tenant1, tenant2);
                    cleanDb.ChiNhanhs.RemoveRange(branch1, branch2);
                    await cleanDb.SaveChangesAsync();
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
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
