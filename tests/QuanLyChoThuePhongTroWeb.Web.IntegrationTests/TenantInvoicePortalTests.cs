using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
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
    public class TenantInvoicePortalTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public TenantInvoicePortalTests(CustomWebApplicationFactory factory)
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

        private class TenantTestContextData
        {
            public string TenantAUsername { get; set; } = string.Empty;
            public string TenantAPassword { get; set; } = string.Empty;
            public string TenantBUsername { get; set; } = string.Empty;
            public string TenantBPassword { get; set; } = string.Empty;
            public string MemberUsername { get; set; } = string.Empty;
            public string MemberPassword { get; set; } = string.Empty;

            public HoaDon InvoiceDaGui { get; set; } = null!;
            public HoaDon InvoiceCancelled { get; set; } = null!;
            public HoaDon InvoiceDaChot { get; set; } = null!;
            public HoaDon InvoiceNhap { get; set; } = null!;
            public HoaDon InvoiceOtherTenant { get; set; } = null!;
        }

        private async Task<TenantTestContextData> SeedTestDataAsync(string prefix)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nguoiDungService = scope.ServiceProvider.GetRequiredService<INguoiDungService>();

            var suffix = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_{prefix}_{suffix}",
                MaChiNhanh = TestCodes.MaChiNhanh("C"),
                DiaChi = "123 Duong",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta"
            };
            db.ChiNhanhs.Add(branch);
            await db.SaveChangesAsync();

            var room1 = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"101_{suffix[..4]}",
                GiaThue = 3000000,
                DienTich = 25,
                MoTa = "Phong 1"
            };
            var room2 = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"102_{suffix[..4]}",
                GiaThue = 3500000,
                DienTich = 30,
                MoTa = "Phong 2"
            };
            db.PhongTros.AddRange(room1, room2);
            await db.SaveChangesAsync();

            var tenantA = new NguoiThue
            {
                HoVaTen = $"Khach A {suffix}",
                SoDienThoai = "0911000001",
                CCCD = $"001{suffix[..6]}1",
                Email = $"a_{suffix}@test.com"
            };
            var tenantB = new NguoiThue
            {
                HoVaTen = $"Khach B {suffix}",
                SoDienThoai = "0911000002",
                CCCD = $"001{suffix[..6]}2",
                Email = $"b_{suffix}@test.com"
            };
            var tenantMember = new NguoiThue
            {
                HoVaTen = $"Member M {suffix}",
                SoDienThoai = "0911000003",
                CCCD = $"001{suffix[..6]}3",
                Email = $"m_{suffix}@test.com"
            };
            db.NguoiThues.AddRange(tenantA, tenantB, tenantMember);
            await db.SaveChangesAsync();

            var contract1 = new HopDong
            {
                PhongTroId = room1.PhongTroId,
                NguoiThueId = tenantA.NguoiThueId,
                MaHopDong = $"HD1_{suffix}",
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-6),
                TienCocPhong = 3000000m,
                TienThuePhong = 3000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            var contract2 = new HopDong
            {
                PhongTroId = room2.PhongTroId,
                NguoiThueId = tenantB.NguoiThueId,
                MaHopDong = $"HD2_{suffix}",
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-6),
                TienCocPhong = 3500000m,
                TienThuePhong = 3500000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            db.HopDongs.AddRange(contract1, contract2);
            await db.SaveChangesAsync();

            var memberRel = new ChiTietThanhVienHopDong
            {
                HopDongId = contract1.HopDongId,
                NguoiThueId = tenantMember.NguoiThueId
            };
            db.ChiTietThanhVienHopDongs.Add(memberRel);
            await db.SaveChangesAsync();

            HoaDon CreateInvoice(HopDong c, string codeSuffix, int month, TrangThaiPhatHanhHoaDon ph, bool hasNgayGui)
            {
                return new HoaDon
                {
                    HopDongId = c.HopDongId,
                    MaHoaDon = $"HD_{codeSuffix}_{suffix}",
                    Thang = month,
                    Nam = 2026,
                    TongTien = c.TienThuePhong,
                    TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    TrangThaiPhatHanh = ph,
                    NgayGui = hasNgayGui ? DateTime.UtcNow.AddDays(-2) : null,
                    NgayTao = DateTime.UtcNow.AddDays(-3),
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                    {
                        new ChiTietHoaDon
                        {
                            TenDichVu = "Tiền phòng",
                            SoLuong = 1,
                            DonGia = c.TienThuePhong,
                            TongTien = c.TienThuePhong
                        }
                    }
                };
            }

            var invDaGui = CreateInvoice(contract1, "DAGUI", 1, TrangThaiPhatHanhHoaDon.DaGui, true);
            var invCancelled = CreateInvoice(contract1, "HUY", 2, TrangThaiPhatHanhHoaDon.DaHuy, true);
            var invDaChot = CreateInvoice(contract1, "CHOT", 3, TrangThaiPhatHanhHoaDon.DaChot, false);
            var invNhap = CreateInvoice(contract1, "NHAP", 4, TrangThaiPhatHanhHoaDon.Nhap, false);
            var invOther = CreateInvoice(contract2, "OTHER", 1, TrangThaiPhatHanhHoaDon.DaGui, true);

            db.HoaDons.AddRange(invDaGui, invCancelled, invDaChot, invNhap, invOther);
            await db.SaveChangesAsync();

            // Cancel history for invCancelled
            db.LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = invCancelled.HoaDonId,
                TrangThaiPhatHanhCu = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiPhatHanhMoi = TrangThaiPhatHanhHoaDon.DaHuy,
                TrangThaiThanhToanCu = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiThanhToanMoi = TrangThaiHoaDon.ChuaThanhToan,
                NgayThucHien = DateTime.UtcNow.AddDays(-1),
                LyDo = "Khách yêu cầu hủy"
            });
            await db.SaveChangesAsync();

            // Create users
            var uA = $"tntA_{suffix}";
            var uB = $"tntB_{suffix}";
            var uM = $"mbrM_{suffix}";
            const string pwd = "Password123!";

            await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = uA, MatKhau = pwd, Role = AppRole.KhachThue, NguoiThueId = tenantA.NguoiThueId });
            await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = uB, MatKhau = pwd, Role = AppRole.KhachThue, NguoiThueId = tenantB.NguoiThueId });
            await nguoiDungService.AddAsync(new CreateNguoiDungReq { TenDangNhap = uM, MatKhau = pwd, Role = AppRole.KhachThue, NguoiThueId = tenantMember.NguoiThueId });

            return new TenantTestContextData
            {
                TenantAUsername = uA,
                TenantAPassword = pwd,
                TenantBUsername = uB,
                TenantBPassword = pwd,
                MemberUsername = uM,
                MemberPassword = pwd,
                InvoiceDaGui = invDaGui,
                InvoiceCancelled = invCancelled,
                InvoiceDaChot = invDaChot,
                InvoiceNhap = invNhap,
                InvoiceOtherTenant = invOther
            };
        }

        private HttpClient CreateHttpClient()
        {
            return _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
        }

        private static async Task<HttpResponseMessage> PostTaoLuotAsync(HttpClient client, int hoaDonId)
        {
            var page = await client.GetAsync("/KhachThue/HoaDon");
            var token = ExtractAntiForgeryToken(await page.Content.ReadAsStringAsync());
            var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/HoaDon/ThanhToan/Tao")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { hoaDonId }), System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(req);
        }

        [Fact]
        public async Task GetDanhSach_IncludesPublishState_CancelledMarked()
        {
            var data = await SeedTestDataAsync("GDS");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync("/KhachThue/HoaDon/GetDanhSach");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var items = doc.RootElement.EnumerateArray().ToList();

            var cancelledItem = items.FirstOrDefault(x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceCancelled.HoaDonId);
            Assert.True(cancelledItem.ValueKind != JsonValueKind.Undefined, "Cancelled invoice should be present in list");
            Assert.Equal(4, cancelledItem.GetProperty("trangThaiPhatHanh").GetInt32());
            Assert.Equal("Đã hủy", cancelledItem.GetProperty("trangThai").GetString());

            var daGuiItem = items.FirstOrDefault(x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceDaGui.HoaDonId);
            Assert.True(daGuiItem.ValueKind != JsonValueKind.Undefined, "DaGui invoice should be present in list");

            // Nhap and DaChot must not be returned to tenant
            Assert.DoesNotContain(items, x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceNhap.HoaDonId);
            Assert.DoesNotContain(items, x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceDaChot.HoaDonId);
        }

        [Fact]
        public async Task XemChiTiet_Cancelled_200_DaHuyReason_PanelCannotPay()
        {
            var data = await SeedTestDataAsync("XCT_Can");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceCancelled.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.GetProperty("daHuy").GetBoolean());
            Assert.Equal("Khách yêu cầu hủy", root.GetProperty("lyDoHuy").GetString());
            Assert.False(root.TryGetProperty("bankId", out _));

            var panel = await client.GetAsync($"/KhachThue/HoaDon/ThanhToan/{data.InvoiceCancelled.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
            using var panelDoc = JsonDocument.Parse(await panel.Content.ReadAsStringAsync());
            Assert.False(panelDoc.RootElement.GetProperty("data").GetProperty("coTheTaoLuot").GetBoolean());
        }

        [Fact]
        public async Task XemChiTiet_DaGui_CanPay()
        {
            var data = await SeedTestDataAsync("XCT_Gui");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceDaGui.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.False(root.GetProperty("daHuy").GetBoolean());
            Assert.Equal(root.GetProperty("tongTien").GetDecimal(), root.GetProperty("conLai").GetDecimal());

            var panel = await client.GetAsync($"/KhachThue/HoaDon/ThanhToan/{data.InvoiceDaGui.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
            using var panelDoc = JsonDocument.Parse(await panel.Content.ReadAsStringAsync());
            Assert.True(panelDoc.RootElement.GetProperty("data").GetProperty("coTheTaoLuot").GetBoolean());
        }

        [Fact]
        public async Task XemChiTiet_DaChotOrNhap_404()
        {
            var data = await SeedTestDataAsync("XCT_NotPub");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var resChot = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceDaChot.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, resChot.StatusCode);

            var resNhap = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceNhap.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, resNhap.StatusCode);
        }

        [Fact]
        public async Task XemChiTiet_OtherTenant_404()
        {
            var data = await SeedTestDataAsync("XCT_Other");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceOtherTenant.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task XemChiTiet_Member_404()
        {
            var data = await SeedTestDataAsync("XCT_Mbr");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.MemberUsername, data.MemberPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceDaGui.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task XemChiTiet_NoHistoryAndNoDueDateFields()
        {
            var data = await SeedTestDataAsync("XCT_NoFields");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceDaGui.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.False(root.TryGetProperty("lichSuTrangThais", out _));
            Assert.False(root.TryGetProperty("LichSuTrangThais", out _));
            Assert.False(root.TryGetProperty("hanThanhToan", out _));
            Assert.False(root.TryGetProperty("HanThanhToan", out _));
        }

        [Fact]
        public async Task TaiPdf_OwnDaGui_200_Pdf()
        {
            var data = await SeedTestDataAsync("TPdf_Gui");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/TaiPdf/{data.InvoiceDaGui.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task TaiPdf_OwnCancelled_200_Pdf()
        {
            var data = await SeedTestDataAsync("TPdf_Can");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/TaiPdf/{data.InvoiceCancelled.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task TaiPdf_OtherTenant_404()
        {
            var data = await SeedTestDataAsync("TPdf_Oth");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/TaiPdf/{data.InvoiceOtherTenant.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task TaiPdf_DaChot_404()
        {
            var data = await SeedTestDataAsync("TPdf_Chot");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/TaiPdf/{data.InvoiceDaChot.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task TaiPdf_Anonymous_NotPdf()
        {
            var data = await SeedTestDataAsync("TPdf_Anon");
            var client = CreateHttpClient();

            var res = await client.GetAsync($"/KhachThue/HoaDon/TaiPdf/{data.InvoiceDaGui.HoaDonId}");
            // [Authorize] will redirect unauthenticated users to login or 401
            Assert.NotEqual(HttpStatusCode.OK, res.StatusCode);
            Assert.NotEqual("application/pdf", res.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Index_WithHoaDonId_RendersAutoOpenValue()
        {
            var data = await SeedTestDataAsync("Idx_Open");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon?hoaDonId={data.InvoiceDaGui.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var html = await res.Content.ReadAsStringAsync();
            Assert.Contains(data.InvoiceDaGui.HoaDonId.ToString(), html);
        }

        [Fact]
        public async Task Index_NonNumericHoaDonId_200_NoAutoOpen()
        {
            var data = await SeedTestDataAsync("Idx_NonNum");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync("/KhachThue/HoaDon?hoaDonId=abc");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var html = await res.Content.ReadAsStringAsync();
            Assert.DoesNotContain("abc", html);
        }

        [Fact]
        public async Task CreatePaymentRequest_Cancelled_400()
        {
            var data = await SeedTestDataAsync("VQR_Can");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await PostTaoLuotAsync(client, data.InvoiceCancelled.HoaDonId);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        private async Task<(HoaDon Provisional, HoaDon ManualReview)> AddProvisionalInvoicesAsync(HoaDon baseInvoice)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            HoaDon Make(string tag, int month, decimal total, DateTime? han) => new()
            {
                HopDongId = baseInvoice.HopDongId,
                MaHoaDon = $"HD_{tag}_{suffix}",
                Thang = month,
                Nam = 2026,
                TongTien = total,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet,
                HanPhanHoi = han,
                NgayTao = DateTime.UtcNow.AddDays(-1),
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new() { TenDichVu = "Tiền phòng", SoLuong = 1, DonGia = total, TongTien = total }
                }
            };

            var provisional = Make("TAMTINH", 5, 1_234_567m, DateTime.UtcNow.AddHours(24));
            var manual = Make("CHODUYET", 6, 7_654_321m, null);
            db.HoaDons.AddRange(provisional, manual);
            await db.SaveChangesAsync();
            return (provisional, manual);
        }

        [Fact]
        public async Task GetDanhSach_KhongCoHoaDonChoDuyet_KeCaTamTinhCu()
        {
            var data = await SeedTestDataAsync("GDS_TT");
            var (legacyProvisional, manual) = await AddProvisionalInvoicesAsync(data.InvoiceDaGui);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync("/KhachThue/HoaDon/GetDanhSach");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var items = doc.RootElement.EnumerateArray().ToList();

            Assert.Contains(items, x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceDaGui.HoaDonId);
            Assert.DoesNotContain(items, x => x.GetProperty("hoaDonId").GetInt32() == legacyProvisional.HoaDonId);
            Assert.DoesNotContain(items, x => x.GetProperty("hoaDonId").GetInt32() == manual.HoaDonId);
        }

        [Fact]
        public async Task XemChiTiet_HoaDonChoDuyet_KeCaTamTinhCu_404()
        {
            var data = await SeedTestDataAsync("XCT_TT");
            var (legacyProvisional, manual) = await AddProvisionalInvoicesAsync(data.InvoiceDaGui);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var provisionalRes = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={legacyProvisional.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, provisionalRes.StatusCode);

            var notFound = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={manual.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        }

        [Fact]
        public async Task XemChiTiet_Provisional_OtherTenant_404()
        {
            var data = await SeedTestDataAsync("XCT_TT_Oth");
            var (provisional, _) = await AddProvisionalInvoicesAsync(data.InvoiceDaGui);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantBUsername, data.TenantBPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={provisional.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task TaiPdf_Provisional_404()
        {
            var data = await SeedTestDataAsync("TPdf_TT");
            var (provisional, _) = await AddProvisionalInvoicesAsync(data.InvoiceDaGui);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/TaiPdf/{provisional.HoaDonId}");

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task CreatePaymentRequest_Provisional_Rejected_AndNoRequestCreated()
        {
            var data = await SeedTestDataAsync("VQR_TT");
            var (provisional, _) = await AddProvisionalInvoicesAsync(data.InvoiceDaGui);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await PostTaoLuotAsync(client, provisional.HoaDonId);

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(db.YeuCauThanhToanHoaDons.Any(y => y.HoaDonId == provisional.HoaDonId));
        }

        [Fact]
        public async Task PaymentPanel_Provisional_404_AndPartialPaymentRequestRejected()
        {
            var data = await SeedTestDataAsync("PNL_TT");
            var (provisional, _) = await AddProvisionalInvoicesAsync(data.InvoiceDaGui);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var panel = await client.GetAsync($"/KhachThue/HoaDon/ThanhToan/{provisional.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, panel.StatusCode);

            var page = await client.GetAsync("/KhachThue/HoaDon");
            var token = ExtractAntiForgeryToken(await page.Content.ReadAsStringAsync());
            var req = new HttpRequestMessage(HttpMethod.Post, "/KhachThue/HoaDon/ThanhToan/Tao")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { hoaDonId = provisional.HoaDonId, soTien = 1000m }), System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.Add("RequestVerificationToken", token);
            var partial = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.NotFound, partial.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(db.YeuCauThanhToanHoaDons.Any(y => y.HoaDonId == provisional.HoaDonId));
        }

        private static string VnText(DateTime utc)
            => utc.AddHours(7).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        private async Task AttachMeterPeriodAsync(HoaDon invoice)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contract = await db.HopDongs.FindAsync(invoice.HopDongId);
            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = contract!.PhongTroId, Thang = invoice.Thang, Nam = invoice.Nam,
                ChiSoDienCu = 100, ChiSoDienMoi = 120, ChiSoNuocCu = 50, ChiSoNuocMoi = 60,
                DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();
            var tracked = await db.HoaDons.FindAsync(invoice.HoaDonId);
            tracked!.DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId;
            await db.SaveChangesAsync();
        }

        // Hóa đơn đã gửi (DaGui) với hạn phản hồi cho trước, cùng hợp đồng với hóa đơn mẫu.
        private async Task<HoaDon> AddSentInvoiceAsync(HoaDon baseInvoice, string tag, int month, DateTime? han)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = new HoaDon
            {
                HopDongId = baseInvoice.HopDongId,
                MaHoaDon = $"HD_{tag}_{Guid.NewGuid().ToString("N")[..8]}",
                Thang = month,
                Nam = 2026,
                TongTien = 500_000m,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                HanPhanHoi = han,
                NgayGui = DateTime.UtcNow.AddHours(-1),
                NgayTao = DateTime.UtcNow.AddDays(-1),
                ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                {
                    new() { TenDichVu = "Tiền phòng", SoLuong = 1, DonGia = 500_000m, TongTien = 500_000m }
                }
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            return invoice;
        }

        [Fact]
        public async Task GetDanhSach_DaGui_HanPhanHoiConHan_CoGioVietNam_HetHanHoacKhongCoThiRong()
        {
            var data = await SeedTestDataAsync("GDS_Flag");
            var conHan = await AddSentInvoiceAsync(data.InvoiceDaGui, "CONHAN", 7, DateTime.UtcNow.AddHours(24));
            var hetHan = await AddSentInvoiceAsync(data.InvoiceDaGui, "HETHAN", 8, DateTime.UtcNow.AddHours(-24));
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync("/KhachThue/HoaDon/GetDanhSach");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var items = doc.RootElement.EnumerateArray().ToList();
            var conHanRow = items.Single(x => x.GetProperty("hoaDonId").GetInt32() == conHan.HoaDonId);
            Assert.Equal(VnText(conHan.HanPhanHoi!.Value), conHanRow.GetProperty("hanPhanHoi").GetString());
            Assert.False(conHanRow.TryGetProperty("laTamTinh", out _));
            Assert.False(conHanRow.TryGetProperty("hanTuChot", out _));

            Assert.Equal(string.Empty, items.Single(x => x.GetProperty("hoaDonId").GetInt32() == hetHan.HoaDonId).GetProperty("hanPhanHoi").GetString());
            Assert.Equal(string.Empty, items.Single(x => x.GetProperty("hoaDonId").GetInt32() == data.InvoiceDaGui.HoaDonId).GetProperty("hanPhanHoi").GetString());
        }

        [Fact]
        public async Task XemChiTiet_DaGui_ReturnsHanPhanHoiBranchPhoneAndMeterReadings()
        {
            var data = await SeedTestDataAsync("XCT_Flag");
            var sent = await AddSentInvoiceAsync(data.InvoiceDaGui, "CHITIET", 7, DateTime.UtcNow.AddHours(24));
            await AttachMeterPeriodAsync(sent);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={sent.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Assert.Equal(VnText(sent.HanPhanHoi!.Value), root.GetProperty("hanPhanHoi").GetString());
            Assert.False(root.TryGetProperty("laTamTinh", out _));
            Assert.False(root.TryGetProperty("hanTuChot", out _));
            Assert.Equal("0900000001", root.GetProperty("soDienThoaiChiNhanh").GetString());
            Assert.Equal(100m, root.GetProperty("chiSoDienCu").GetDecimal());
            Assert.Equal(120m, root.GetProperty("chiSoDienMoi").GetDecimal());
            Assert.Equal(50m, root.GetProperty("chiSoNuocCu").GetDecimal());
            Assert.Equal(60m, root.GetProperty("chiSoNuocMoi").GetDecimal());
            Assert.False(root.TryGetProperty("hanThanhToan", out _));
            Assert.False(root.TryGetProperty("lichSuTrangThais", out _));
        }

        [Fact]
        public async Task XemChiTiet_DaGuiKhongCoHan_HanPhanHoiRong()
        {
            var data = await SeedTestDataAsync("XCT_NotFlag");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync($"/KhachThue/HoaDon/XemChiTiet?id={data.InvoiceDaGui.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            Assert.Equal(string.Empty, doc.RootElement.GetProperty("hanPhanHoi").GetString());
            Assert.False(doc.RootElement.TryGetProperty("laTamTinh", out _));
        }

        [Fact]
        public async Task IndexPage_ShowsResponseDeadlineLine_WithoutProvisionalLabel()
        {
            var data = await SeedTestDataAsync("IDX_TT");
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync("/KhachThue/HoaDon");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var html = await res.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Tạm tính", html);
            Assert.DoesNotContain("laTamTinh", html);
            Assert.Contains("Nếu thấy sai sót, vui lòng", html);
            Assert.Contains("res.hanPhanHoi", html);
        }

        [Fact]
        public async Task Dashboard_TotalDebt_DoesNotIncludeProvisionalInvoice()
        {
            var data = await SeedTestDataAsync("DSH_TT");
            await AddProvisionalInvoicesAsync(data.InvoiceDaGui);
            var client = CreateHttpClient();
            await LoginTenantAsync(client, data.TenantAUsername, data.TenantAPassword);

            var res = await client.GetAsync("/KhachThue/Dashboard");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var html = await res.Content.ReadAsStringAsync();
            // 3.000.000 (hóa đơn đã gửi) + 1.234.567 (tạm tính) = 4.234.567 không được xuất hiện ở bất kỳ định dạng nào.
            Assert.DoesNotContain("234.567", html);
            Assert.DoesNotContain("234,567", html);
            Assert.DoesNotContain("4234567", html);
        }
    }
}
