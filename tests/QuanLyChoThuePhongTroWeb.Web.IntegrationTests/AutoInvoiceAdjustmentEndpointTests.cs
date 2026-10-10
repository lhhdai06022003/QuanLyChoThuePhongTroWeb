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
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // HTTP của "Điều chỉnh và gửi lại", chặn Admin chốt/trả lại tay hóa đơn tạm tính, và gửi nền email tạm tính khi chốt kỳ.
    [Collection(WebTestCollection.Name)]
    public class AutoInvoiceAdjustmentEndpointTests
    {
        private const string Password = "Password123!";

        private readonly CustomWebApplicationFactory _factory;

        public AutoInvoiceAdjustmentEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private sealed class Seed
        {
            public ChiNhanh Branch { get; init; } = null!;
            public List<(PhongTro Room, NguoiThue Tenant, HopDong Contract)> Rooms { get; } = new();
            public HoaDon? Invoice { get; set; }
            public DichVuDienNuocCuaPhong? Period { get; set; }
            public string StaffUsername { get; init; } = string.Empty;
        }

        private async Task<Seed> SeedAsync(int roomCount = 1, bool withProvisionalInvoice = true, bool manualChoDuyet = false)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_ADJ_{suffix}", MaChiNhanh = $"J{suffix}", DiaChi = "A", SoDienThoai = "0900000066", MoTa = "M",
                SoGioPhanHoiHoaDon = 24
            };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var dien = new DichVu { TenDichVu = $"Dien {suffix}", DonVi = "kWh", LoaiDichVu = LoaiDichVu.Dien };
            var nuoc = new DichVu { TenDichVu = $"Nuoc {suffix}", DonVi = "m3", LoaiDichVu = LoaiDichVu.Nuoc };
            db.DichVus.AddRange(dien, nuoc);
            await db.SaveChangesAsync();
            db.DichVuChiNhanhs.AddRange(
                new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = dien.DichVuId, GiaDichVu = 3500m },
                new DichVuChiNhanh { ChiNhanhId = branch.ChiNhanhId, DichVuId = nuoc.DichVuId, GiaDichVu = 20000m });
            await db.SaveChangesAsync();

            var seed = new Seed { Branch = branch, StaffUsername = $"adj_staff_{suffix}" };
            for (var i = 0; i < roomCount; i++)
            {
                var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"J{i}_{suffix}", GiaThue = 3_000_000m, DienTich = 20, MoTa = "M" };
                var tenant = new NguoiThue
                {
                    HoVaTen = $"Khach {i} {suffix}", SoDienThoai = $"09{i}{suffix[..7]}", CCCD = $"J{i}{suffix}", Email = $"adj{i}_{suffix}@test.com"
                };
                db.AddRange(room, tenant);
                await db.SaveChangesAsync();
                var contract = new HopDong
                {
                    MaHopDong = $"HDJ{i}_{suffix}", PhongTroId = room.PhongTroId, NguoiThueId = tenant.NguoiThueId,
                    ThoiDiemBatDau = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
                    TrangThaiHopDong = TrangThaiHopDong.DangHoatDong, TienThuePhong = 3_000_000m, TienCocPhong = 3_000_000m
                };
                db.HopDongs.Add(contract);
                await db.SaveChangesAsync();
                seed.Rooms.Add((room, tenant, contract));
            }

            await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = seed.StaffUsername, MatKhau = Password, Role = AppRole.NhanVien });
            var staff = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == seed.StaffUsername);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, NguoiPhanCongId = staff.NguoiDungId,
                NgayPhanCong = DateTime.UtcNow.AddDays(-1), IsActive = true
            });
            await db.SaveChangesAsync();

            if (withProvisionalInvoice)
            {
                var (room, _, contract) = seed.Rooms[0];
                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId, Thang = 10, Nam = 2026,
                    ChiSoDienCu = 100, ChiSoDienMoi = 120, ChiSoNuocCu = 50, ChiSoNuocMoi = 60,
                    DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                };
                db.DichVuDienNuocCuaPhongs.Add(period);
                await db.SaveChangesAsync();

                var invoice = new HoaDon
                {
                    HopDongId = contract.HopDongId, MaHoaDon = $"HD-ADJ-{suffix}", Thang = 10, Nam = 2026,
                    TongTien = 3_000_000m + 70_000m + 200_000m,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    HanPhanHoi = manualChoDuyet ? null : DateTime.UtcNow.AddHours(24),
                    DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId, NgayTao = DateTime.UtcNow,
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                    {
                        new() { TenDichVu = "Tiền phòng", DonGia = 3_000_000m, SoLuong = 1, TongTien = 3_000_000m },
                        new() { TenDichVu = "Tiền điện", DonGia = 3500m, SoLuong = 20, TongTien = 70_000m },
                        new() { TenDichVu = "Tiền nước", DonGia = 20000m, SoLuong = 10, TongTien = 200_000m }
                    }
                };
                db.HoaDons.Add(invoice);
                await db.SaveChangesAsync();
                seed.Invoice = invoice;
                seed.Period = period;
            }

            return seed;
        }

        private async Task CreateUserAsync(string username, AppRole role)
        {
            using var scope = _factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var res = await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = username, MatKhau = Password, Role = role });
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

        private async Task<(HttpClient Client, string Csrf)> LoginAsync(string username)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var loginPage = await client.GetAsync("/QuanLyNhaTro/DangNhap");
            var csrf = ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());
            var response = await client.PostAsync("/QuanLyNhaTro/DangNhap", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", csrf),
                new KeyValuePair<string, string>("TenDangNhap", username),
                new KeyValuePair<string, string>("MatKhau", Password)
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var page = await client.GetAsync("/QuanLyNhaTro/YeuCauSuCo");
            var authCsrf = ExtractAntiForgeryToken(await page.Content.ReadAsStringAsync());
            return (client, string.IsNullOrWhiteSpace(authCsrf) ? csrf : authCsrf);
        }

        private static string VnText(DateTime utc)
            => utc.AddHours(7).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        private static HttpRequestMessage Post(string url, string csrf, object? body = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (body != null)
            {
                request.Content = JsonContent.Create(body);
            }

            request.Headers.Add("RequestVerificationToken", csrf);
            return request;
        }

        private async Task<HoaDon> ReloadInvoiceAsync(int id)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.HoaDons.AsNoTracking().Include(h => h.LichSuTrangThaiHoaDons).SingleAsync(h => h.HoaDonId == id);
        }

        [Fact]
        public async Task StaffOfSameBranch_AdjustsProvisionalInvoice_Returns200AndUpdatesDatabase()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);

            var info = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, info.StatusCode);
            using (var infoJson = JsonDocument.Parse(await info.Content.ReadAsStringAsync()))
            {
                Assert.True(infoJson.RootElement.GetProperty("coTheDieuChinh").GetBoolean());
                Assert.Equal(120m, infoJson.RootElement.GetProperty("chiSoDienMoi").GetDecimal());
                // GET và POST cùng trả hạn tự chốt theo giờ Việt Nam.
                Assert.Equal(VnText(seed.Invoice.HanPhanHoi!.Value), infoJson.RootElement.GetProperty("hanTuChot").GetString());
            }

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf, new { chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(seed.Invoice.HoaDonId, data.GetProperty("hoaDonId").GetInt32());
            Assert.Equal(3_305_000m, data.GetProperty("tongTien").GetDecimal());
            Assert.False(data.GetProperty("daTaoHoaDonThayThe").GetBoolean());

            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(3_305_000m, after.TongTien);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.InRange(after.HanPhanHoi!.Value, DateTime.UtcNow.AddHours(23), DateTime.UtcNow.AddHours(25));
            Assert.Equal(VnText(after.HanPhanHoi.Value), data.GetProperty("hanTuChot").GetString());
        }

        [Fact]
        public async Task StaffOfAnotherBranch_GetsForbiddenOnInfoAndAdjust_AndDatabaseIsUnchanged()
        {
            var seed = await SeedAsync();
            var other = await SeedAsync(withProvisionalInvoice: false);
            var (client, csrf) = await LoginAsync(other.StaffUsername);

            var info = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}");
            var adjust = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf, new { chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.Forbidden, info.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, adjust.StatusCode);
            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(seed.Invoice.TongTien, after.TongTien);
            Assert.Equal(seed.Invoice.HanPhanHoi!.Value, after.HanPhanHoi!.Value, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Adjust_WithoutAntiforgeryToken_ReturnsBadRequest()
        {
            var seed = await SeedAsync();
            var (client, _) = await LoginAsync(seed.StaffUsername);

            var request = new HttpRequestMessage(HttpMethod.Post, $"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}")
            {
                Content = JsonContent.Create(new { chiSoDienMoi = 130m, chiSoNuocMoi = 60m })
            };
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(seed.Invoice.TongTien, (await ReloadInvoiceAsync(seed.Invoice.HoaDonId)).TongTien);
        }

        [Fact]
        public async Task Admin_CannotFinalizeOrRejectProvisionalInvoice_ButStillCanForManualChoDuyet()
        {
            var provisional = await SeedAsync();
            var manual = await SeedAsync(manualChoDuyet: true);
            var adminName = $"adj_admin_{Guid.NewGuid():N}"[..24];
            await CreateUserAsync(adminName, AppRole.Admin);
            var (client, csrf) = await LoginAsync(adminName);

            var chot = await client.SendAsync(Post($"/HoaDon/Chot/{provisional.Invoice!.HoaDonId}", csrf));
            var traLai = await client.SendAsync(Post($"/HoaDon/TraLai/{provisional.Invoice.HoaDonId}", csrf, new { lyDo = "Sai số" }));

            Assert.Equal(HttpStatusCode.BadRequest, chot.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, traLai.StatusCode);
            Assert.Contains("Hóa đơn đang tự chốt theo hạn, dùng Điều chỉnh và gửi lại.", await chot.Content.ReadAsStringAsync());
            Assert.Contains("Hóa đơn đang tự chốt theo hạn, dùng Điều chỉnh và gửi lại.", await traLai.Content.ReadAsStringAsync());
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, (await ReloadInvoiceAsync(provisional.Invoice.HoaDonId)).TrangThaiPhatHanh);

            var manualChot = await client.SendAsync(Post($"/HoaDon/Chot/{manual.Invoice!.HoaDonId}", csrf));
            Assert.Equal(HttpStatusCode.OK, manualChot.StatusCode);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, (await ReloadInvoiceAsync(manual.Invoice.HoaDonId)).TrangThaiPhatHanh);
        }

        [Fact]
        public async Task StaffClosingTwoRooms_SendsProvisionalEmailsInBackground()
        {
            var seed = await SeedAsync(roomCount: 2, withProvisionalInvoice: false);
            var (client, csrf) = await LoginAsync(seed.StaffUsername);
            var request = new ChotDienNuocReq
            {
                ChiNhanhId = seed.Branch.ChiNhanhId,
                Thang = 10,
                Nam = 2026,
                DanhSachPhong = seed.Rooms.Select(r => new DienNuocPhongReq
                {
                    PhongTroId = r.Room.PhongTroId,
                    DienMode = MeterReadingSubmissionMode.Manual,
                    NuocMode = MeterReadingSubmissionMode.Manual,
                    ChiSoDienCu = 0,
                    ChiSoDienMoi = 50,
                    ChiSoNuocCu = 0,
                    ChiSoNuocMoi = 10,
                    LyDoNhapThuCongDien = "Đồng hồ mờ số",
                    LyDoNhapThuCongNuoc = "Khách vắng nhà"
                }).ToList()
            };

            var response = await client.SendAsync(Post("/DienNuoc/SaveChotDienNuoc", csrf, request));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var emails = seed.Rooms.Select(r => r.Tenant.Email!).ToList();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline
                   && emails.Any(e => !_factory.FakeEmailServiceInstance.ProvisionalEmailCalls.Any(c => c.ToEmail == e)))
            {
                await Task.Delay(100);
            }

            foreach (var email in emails)
            {
                Assert.Contains(_factory.FakeEmailServiceInstance.ProvisionalEmailCalls, c => c.ToEmail == email);
            }
        }
    }
}
