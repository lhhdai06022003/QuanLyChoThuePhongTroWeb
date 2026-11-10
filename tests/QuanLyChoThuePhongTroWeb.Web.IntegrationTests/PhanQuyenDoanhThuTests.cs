using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Doanh thu chỉ dành cho Admin: Bảng điều khiển, Báo cáo (tab Doanh thu) và phạm vi chi nhánh của Lịch sử thanh toán.
    [Collection(WebTestCollection.Name)]
    public class PhanQuyenDoanhThuTests
    {
        private const string Dashboard = "/QuanLyNhaTro/Dashboard";
        private const string BaoCao = "/QuanLyNhaTro/BaoCao";

        private readonly CustomWebApplicationFactory _factory;

        public PhanQuyenDoanhThuTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static string AjaxUrl(int branchId)
        {
            var vn = DateTime.UtcNow.AddHours(7);
            return $"{Dashboard}/GetAjaxData?branchId={branchId}&year={vn.Year}&month={vn.Month}";
        }

        private static async Task<JsonDocument> GetJsonAsync(System.Net.Http.HttpClient client, string url)
        {
            var res = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        }

        // Hóa đơn đã gửi kỳ lạ (3/2005) với một khoản tiền mặt ở hợp đồng.
        private Task SeedLedgerAsync(int hopDongId, decimal soTien) =>
            SeedLedgersAsync(hopDongId, (soTien, null, PhuongThucThanhToan.TienMat));

        private async Task SeedLedgersAsync(int hopDongId, params (decimal SoTien, string? MaGiaoDich, PhuongThucThanhToan PhuongThuc)[] rows)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = new HoaDon
            {
                HopDongId = hopDongId,
                MaHoaDon = $"PQ-{Guid.NewGuid():N}"[..20],
                Thang = 3,
                Nam = 2005,
                TongTien = 1_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan,
                NgayGui = DateTime.UtcNow
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            foreach (var row in rows)
            {
                db.LichSuThanhToans.Add(new LichSuThanhToan
                {
                    HoaDonId = invoice.HoaDonId,
                    MaGiaoDich = row.MaGiaoDich ?? $"PQ-{Guid.NewGuid():N}",
                    SoTienThanhToan = row.SoTien,
                    PhuongThucThanhToan = row.PhuongThuc,
                    NgayThanhToan = new DateTime(2005, 3, 10, 5, 0, 0, DateTimeKind.Utc),
                    NgayXacNhan = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }

        // Test 1
        [Fact]
        public async Task Dashboard_NhanVienKhongThayDoanhThu_AdminThay()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff, Dashboard);
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin, Dashboard);

            var staffHtml = WebUtility.HtmlDecode(await (await staff.GetAsync(Dashboard)).Content.ReadAsStringAsync());
            Assert.DoesNotContain("id=\"statDoanhThu\"", staffHtml);
            Assert.DoesNotContain("id=\"revenueBarChart\"", staffHtml);
            Assert.DoesNotContain("Xu hướng doanh thu", staffHtml);

            using (var doc = await GetJsonAsync(staff, AjaxUrl(w.BranchA)))
            {
                var root = doc.RootElement;
                Assert.False(root.GetProperty("coXemDoanhThu").GetBoolean());
                Assert.Equal(JsonValueKind.Null, root.GetProperty("doanhThuThangNay").ValueKind);
                Assert.Equal(JsonValueKind.Null, root.GetProperty("chartData").ValueKind);
                Assert.Equal(JsonValueKind.Null, root.GetProperty("chartDataChoThu").ValueKind);
            }

            var adminHtml = WebUtility.HtmlDecode(await (await admin.GetAsync(Dashboard)).Content.ReadAsStringAsync());
            Assert.Contains("id=\"revenueBarChart\"", adminHtml);
            Assert.Contains("id=\"statDoanhThu\"", adminHtml);

            using (var doc = await GetJsonAsync(admin, AjaxUrl(w.BranchA)))
            {
                var root = doc.RootElement;
                Assert.True(root.GetProperty("coXemDoanhThu").GetBoolean());
                Assert.Equal(JsonValueKind.Number, root.GetProperty("doanhThuThangNay").ValueKind);
                Assert.Equal(12, root.GetProperty("chartData").GetArrayLength());
            }
        }

        // Test 2
        [Fact]
        public async Task BaoCao_NhanVienBiChanDoanhThu403_CacTabKhacMo200()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff, BaoCao);

            foreach (var query in new[] { "", $"&chiNhanhId={w.BranchA}", "&tuThang=1&tuNam=2024&denThang=1&denNam=2026" })
            {
                var data = await staff.GetAsync($"{BaoCao}/DuLieu?tab=doanh-thu{query}");
                Assert.Equal(HttpStatusCode.Forbidden, data.StatusCode);
                Assert.Null(data.Headers.Location);

                var excel = await staff.GetAsync($"{BaoCao}/XuatExcel?tab=doanh-thu{query}");
                Assert.Equal(HttpStatusCode.Forbidden, excel.StatusCode);
                Assert.Null(excel.Headers.Location);
            }

            foreach (var tab in new[] { "cong-no", "cong-suat", "hop-dong", "dien-nuoc" })
            {
                Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"{BaoCao}/DuLieu?tab={tab}")).StatusCode);
            }

            foreach (var url in new[] { BaoCao, $"{BaoCao}?tab=doanh-thu" })
            {
                var html = (await (await staff.GetAsync(url)).Content.ReadAsStringAsync());
                Assert.Contains("tab: \"cong-no\"", html.Replace("\\u0022", "\""));
                Assert.DoesNotContain("id=\"tab-doanh-thu\"", html);
                Assert.DoesNotContain("id=\"pane-doanh-thu\"", html);
            }
        }

        [Fact]
        public async Task BaoCao_AdminVanThayTabDoanhThu()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (admin, _) = await BranchScopeKit.LoginAsync(_factory, w.Admin, BaoCao);

            var html = await (await admin.GetAsync(BaoCao)).Content.ReadAsStringAsync();

            Assert.Contains("id=\"tab-doanh-thu\"", html);
            Assert.Contains("id=\"pane-doanh-thu\"", html);
            Assert.Contains("tab: \"doanh-thu\"", html.Replace("\\u0022", "\""));
        }

        // Test 3 (nửa đầu): nhân viên chỉ thấy tổng của chi nhánh mình
        [Fact]
        public async Task LichSuThanhToan_NhanVienChiThayTongCuaChiNhanhMinh()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            await SeedLedgerAsync(w.ContractA, 100_000m);
            await SeedLedgerAsync(w.ContractB, 7_000_000m);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff, "/QuanLyNhaTro/LichSuThanhToan");

            using (var doc = await GetJsonAsync(staff, "/LichSuThanhToan/GetStats"))
            {
                Assert.Equal(1, doc.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(100_000m, doc.RootElement.GetProperty("tongDoanhThu").GetDecimal());
            }

            using (var doc = await GetJsonAsync(staff, $"/LichSuThanhToan/GetStats?chiNhanhId={w.BranchB}"))
            {
                Assert.Equal(0, doc.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
                Assert.Equal(0m, doc.RootElement.GetProperty("tongDoanhThu").GetDecimal());
            }
        }

        // Test 4/6 (HTTP): ô theo kênh, bộ lọc kênh bằng chữ.
        [Fact]
        public async Task LichSuThanhToan_AdminThayOSePayVnPay_VaLocKenhSePayChiTraDongSePay()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            await SeedLedgersAsync(w.ContractA,
                (100_000m, null, PhuongThucThanhToan.TienMat),
                (20_000m, "SEPAY-" + Guid.NewGuid().ToString("N"), PhuongThucThanhToan.ChuyenKhoan),
                (3_000m, "VNPAY-" + Guid.NewGuid().ToString("N"), PhuongThucThanhToan.ChuyenKhoan));
            var (admin, token) = await BranchScopeKit.LoginAsync(_factory, w.Admin, "/QuanLyNhaTro/LichSuThanhToan");

            using (var doc = await GetJsonAsync(admin, $"/LichSuThanhToan/GetStats?chiNhanhId={w.BranchA}"))
            {
                var root = doc.RootElement;
                Assert.Equal(100_000m, root.GetProperty("doanhThuTienMat").GetDecimal());
                Assert.Equal(0m, root.GetProperty("doanhThuChuyenKhoan").GetDecimal());
                Assert.Equal(20_000m, root.GetProperty("doanhThuSePay").GetDecimal());
                Assert.Equal(3_000m, root.GetProperty("doanhThuVnPay").GetDecimal());
                Assert.Equal(123_000m, root.GetProperty("tongDoanhThu").GetDecimal());
            }

            var list = await BranchScopeKit.PostFormAsync(admin, token, "/LichSuThanhToan/GetList", new System.Collections.Generic.Dictionary<string, string>
            {
                ["chiNhanhId"] = w.BranchA.ToString(),
                ["phuongThuc"] = "sepay",
                ["draw"] = "1",
                ["start"] = "0",
                ["length"] = "50"
            });
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            Assert.Equal(1, listDoc.RootElement.GetProperty("recordsTotal").GetInt32());
            Assert.StartsWith("SEPAY-", listDoc.RootElement.GetProperty("data")[0].GetProperty("maGiaoDich").GetString());
        }

        [Fact]
        public async Task Dashboard_NhanVienKhongCoODaThuKy()
        {
            var w = await BranchScopeKit.SeedAsync(_factory);
            var (staff, _) = await BranchScopeKit.LoginAsync(_factory, w.Staff, Dashboard);

            var html = WebUtility.HtmlDecode(await (await staff.GetAsync(Dashboard)).Content.ReadAsStringAsync());

            Assert.DoesNotContain("Đã thu kỳ", html);
            Assert.Contains("Chờ thu kỳ", html);
        }
    }
}
