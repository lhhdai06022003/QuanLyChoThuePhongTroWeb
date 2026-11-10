using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Lịch sử thanh toán: lọc kỳ hóa đơn (chỉ Admin), người thu, tham số URL, và link từ Báo cáo.
    [Collection(WebTestCollection.Name)]
    public class LichSuThanhToanLocTests
    {
        private const string Page = "/QuanLyNhaTro/LichSuThanhToan";

        private readonly CustomWebApplicationFactory _factory;

        public LichSuThanhToanLocTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Hóa đơn đã gửi kỳ (thang, nam) của hợp đồng với một khoản tiền mặt.
        private async Task SeedAsync(int hopDongId, int thang, int nam, decimal soTien)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = new HoaDon
            {
                HopDongId = hopDongId,
                MaHoaDon = $"LK-{Guid.NewGuid():N}"[..20],
                Thang = thang,
                Nam = nam,
                TongTien = 1_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan,
                NgayGui = DateTime.UtcNow
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            db.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = invoice.HoaDonId,
                MaGiaoDich = $"LK-{Guid.NewGuid():N}",
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                NgayThanhToan = new DateTime(2033, 6, 1, 5, 0, 0, DateTimeKind.Utc),
                NgayXacNhan = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        private static async Task<JsonDocument> StatsAsync(System.Net.Http.HttpClient client, string query)
        {
            var res = await client.GetAsync("/LichSuThanhToan/GetStats" + query);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        }

        private static async Task<JsonDocument> ListAsync(System.Net.Http.HttpClient client, string token, Dictionary<string, string> form)
        {
            form["draw"] = "1";
            form["start"] = "0";
            form["length"] = "50";
            var res = await BranchScopeKit.PostFormAsync(client, token, "/LichSuThanhToan/GetList", form);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        }

        // Test 3 (nửa sau)
        [Fact]
        public async Task Ky_NhanVienGuiKyBiBoQua_AdminLocDuocTheoKy()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            await SeedAsync(w.ContractA, 3, 2033, 100_000m);
            await SeedAsync(w.ContractA, 4, 2033, 20_000m);
            var (staff, staffToken) = await BranchScopeKit.LoginAsync(_factory, w.Staff, Page);
            var (admin, adminToken) = await BranchScopeKit.LoginAsync(_factory, w.Admin, Page);

            using (var khongKy = await StatsAsync(staff, ""))
            using (var coKy = await StatsAsync(staff, "?kyThang=3&kyNam=2033"))
            {
                Assert.Equal(2, khongKy.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(khongKy.RootElement.GetProperty("tongSoGiaoDich").GetInt32(), coKy.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(120_000m, coKy.RootElement.GetProperty("tongDoanhThu").GetDecimal());
                Assert.False(coKy.RootElement.GetProperty("locTheoKy").GetBoolean());
            }

            using (var khongKy = await ListAsync(staff, staffToken, new Dictionary<string, string>()))
            using (var coKy = await ListAsync(staff, staffToken, new Dictionary<string, string> { ["kyThang"] = "3", ["kyNam"] = "2033" }))
            {
                Assert.Equal(khongKy.RootElement.GetProperty("recordsTotal").GetInt32(), coKy.RootElement.GetProperty("recordsTotal").GetInt32());
                Assert.Equal(2, coKy.RootElement.GetProperty("recordsTotal").GetInt32());
            }

            using (var stats = await StatsAsync(admin, $"?chiNhanhId={w.BranchA}&kyThang=3&kyNam=2033"))
            {
                Assert.Equal(1, stats.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(100_000m, stats.RootElement.GetProperty("tongDoanhThu").GetDecimal());
                Assert.True(stats.RootElement.GetProperty("locTheoKy").GetBoolean());
                Assert.Equal(3, stats.RootElement.GetProperty("kyThang").GetInt32());
                Assert.Equal(2033, stats.RootElement.GetProperty("kyNam").GetInt32());
            }

            using (var list = await ListAsync(admin, adminToken, new Dictionary<string, string>
            {
                ["chiNhanhId"] = w.BranchA.ToString(), ["kyThang"] = "3", ["kyNam"] = "2033"
            }))
            {
                Assert.Equal(1, list.RootElement.GetProperty("recordsTotal").GetInt32());
            }
        }

        [Fact]
        public async Task NguoiThu_HeThongLocDuocQuaHttp()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            await SeedAsync(w.ContractA, 5, 2033, 55_000m); // khoản không có người xác nhận
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin, Page);

            using var heThong = await StatsAsync(admin, $"?chiNhanhId={w.BranchA}&nguoiThu=he-thong");
            using var toi = await StatsAsync(admin, $"?chiNhanhId={w.BranchA}&nguoiThu=toi");

            Assert.Equal(55_000m, heThong.RootElement.GetProperty("tongDoanhThu").GetDecimal());
            Assert.Equal(0m, toi.RootElement.GetProperty("tongDoanhThu").GetDecimal());
        }

        // Test 9
        [Fact]
        public async Task TrangLichSuThanhToan_NhanThamSoUrl_AdminCoKy_NhanVienKhong()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin, Page);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff, Page);
            var url = $"{Page}?kyThang=3&kyNam=2026&nguoiThu=toi&phuongThuc=tien-mat&tuNgay=2026-11-01&denNgay=2026-11-30&chiNhanhId={w.BranchA}";

            var adminRes = await admin.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);
            var adminHtml = Chuan(await adminRes.Content.ReadAsStringAsync());
            Assert.Contains("window.lichSuThanhToanConfig", adminHtml);
            Assert.Contains("laAdmin: true", adminHtml);
            Assert.Contains($"chiNhanhId: {w.BranchA}", adminHtml);
            Assert.Contains("phuongThuc: \"tien-mat\"", adminHtml);
            Assert.Contains("nguoiThu: \"toi\"", adminHtml);
            Assert.Contains("kyThang: 3", adminHtml);
            Assert.Contains("kyNam: 2026", adminHtml);
            Assert.Contains("tuNgay: \"2026-11-01\"", adminHtml);
            Assert.Contains("denNgay: \"2026-11-30\"", adminHtml);
            Assert.Contains("id=\"filterKy\"", adminHtml);
            Assert.Contains("value=\"2026-03\"", adminHtml);

            var staffRes = await staff.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, staffRes.StatusCode);
            var staffHtml = Chuan(await staffRes.Content.ReadAsStringAsync());
            Assert.Contains("kyThang: null", staffHtml);
            Assert.Contains("kyNam: null", staffHtml);
            Assert.Contains("laAdmin: false", staffHtml);
            Assert.DoesNotContain("id=\"filterKy\"", staffHtml);
            Assert.Contains("nguoiThu: \"toi\"", staffHtml);
        }

        [Fact]
        public async Task TrangLichSuThanhToan_ThamSoRac_Van200_VaBoThamSoSai()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin, Page);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff, Page);

            foreach (var client in new[] { admin, staff })
            {
                var res = await client.GetAsync($"{Page}?kyThang=abc&kyNam=zz&tuNgay=31/12/2026&denNgay=x&phuongThuc=xyz&nguoiThu=%3Cb%3EXSS%3C/b%3E&chiNhanhId=99999999");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);
                var html = Chuan(await res.Content.ReadAsStringAsync());
                Assert.Contains("kyThang: null", html);
                Assert.Contains("phuongThuc: \"\"", html);
                Assert.Contains("nguoiThu: \"\"", html);
                Assert.Contains("tuNgay: \"\"", html);
                Assert.Contains("chiNhanhId: 0", html);
                Assert.DoesNotContain("XSS", html);
            }
        }

        [Fact]
        public async Task TrangLichSuThanhToan_KhoangNgayDaoNguoc_BiBo()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin, Page);

            var html = Chuan(await (await admin.GetAsync($"{Page}?tuNgay=2026-12-31&denNgay=2026-01-01")).Content.ReadAsStringAsync());

            Assert.Contains("tuNgay: \"\"", html);
            Assert.Contains("denNgay: \"\"", html);
        }

        [Fact]
        public async Task BaoCao_AdminMoVoiKhoangKyTrenUrl_KhoangSaiThiDungMacDinh()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin, "/QuanLyNhaTro/BaoCao");

            var coKhoang = await (await admin.GetAsync("/QuanLyNhaTro/BaoCao?tab=doanh-thu&tuThang=3&tuNam=2026&denThang=3&denNam=2026")).Content.ReadAsStringAsync();
            Assert.Matches(new Regex("id=\"bcTuKy\"[^>]*value=\"2026-03\""), coKhoang);
            Assert.Matches(new Regex("id=\"bcDenKy\"[^>]*value=\"2026-03\""), coKhoang);

            var vn = DateTime.UtcNow.AddHours(7);
            var den = new DateTime(vn.Year, vn.Month, 1);
            var tu = den.AddMonths(-11);
            var khoangSai = await (await admin.GetAsync("/QuanLyNhaTro/BaoCao?tab=doanh-thu&tuThang=13&tuNam=2026&denThang=3&denNam=2026")).Content.ReadAsStringAsync();
            Assert.Contains($"value=\"{tu.ToString("yyyy-MM", CultureInfo.InvariantCulture)}\"", khoangSai);
            Assert.Contains($"value=\"{den.ToString("yyyy-MM", CultureInfo.InvariantCulture)}\"", khoangSai);

            var thieu = await (await admin.GetAsync("/QuanLyNhaTro/BaoCao?tab=doanh-thu&tuThang=3&tuNam=2026")).Content.ReadAsStringAsync();
            Assert.Contains($"value=\"{tu.ToString("yyyy-MM", CultureInfo.InvariantCulture)}\"", thieu);

            var html = await (await admin.GetAsync("/QuanLyNhaTro/BaoCao")).Content.ReadAsStringAsync();
            Assert.Contains("urlLichSuThanhToan", html);
            var js = await (await admin.GetAsync("/js/bao-cao.js")).Content.ReadAsStringAsync();
            Assert.Contains("Xem giao dịch", js);
        }

        // Q4: nhân viên tự gửi nguoiThu là id người khác thì máy chủ vẫn nhận, nhưng chỉ trong chi nhánh của mình.
        [Fact]
        public async Task NguoiThu_NhanVienGuiIdNguoiKhac_VanChiTrongChiNhanhCuaMinh()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            await SeedAsync(w.ContractA, 5, 2033, 40_000m);
            await SeedAsync(w.ContractB, 5, 2033, 9_000_000m);
            int adminId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                adminId = db.NguoiDungs.Single(u => u.TenDangNhap == w.Admin).NguoiDungId;
                // Gán người xác nhận cho đúng hai khoản vừa seed (khoản của hợp đồng A và B).
                foreach (var l in db.LichSuThanhToans.Where(l => l.HoaDon.HopDongId == w.ContractA || l.HoaDon.HopDongId == w.ContractB))
                {
                    l.NguoiXacNhanId = adminId;
                }

                await db.SaveChangesAsync();
            }

            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff, Page);

            using (var cuaMinh = await StatsAsync(staff, $"?nguoiThu={adminId}"))
            {
                Assert.Equal(1, cuaMinh.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(40_000m, cuaMinh.RootElement.GetProperty("tongDoanhThu").GetDecimal());
            }

            using (var chiNhanhKhac = await StatsAsync(staff, $"?nguoiThu={adminId}&chiNhanhId={w.BranchB}"))
            {
                Assert.Equal(0, chiNhanhKhac.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(0m, chiNhanhKhac.RootElement.GetProperty("tongDoanhThu").GetDecimal());
            }
        }

        // Fail closed qua HTTP: nhân viên chưa được phân công chi nhánh nào không thấy tiền ở đâu, doanh thu bị 403.
        [Fact]
        public async Task NhanVienChuaPhanCong_KhongThayTien_DoanhThu403()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            await SeedAsync(w.ContractA, 6, 2033, 70_000m);
            var ten = $"nvtrong_{Guid.NewGuid():N}"[..16];
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services.INguoiDungService>();
                Assert.True((await users.AddAsync(new QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs.CreateNguoiDungReq
                {
                    TenDangNhap = ten,
                    MatKhau = BranchScopeKit.Password,
                    Role = QuanLyChoThuePhongTroWeb.Application.Common.Enums.AppRole.NhanVien
                })).Success);
            }

            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, ten, "/QuanLyNhaTro/Dashboard");

            using (var stats = await StatsAsync(staff, $"?kyThang=6&kyNam=2033&chiNhanhId={w.BranchA}"))
            {
                Assert.Equal(0, stats.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(0m, stats.RootElement.GetProperty("tongDoanhThu").GetDecimal());
                Assert.False(stats.RootElement.GetProperty("locTheoKy").GetBoolean());
            }

            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/QuanLyNhaTro/BaoCao/DuLieu?tab=doanh-thu")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/QuanLyNhaTro/BaoCao/XuatExcel?tab=doanh-thu")).StatusCode);

            var ajax = await staff.GetAsync("/QuanLyNhaTro/Dashboard/GetAjaxData?year=2033&month=6");
            Assert.Equal(HttpStatusCode.OK, ajax.StatusCode);
            using var doc = JsonDocument.Parse(await ajax.Content.ReadAsStringAsync());
            Assert.False(doc.RootElement.GetProperty("coXemDoanhThu").GetBoolean());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("doanhThuThangNay").ValueKind);
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("tienMatNhanVien").ValueKind);
        }

        private static string Chuan(string html) => WebUtility.HtmlDecode(html.Replace("\\u0022", "\""));
    }
}
