using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Trang Báo cáo thống kê: phân quyền chi nhánh, JSON, xuất Excel, menu, link từ Dashboard và lọc khoảng ngày màn Lịch sử thanh toán.
    [Collection(WebTestCollection.Name)]
    public class BaoCaoPageTests
    {
        private const string Page = "/QuanLyNhaTro/BaoCao";
        private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly CustomWebApplicationFactory _factory;

        public BaoCaoPageTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static (int Thang, int Nam) KyHienTaiVn()
        {
            var vn = DateTime.UtcNow.AddHours(7);
            return (vn.Month, vn.Year);
        }

        // Hóa đơn đã gửi kỳ tháng hiện tại (giờ VN) ở hợp đồng A, đã thu 400.000 bằng tiền mặt.
        private async Task SeedInvoiceWithCashAsync(int hopDongId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var (thang, nam) = KyHienTaiVn();
            var invoice = new HoaDon
            {
                HopDongId = hopDongId,
                MaHoaDon = $"BC-{Guid.NewGuid():N}"[..20],
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
                MaGiaoDich = $"BC-{Guid.NewGuid():N}"[..24],
                SoTienThanhToan = 400_000m,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                NgayThanhToan = DateTime.UtcNow,
                NgayXacNhan = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task ChuaDangNhap_BiChuyenVeTrangDangNhap()
        {
            var client = BranchScopeKit.NewClient(_factory);

            var res = await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu");

            Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", res.Headers.Location!.ToString());
        }

        [Fact]
        public async Task NhanVien_ChiXemDuocChiNhanhCuaMinh()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&chiNhanhId={world.BranchA}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&chiNhanhId={world.BranchB}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Page}/DuLieu?tab=cong-no")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Page}/XuatExcel?tab=doanh-thu&chiNhanhId={world.BranchB}")).StatusCode);
        }

        [Fact]
        public async Task ThamSoSai_Tra400()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Staff, Page);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/DuLieu?tab=xyz")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/DuLieu")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/DuLieu?tab=doanh-thu&tuThang=1&tuNam=2024&denThang=1&denNam=2026")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Page}/XuatExcel?tab=xyz")).StatusCode);
        }

        [Fact]
        public async Task Admin_MoTrang_MenuBaoCaoActive_VaCoBoLoc()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            var res = await client.GetAsync($"{Page}?tab=cong-no");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

            Assert.Matches(@"<a[^>]*class=""dropdown-item[^""]*active[^""]*""[^>]*href=""/QuanLyNhaTro/BaoCao""", html);
            Assert.Contains("id=\"bcChiNhanh\"", html);
            Assert.Contains("Tất cả chi nhánh", html);
            Assert.Contains("id=\"tab-dien-nuoc\"", html);
            Assert.Contains("window.baoCaoConfig", html);
        }

        [Fact]
        public async Task TabLa_VeDoanhThu()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            var html = await (await client.GetAsync($"{Page}?tab=khong-co")).Content.ReadAsStringAsync();

            Assert.Contains("tab: \"doanh-thu\"", html.Replace("\\u0022", "\""));
        }

        [Fact]
        public async Task Admin_DuLieuVaXuatExcel_CungSoLieu()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            await SeedInvoiceWithCashAsync(world.ContractA);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);
            var query = $"tab=doanh-thu&chiNhanhId={world.BranchA}";

            var json = await client.GetAsync($"{Page}/DuLieu?{query}");
            Assert.Equal(HttpStatusCode.OK, json.StatusCode);
            using var doc = JsonDocument.Parse(await json.Content.ReadAsStringAsync());
            var daThu = doc.RootElement.GetProperty("tong").GetProperty("daThu").GetDecimal();
            Assert.Equal(400_000m, daThu);
            Assert.Equal(600_000m, doc.RootElement.GetProperty("tong").GetProperty("conNo").GetDecimal());

            var excel = await client.GetAsync($"{Page}/XuatExcel?{query}");
            Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
            Assert.Equal(ExcelContentType, excel.Content.Headers.ContentType!.MediaType);
            Assert.Matches(@"BaoCao_doanh-thu_\d{6}_\d{6}\.xlsx", excel.Content.Headers.ContentDisposition!.FileNameStar ?? excel.Content.Headers.ContentDisposition.FileName!);

            using var wb = new XLWorkbook(new MemoryStream(await excel.Content.ReadAsByteArrayAsync()));
            var cell = wb.Worksheet("Tổng hợp").Column(1).CellsUsed().First(c => c.GetString() == "Đã thu").CellRight();
            Assert.Equal((double)daThu, cell.GetDouble());
        }

        [Fact]
        public async Task Admin_TaiDuocCongNoVaCacTabKhacDuoiDangExcel()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, Page);

            foreach (var tab in new[] { "cong-no", "cong-suat", "hop-dong", "dien-nuoc" })
            {
                var data = await client.GetAsync($"{Page}/DuLieu?tab={tab}&chiNhanhId={world.BranchA}");
                Assert.Equal(HttpStatusCode.OK, data.StatusCode);

                var excel = await client.GetAsync($"{Page}/XuatExcel?tab={tab}&chiNhanhId={world.BranchA}");
                Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
                using var wb = new XLWorkbook(new MemoryStream(await excel.Content.ReadAsByteArrayAsync()));
                Assert.Equal("Tổng hợp", wb.Worksheets.First().Name);
            }
        }

        [Fact]
        public async Task Dashboard_CoLinkSangBaoCaoDoanhThu()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, "/QuanLyNhaTro/Dashboard");

            var html = WebUtility.HtmlDecode(await (await client.GetAsync("/QuanLyNhaTro/Dashboard")).Content.ReadAsStringAsync());

            Assert.Contains("href=\"/QuanLyNhaTro/BaoCao?tab=doanh-thu\"", html);
        }

        // ----- C3: khoảng ngày của màn Lịch sử thanh toán -----

        private async Task<int> SeedLedgerPairAsync(int hopDongId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = new HoaDon
            {
                HopDongId = hopDongId,
                MaHoaDon = $"LS-{Guid.NewGuid():N}"[..20],
                Thang = 3,
                Nam = 2003,
                TongTien = 1_000_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan,
                NgayGui = DateTime.UtcNow
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            LichSuThanhToan Row(decimal soTien, DateTime ngay) => new()
            {
                HoaDonId = invoice.HoaDonId,
                MaGiaoDich = $"LS-{Guid.NewGuid():N}",
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                NgayThanhToan = ngay,
                NgayXacNhan = DateTime.UtcNow
            };
            // 09/03/2003 23:59:59 giờ VN và 10/03/2003 00:00:00 giờ VN.
            db.LichSuThanhToans.Add(Row(100m, new DateTime(2003, 3, 9, 16, 59, 59, DateTimeKind.Utc)));
            db.LichSuThanhToans.Add(Row(200m, new DateTime(2003, 3, 9, 17, 0, 0, DateTimeKind.Utc)));
            await db.SaveChangesAsync();
            return invoice.HoaDonId;
        }

        [Theory]
        [InlineData("2003-03-09 to 2003-03-09", 1, 100)]
        [InlineData("2003-03-09 đến 2003-03-09", 1, 100)]
        [InlineData("2003-03-10 đến 2003-03-10", 1, 200)]
        [InlineData("2003-03-09 đến 2003-03-10", 2, 300)]
        [InlineData("2003-03-10", 1, 200)]
        public async Task LichSuThanhToan_GetStatsVaGetList_AdDungKhoangNgay(string dateRange, int soGiaoDich, int tong)
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            await SeedLedgerPairAsync(world.ContractA);
            var (client, token) = await BranchScopeKit.LoginAsync(_factory, world.Admin, "/QuanLyNhaTro/LichSuThanhToan");

            var stats = await client.GetAsync($"/LichSuThanhToan/GetStats?chiNhanhId={world.BranchA}&dateRange={Uri.EscapeDataString(dateRange)}");
            Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
            using var statsDoc = JsonDocument.Parse(await stats.Content.ReadAsStringAsync());
            Assert.Equal(soGiaoDich, statsDoc.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
            Assert.Equal(tong, statsDoc.RootElement.GetProperty("tongDoanhThu").GetDecimal());

            var list = await BranchScopeKit.PostFormAsync(client, token, "/LichSuThanhToan/GetList", new Dictionary<string, string>
            {
                ["chiNhanhId"] = world.BranchA.ToString(),
                ["dateRange"] = dateRange,
                ["draw"] = "1",
                ["start"] = "0",
                ["length"] = "50"
            });
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            Assert.Equal(soGiaoDich, listDoc.RootElement.GetProperty("recordsTotal").GetInt32());
        }

        [Fact]
        public async Task LichSuThanhToan_KhongCoKhoangNgay_TinhTatCa()
        {
            var world = await BranchScopeKit.SeedAsync(_factory);
            await SeedLedgerPairAsync(world.ContractA);
            var (client, _) = await BranchScopeKit.LoginAsync(_factory, world.Admin, "/QuanLyNhaTro/LichSuThanhToan");

            var stats = await client.GetAsync($"/LichSuThanhToan/GetStats?chiNhanhId={world.BranchA}");
            using var doc = JsonDocument.Parse(await stats.Content.ReadAsStringAsync());

            Assert.Equal(2, doc.RootElement.GetProperty("tongSoGiaoDich").GetInt32());
            Assert.Equal(0m, doc.RootElement.GetProperty("doanhThuCanTruCoc").GetDecimal());
        }
    }
}
