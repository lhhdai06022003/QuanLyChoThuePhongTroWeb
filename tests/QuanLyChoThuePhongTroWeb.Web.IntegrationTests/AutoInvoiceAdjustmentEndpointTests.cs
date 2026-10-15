using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
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
    // HTTP của điều chỉnh hóa đơn theo trạng thái, Admin chốt/trả lại tay hóa đơn chờ duyệt còn hạn phản hồi từ luồng cũ, và chốt kỳ chỉ tạo nháp (không gửi email).
    [Collection(WebTestCollection.Name)]
    public class AutoInvoiceAdjustmentEndpointTests
    {
        private const string Password = "Password123!";
        private const int ChoDuyet = (int)TrangThaiPhatHanhHoaDon.ChoDuyet;
        private const int DaChot = (int)TrangThaiPhatHanhHoaDon.DaChot;

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

        // Hóa đơn ChoDuyet được seed kèm HanPhanHoi như dữ liệu còn lại từ luồng tạm tính cũ.
        private async Task<Seed> SeedAsync(int roomCount = 1, bool withInvoice = true,
            TrangThaiPhatHanhHoaDon status = TrangThaiPhatHanhHoaDon.ChoDuyet)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_ADJ_{suffix}", MaChiNhanh = TestCodes.MaChiNhanh("J"), DiaChi = "A", SoDienThoai = "0900000066", MoTa = "M",
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

            if (withInvoice)
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
                    TrangThaiPhatHanh = status, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    HanPhanHoi = status != TrangThaiPhatHanhHoaDon.ChoDuyet ? null : DateTime.UtcNow.AddHours(24),
                    NgayChot = status == TrangThaiPhatHanhHoaDon.DaChot ? DateTime.UtcNow.AddHours(-1) : null,
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
        public async Task StaffOfSameBranch_AdjustsChoDuyetInvoice_Returns200_StaysChoDuyet()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);

            var info = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, info.StatusCode);
            using (var infoJson = JsonDocument.Parse(await info.Content.ReadAsStringAsync()))
            {
                Assert.True(infoJson.RootElement.GetProperty("coTheDieuChinh").GetBoolean());
                Assert.Equal(120m, infoJson.RootElement.GetProperty("chiSoDienMoi").GetDecimal());
                Assert.Equal(ChoDuyet, infoJson.RootElement.GetProperty("trangThaiPhatHanhValue").GetInt32());
                // Hạn phản hồi chỉ có với hóa đơn đã gửi.
                Assert.Equal(string.Empty, infoJson.RootElement.GetProperty("hanPhanHoi").GetString());
                Assert.False(infoJson.RootElement.TryGetProperty("hanTuChot", out _));
            }

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf,
                new { trangThaiPhatHanhHienTai = ChoDuyet, chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Đã điều chỉnh hóa đơn, hóa đơn vẫn chờ duyệt.", json.RootElement.GetProperty("message").GetString());
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(seed.Invoice.HoaDonId, data.GetProperty("hoaDonId").GetInt32());
            Assert.Equal(3_305_000m, data.GetProperty("tongTien").GetDecimal());
            Assert.False(data.GetProperty("daTaoHoaDonThayThe").GetBoolean());
            Assert.Equal(ChoDuyet, data.GetProperty("trangThaiPhatHanhMoi").GetInt32());
            Assert.False(data.TryGetProperty("hanTuChot", out _));

            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(3_305_000m, after.TongTien);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Null(after.HanPhanHoi);
        }

        [Fact]
        public async Task Info_OfSentInvoice_ReturnsStatusAndResponseDeadline()
        {
            var seed = await SeedAsync();
            var han = DateTime.UtcNow.AddHours(30);
            using (var scope = _factory.Services.CreateScope())
            {
                // Hóa đơn đã công bố: hạn khách phản hồi trả về theo giờ Việt Nam.
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var entity = await db.HoaDons.SingleAsync(h => h.HoaDonId == seed.Invoice!.HoaDonId);
                entity.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
                entity.HanPhanHoi = han;
                await db.SaveChangesAsync();
            }

            var (client, _) = await LoginAsync(seed.StaffUsername);

            var response = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("coTheDieuChinh").GetBoolean());
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.DaGui, json.RootElement.GetProperty("trangThaiPhatHanhValue").GetInt32());
            Assert.Equal(VnText(han), json.RootElement.GetProperty("hanPhanHoi").GetString());
        }

        [Fact]
        public async Task StaffOfAnotherBranch_GetsForbiddenOnInfoAndAdjust_AndDatabaseIsUnchanged()
        {
            var seed = await SeedAsync();
            var other = await SeedAsync(withInvoice: false);
            var (client, csrf) = await LoginAsync(other.StaffUsername);

            var info = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}");
            var adjust = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf, new { trangThaiPhatHanhHienTai = ChoDuyet, chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

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
                Content = JsonContent.Create(new { trangThaiPhatHanhHienTai = ChoDuyet, chiSoDienMoi = 130m, chiSoNuocMoi = 60m })
            };
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(seed.Invoice.TongTien, (await ReloadInvoiceAsync(seed.Invoice.HoaDonId)).TongTien);
        }

        [Fact]
        public async Task Admin_CanFinalizeAndReject_ChoDuyetWithLegacyDeadline()
        {
            var toFinalize = await SeedAsync();
            var toReject = await SeedAsync();
            Assert.NotNull(toFinalize.Invoice!.HanPhanHoi);
            Assert.NotNull(toReject.Invoice!.HanPhanHoi);
            var adminName = $"adj_admin_{Guid.NewGuid():N}"[..24];
            await CreateUserAsync(adminName, AppRole.Admin);
            var (client, csrf) = await LoginAsync(adminName);

            var chot = await client.SendAsync(Post($"/HoaDon/Chot/{toFinalize.Invoice.HoaDonId}", csrf));
            var traLai = await client.SendAsync(Post($"/HoaDon/TraLai/{toReject.Invoice.HoaDonId}", csrf, new { lyDo = "Sai số" }));

            Assert.Equal(HttpStatusCode.OK, chot.StatusCode);
            Assert.Equal(HttpStatusCode.OK, traLai.StatusCode);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, (await ReloadInvoiceAsync(toFinalize.Invoice.HoaDonId)).TrangThaiPhatHanh);
            Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, (await ReloadInvoiceAsync(toReject.Invoice.HoaDonId)).TrangThaiPhatHanh);
        }

        [Fact]
        public async Task StaffClosingTwoRooms_CreatesDraftInvoices_AndSendsNoEmail()
        {
            var seed = await SeedAsync(roomCount: 2, withInvoice: false);
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

            var emails = seed.Rooms.Select(r => r.Tenant.Email!).ToList();
            var fakeEmail = _factory.FakeEmailServiceInstance;

            var response = await client.SendAsync(Post("/DienNuoc/SaveChotDienNuoc", csrf, request));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                foreach (var (_, _, contract) in seed.Rooms)
                {
                    var invoice = await db.HoaDons.AsNoTracking().Include(h => h.LichSuTrangThaiHoaDons)
                        .SingleAsync(h => h.HopDongId == contract.HopDongId && !h.IsDeleted);
                    Assert.Equal(TrangThaiPhatHanhHoaDon.Nhap, invoice.TrangThaiPhatHanh);
                    Assert.Null(invoice.HanPhanHoi);
                    Assert.StartsWith("[TU_DONG]", Assert.Single(invoice.LichSuTrangThaiHoaDons).LyDo);
                }
            }

            Assert.DoesNotContain(fakeEmail.InvoiceEmailCalls, c => emails.Contains(c.ToEmail));
        }

        [Fact]
        public async Task StaffAdjustingChoDuyet_SendsNoEmail()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);
            var email = seed.Rooms[0].Tenant.Email!;

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}", csrf,
                new { trangThaiPhatHanhHienTai = ChoDuyet, chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3_305_000m, (await ReloadInvoiceAsync(seed.Invoice.HoaDonId)).TongTien);
            Assert.DoesNotContain(_factory.FakeEmailServiceInstance.InvoiceEmailCalls, c => c.ToEmail == email);
        }

        [Fact]
        public async Task Adjust_WithoutCurrentStatus_Returns400AndChangesNothing()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}", csrf, new { chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Dữ liệu không hợp lệ.", await response.Content.ReadAsStringAsync());
            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(seed.Invoice.TongTien, after.TongTien);
            Assert.Empty(after.LichSuTrangThaiHoaDons);
        }

        [Fact]
        public async Task Adjust_WithStaleStatus_Returns400AndChangesNothing()
        {
            // Màn hình còn thấy Chờ duyệt nhưng hóa đơn đã được chốt.
            var seed = await SeedAsync(status: TrangThaiPhatHanhHoaDon.DaChot);
            var adminName = $"adj_admin_{Guid.NewGuid():N}"[..24];
            await CreateUserAsync(adminName, AppRole.Admin);
            var (client, csrf) = await LoginAsync(adminName);

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}", csrf,
                new { trangThaiPhatHanhHienTai = ChoDuyet, chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Hóa đơn vừa đổi trạng thái, vui lòng tải lại.", await response.Content.ReadAsStringAsync());
            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, after.TrangThaiPhatHanh);
            Assert.Equal(seed.Invoice.TongTien, after.TongTien);
            Assert.Empty(after.LichSuTrangThaiHoaDons);
        }

        [Fact]
        public async Task StaffAdjustingDaChot_Returns403_AndInfoIsLocked()
        {
            var seed = await SeedAsync(status: TrangThaiPhatHanhHoaDon.DaChot);
            var (client, csrf) = await LoginAsync(seed.StaffUsername);

            var info = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}");
            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf,
                new { trangThaiPhatHanhHienTai = DaChot, chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.OK, info.StatusCode);
            using (var infoJson = JsonDocument.Parse(await info.Content.ReadAsStringAsync()))
            {
                Assert.False(infoJson.RootElement.GetProperty("coTheDieuChinh").GetBoolean());
                Assert.Equal("Chỉ Quản trị viên mới điều chỉnh được hóa đơn đã chốt.", infoJson.RootElement.GetProperty("lyDoKhoa").GetString());
                Assert.Equal(DaChot, infoJson.RootElement.GetProperty("trangThaiPhatHanhValue").GetInt32());
            }

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, after.TrangThaiPhatHanh);
            Assert.Equal(seed.Invoice.TongTien, after.TongTien);
        }

        [Fact]
        public async Task AdminAdjustingDaChot_Returns200_StaysDaChot()
        {
            var seed = await SeedAsync(status: TrangThaiPhatHanhHoaDon.DaChot);
            var adminName = $"adj_admin_{Guid.NewGuid():N}"[..24];
            await CreateUserAsync(adminName, AppRole.Admin);
            var (client, csrf) = await LoginAsync(adminName);

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}", csrf,
                new { trangThaiPhatHanhHienTai = DaChot, chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Đã điều chỉnh hóa đơn đã chốt.", json.RootElement.GetProperty("message").GetString());
            Assert.Equal(DaChot, json.RootElement.GetProperty("data").GetProperty("trangThaiPhatHanhMoi").GetInt32());
            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, after.TrangThaiPhatHanh);
            Assert.Equal(3_305_000m, after.TongTien);
            Assert.NotNull(after.NguoiChotId);
            Assert.NotNull(after.NgayChot);
        }

        // ---------- Sửa các khoản phí khi điều chỉnh ----------

        private async Task<List<ChiTietHoaDon>> ReloadLinesAsync(int id)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.ChiTietHoaDons.AsNoTracking().Where(c => c.HoaDonId == id && !c.IsDeleted).OrderBy(c => c.ChiTietHoaDonId).ToListAsync();
        }

        [Fact]
        public async Task Info_ReturnsInvoiceLinesWithKindAndTotal()
        {
            var seed = await SeedAsync();
            var (client, _) = await LoginAsync(seed.StaffUsername);

            var response = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(3_270_000m, json.RootElement.GetProperty("tongTien").GetDecimal());
            var lines = json.RootElement.GetProperty("chiTiets").EnumerateArray().ToList();
            Assert.Equal(3, lines.Count);
            Assert.Equal(new[] { "khac", "dien", "nuoc" }, lines.Select(l => l.GetProperty("loai").GetString()).ToArray());
            Assert.Equal(new[] { "", "kWh", "m³" }, lines.Select(l => l.GetProperty("donVi").GetString()).ToArray());
            Assert.Equal(new[] { 3_000_000m, 70_000m, 200_000m }, lines.Select(l => l.GetProperty("tongTien").GetDecimal()).ToArray());
            Assert.All(lines, l => Assert.True(l.GetProperty("chiTietHoaDonId").GetInt32() > 0));
        }

        [Fact]
        public async Task Preview_ReturnsRecalculatedMeterLines_AndWritesNothing()
        {
            var seed = await SeedAsync();
            var (client, _) = await LoginAsync(seed.StaffUsername);

            var response = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}/TinhLai?chiSoDienMoi=130&chiSoNuocMoi=70");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var dien = json.RootElement.GetProperty("dien");
            Assert.Equal("Tiền điện (100 → 130)", dien.GetProperty("tenDichVu").GetString());
            Assert.Equal((3_500m, 30m, 105_000m), (dien.GetProperty("donGia").GetDecimal(), dien.GetProperty("soLuong").GetDecimal(), dien.GetProperty("tongTien").GetDecimal()));
            var nuoc = json.RootElement.GetProperty("nuoc");
            Assert.Equal((20_000m, 20m, 400_000m), (nuoc.GetProperty("donGia").GetDecimal(), nuoc.GetProperty("soLuong").GetDecimal(), nuoc.GetProperty("tongTien").GetDecimal()));

            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(seed.Invoice.TongTien, after.TongTien);
            Assert.Empty(after.LichSuTrangThaiHoaDons);
        }

        [Fact]
        public async Task Preview_ReadingBelowOldValue_Returns400WithMessage()
        {
            var seed = await SeedAsync();
            var (client, _) = await LoginAsync(seed.StaffUsername);

            var response = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}/TinhLai?chiSoDienMoi=99&chiSoNuocMoi=60");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Preview_StaffOfAnotherBranch_IsForbidden_AndAnonymousIsRedirected()
        {
            var seed = await SeedAsync();
            var other = await SeedAsync(withInvoice: false);
            var (client, _) = await LoginAsync(other.StaffUsername);
            var anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

            var forbidden = await client.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}/TinhLai?chiSoDienMoi=130&chiSoNuocMoi=60");
            var redirected = await anonymous.GetAsync($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}/TinhLai?chiSoDienMoi=130&chiSoNuocMoi=60");

            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.True(redirected.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Adjust_WithEditedLines_ReplacesLinesAndReturnsNewTotal()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);
            var before = await ReloadLinesAsync(seed.Invoice!.HoaDonId);
            var phong = before.Single(l => l.TenDichVu == "Tiền phòng");
            var dien = before.Single(l => l.TenDichVu == "Tiền điện");

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf, new
            {
                trangThaiPhatHanhHienTai = ChoDuyet,
                chiTiets = new object[]
                {
                    new { chiTietHoaDonId = phong.ChiTietHoaDonId, tenDichVu = "Tiền phòng (giảm giá)", donGia = 2_800_000m, soLuong = 1m },
                    new { chiTietHoaDonId = dien.ChiTietHoaDonId, tenDichVu = "Tiền điện", donGia = 3_500m, soLuong = 20m },
                    new { tenDichVu = "Phí giữ xe", donGia = 100_000m, soLuong = 2m }
                }
            }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(2_800_000m + 70_000m + 200_000m, data.GetProperty("tongTien").GetDecimal());
            Assert.False(data.GetProperty("daTaoHoaDonThayThe").GetBoolean());

            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(3_070_000m, after.TongTien);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Null(after.HanPhanHoi);
            var lines = await ReloadLinesAsync(seed.Invoice.HoaDonId);
            Assert.Equal(new[] { "Tiền phòng (giảm giá)", "Tiền điện", "Phí giữ xe" }, lines.Select(l => l.TenDichVu).ToArray());
            Assert.Equal(new[] { 2_800_000m, 70_000m, 200_000m }, lines.Select(l => l.TongTien).ToArray());
            Assert.Contains(after.LichSuTrangThaiHoaDons, h => h.LyDo != null && h.LyDo == "[DIEU_CHINH] Điều chỉnh hóa đơn");
            Assert.Contains(after.LichSuTrangThaiHoaDons, h => h.LyDo != null && h.LyDo.Contains("(sửa 1 dòng, thêm 1 dòng, xóa 1 dòng)"));
        }

        [Fact]
        public async Task Adjust_WithInvalidLines_Returns400AndKeepsInvoice()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);

            var blankName = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}", csrf,
                new { trangThaiPhatHanhHienTai = ChoDuyet, chiTiets = new[] { new { tenDichVu = "  ", donGia = 1_000m, soLuong = 1m } } }));
            var emptyList = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf, new { trangThaiPhatHanhHienTai = ChoDuyet, chiTiets = Array.Empty<object>() }));
            var fractionalPrice = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf,
                new { trangThaiPhatHanhHienTai = ChoDuyet, chiTiets = new[] { new { tenDichVu = "Phí", donGia = 10.5m, soLuong = 1m } } }));

            Assert.Equal(HttpStatusCode.BadRequest, blankName.StatusCode);
            Assert.Contains("Tên dịch vụ không được để trống.", await blankName.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest, emptyList.StatusCode);
            Assert.Contains("Hóa đơn phải có ít nhất một khoản phí.", await emptyList.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest, fractionalPrice.StatusCode);
            Assert.Contains("Đơn giá phải là số nguyên VND.", await fractionalPrice.Content.ReadAsStringAsync());

            var after = await ReloadInvoiceAsync(seed.Invoice.HoaDonId);
            Assert.Equal(seed.Invoice.TongTien, after.TongTien);
            Assert.Equal(3, (await ReloadLinesAsync(seed.Invoice.HoaDonId)).Count);
        }

        [Fact]
        public async Task Adjust_ReadingsOnlyBody_Works()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);

            // Thân yêu cầu chỉ có trạng thái và hai chỉ số, không có danh sách dòng.
            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice!.HoaDonId}", csrf, new { trangThaiPhatHanhHienTai = ChoDuyet, chiSoDienMoi = 130m, chiSoNuocMoi = 60m }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3_305_000m, (await ReloadInvoiceAsync(seed.Invoice.HoaDonId)).TongTien);
        }

        [Fact]
        public async Task Adjust_WithNothingChanged_Returns400NoChange()
        {
            var seed = await SeedAsync();
            var (client, csrf) = await LoginAsync(seed.StaffUsername);
            var lines = await ReloadLinesAsync(seed.Invoice!.HoaDonId);

            var response = await client.SendAsync(Post($"/HoaDon/DieuChinh/{seed.Invoice.HoaDonId}", csrf, new
            {
                trangThaiPhatHanhHienTai = ChoDuyet,
                chiSoDienMoi = 120m,
                chiSoNuocMoi = 60m,
                chiTiets = lines.Select(l => new { chiTietHoaDonId = l.ChiTietHoaDonId, tenDichVu = l.TenDichVu, donGia = l.DonGia, soLuong = l.SoLuong }).ToArray()
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Không có thay đổi", await response.Content.ReadAsStringAsync());
            Assert.Equal(seed.Invoice.TongTien, (await ReloadInvoiceAsync(seed.Invoice.HoaDonId)).TongTien);
        }
    }
}
