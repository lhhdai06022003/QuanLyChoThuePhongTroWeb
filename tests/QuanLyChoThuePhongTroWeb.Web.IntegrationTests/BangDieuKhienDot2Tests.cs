using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Common.Formatting;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Bảng điều khiển đợt 2: thanh thu tiền của Admin, ô số liệu nhạt, màu biểu đồ theo biến.
    [Collection(WebTestCollection.Name)]
    public class BangDieuKhienDot2Tests
    {
        private const string Dashboard = "/QuanLyNhaTro/Dashboard";

        // Id có ở cả hai vai trò.
        private static readonly string[] IdChung =
        {
            "statChoThu", "statQuaHan", "statSoHoaDonQuaHan", "statChuaThanhToan", "cardChoThu", "cardQuaHan", "cardPhongDangNo",
            "kpiChoThuTitle", "viecCanLamList", "viecCanLamChips", "viecCanLamFooter", "viecXemTatCa", "viecTongSo", "viecSoKhan",
            "viecSoCanLam", "viecSoTheoDoi", "activitiesContainer", "roomStatusChart", "branchSelect", "monthSelect", "yearSelect",
            "dashboardLoading", "statHopDong", "statTyLeLapDay", "statPhongLapDay"
        };

        private static readonly string[] IdChiAdmin =
        {
            "thuTienCard", "thuTienPhaiThu", "thuTienPhanTram", "thuTienThanh", "thuTienDoanDaThu", "thuTienDoanChoThu",
            "statDoanhThu", "statDaThuO", "cardDaThu", "kpiDaThuTitle", "kpiDaThuNhan", "choDuyetCard", "choDuyetHd2", "choDuyetTongSo",
            "tienMatCard", "tienMatBang", "tienMatBody", "tienMatFoot", "tienMatTongKhoan", "tienMatTongTien", "tienMatKy",
            "viecToggle", "viecCanLamBody", "revenueBarChart", "linkBaoCaoNam", "chartYearText"
        };

        private static readonly string[] IdChiNhanVien =
        {
            "statTienMat", "cardTienMat", "kpiTienMatTitle", "statTienMatChuThich"
        };

        private readonly CustomWebApplicationFactory _factory;

        public BangDieuKhienDot2Tests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static async Task<string> HtmlAsync(HttpClient client, string url)
        {
            var res = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        }

        // Số lần id="x" xuất hiện (không tính thuộc tính kết thúc bằng id như data-id).
        private static int DemId(string html, string id) =>
            Regex.Matches(html, $"(?<![\\w-])id=\"{Regex.Escape(id)}\"").Count;

        private static void CoDungMotLan(string html, params string[][] nhomId)
        {
            foreach (var id in nhomId.SelectMany(n => n))
            {
                var dem = DemId(html, id);
                Assert.True(dem == 1, $"id=\"{id}\" xuất hiện {dem} lần, cần đúng 1");
            }
        }

        // Nội dung chữ của phần tử có id (đến thẻ kế tiếp).
        private static string NoiDung(string html, string id)
        {
            var m = Regex.Match(html, $"(?<![\\w-])id=\"{Regex.Escape(id)}\"[^>]*>([^<]*)<");
            Assert.True(m.Success, $"Không tìm thấy phần tử id=\"{id}\"");
            return m.Groups[1].Value.Trim();
        }

        private async Task ThemHoaDonCoMotKhoanTienMatAsync(int hopDongId, int thang, int nam, string tenAdmin, decimal tong, decimal daTra)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var adminId = await db.NguoiDungs.Where(u => u.TenDangNhap == tenAdmin).Select(u => u.NguoiDungId).SingleAsync();
            var invoice = new HoaDon
            {
                MaHoaDon = $"BD{Guid.NewGuid():N}"[..20],
                HopDongId = hopDongId,
                Thang = thang,
                Nam = nam,
                TongTien = tong,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan,
                NgayGui = DateTime.UtcNow
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            db.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = invoice.HoaDonId,
                MaGiaoDich = $"TM-{Guid.NewGuid():N}",
                SoTienThanhToan = daTra,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                NgayThanhToan = DateTime.UtcNow,
                NgayXacNhan = DateTime.UtcNow,
                NguoiXacNhanId = adminId
            });
            await db.SaveChangesAsync();
        }

        private static string ThuMucGoc()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return dir!.FullName;
        }

        [Fact]
        public async Task Admin_CoThanhThuTien_PhanTramDungVaDuIdCu()
        {
            var w = await SeedAsync(_factory);
            var vn = DateTime.UtcNow.AddHours(7);
            await ThemHoaDonCoMotKhoanTienMatAsync(w.ContractA, vn.Month, vn.Year, w.Admin, 400_000m, 100_000m);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = await HtmlAsync(admin, $"{Dashboard}?branchId={w.BranchA}&month={vn.Month}&year={vn.Year}");

            Assert.Contains("id=\"thuTienCard\"", html);
            Assert.Contains("id=\"thuTienThanh\"", html);
            Assert.Contains("id=\"thuTienPhanTram\"", html);
            Assert.Equal("đã thu, 25%", NoiDung(html, "thuTienPhanTram"));
            Assert.Equal(TienTe.DinhDang(400_000m), NoiDung(html, "thuTienPhaiThu"));
            Assert.Contains($"Đã thu kỳ T{vn.Month}/{vn.Year}", html);
            CoDungMotLan(html, IdChung, IdChiAdmin);
            foreach (var id in IdChiNhanVien)
            {
                Assert.Equal(0, DemId(html, id));
            }
        }

        [Fact]
        public async Task Admin_PhaiThuBang0_PhanTram0()
        {
            var w = await SeedAsync(_factory);
            var vn = DateTime.UtcNow.AddHours(7);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = await HtmlAsync(admin, $"{Dashboard}?branchId={w.BranchB}&month={vn.Month}&year={vn.Year}");

            Assert.Equal("đã thu, 0%", NoiDung(html, "thuTienPhanTram"));
            Assert.Contains("id=\"thuTienDoanDaThu\"", html);
            Assert.Contains("width: 0%", html);
        }

        [Fact]
        public async Task NhanVien_KhongCoThanhThuTien_KhongCoDoanhThu()
        {
            var w = await SeedAsync(_factory);
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var html = await HtmlAsync(staff, Dashboard);

            Assert.DoesNotContain("id=\"thuTienCard\"", html);
            Assert.DoesNotContain("id=\"statDoanhThu\"", html);
            Assert.DoesNotContain("id=\"statDaThuO\"", html);
            Assert.DoesNotContain("Đã thu kỳ", html);
            CoDungMotLan(html, IdChung, IdChiNhanVien);
            foreach (var id in IdChiAdmin)
            {
                Assert.Equal(0, DemId(html, id));
            }
        }

        [Fact]
        public void View_KhongConONenMauDac()
        {
            var goc = Path.Combine(ThuMucGoc(), "src", "QuanLyChoThuePhongTroWeb.Web");
            var view = File.ReadAllText(Path.Combine(goc, "Areas", "QuanLyNhaTro", "Views", "Dashboard", "Index.cshtml"));
            foreach (var chuoi in new[] { "bg-success", "bg-danger", "bg-warning", "bg-pink", "text-white" })
            {
                Assert.DoesNotContain(chuoi, view);
            }

            var js = File.ReadAllText(Path.Combine(goc, "wwwroot", "js", "dashboard.js"));
            foreach (var chuoi in new[] { "#2fb344", "#f59f00", "#206bc4" })
            {
                Assert.DoesNotContain(chuoi, js);
            }
            Assert.Contains("--ts-ok", js);
            Assert.Contains("--ts-wait", js);
        }
    }
}
