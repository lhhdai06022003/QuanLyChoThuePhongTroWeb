using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Bảng điều khiển theo vai trò: khối Chờ bạn duyệt, liên kết của các ô số, tiền mặt theo người thu, bộ lọc "Chưa thu đủ".
    [Collection(WebTestCollection.Name)]
    public class BangDieuKhienTheoVaiTroTests
    {
        private const string Dashboard = "/QuanLyNhaTro/Dashboard";

        private readonly CustomWebApplicationFactory _factory;

        public BangDieuKhienTheoVaiTroTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static string Ajax(int? branchId, int month, int year) =>
            $"{Dashboard}/GetAjaxData?{(branchId.HasValue ? $"branchId={branchId}&" : string.Empty)}year={year}&month={month}";

        private static async Task<JsonDocument> JsonAsync(System.Net.Http.HttpClient client, string url)
        {
            var res = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            return JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        }

        private async Task<string> HtmlAsync(System.Net.Http.HttpClient client, string url) =>
            WebUtility.HtmlDecode(await (await client.GetAsync(url)).Content.ReadAsStringAsync());

        private async Task<int> AddInvoiceAsync(int hopDongId, int thang, int nam, TrangThaiPhatHanhHoaDon phatHanh, TrangThaiHoaDon thanhToan, decimal tong = 100_000m)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var invoice = new HoaDon
            {
                MaHoaDon = $"BD{Guid.NewGuid():N}"[..20],
                HopDongId = hopDongId,
                Thang = thang,
                Nam = nam,
                TongTien = tong,
                TrangThaiPhatHanh = phatHanh,
                TrangThaiHoaDon = thanhToan,
                NgayGui = phatHanh == TrangThaiPhatHanhHoaDon.DaGui ? DateTime.UtcNow : null
            };
            db.HoaDons.Add(invoice);
            await db.SaveChangesAsync();
            return invoice.HoaDonId;
        }

        // Test 8
        [Fact]
        public async Task ChoBanDuyet_ChiAdminThay_VaHd2RoiKhoiViecCanLam()
        {
            var w = await SeedAsync(_factory);
            await AddInvoiceAsync(w.ContractA, 1, 2030, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan);
            var (admin, _) = await LoginAsync(_factory, w.Admin);
            var (staff, _) = await LoginAsync(_factory, w.Staff);
            var vn = DateTime.UtcNow.AddHours(7);

            using (var doc = await JsonAsync(admin, Ajax(w.BranchA, vn.Month, vn.Year)))
            {
                var root = doc.RootElement;
                var choDuyet = root.GetProperty("choDuyet");
                Assert.Equal(1, choDuyet.GetProperty("soLuong").GetInt32());
                Assert.StartsWith("/QuanLyNhaTro/QuanLyHoaDon", choDuyet.GetProperty("link").GetString());
                Assert.DoesNotContain(root.GetProperty("viecCanLam").GetProperty("items").EnumerateArray(), i => i.GetProperty("maViec").GetString() == "HD2");
                Assert.NotEqual(JsonValueKind.Null, root.GetProperty("tienMatNhanVien").ValueKind);
            }

            using (var doc = await JsonAsync(staff, Ajax(null, vn.Month, vn.Year)))
            {
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("choDuyet").ValueKind);
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("tienMatNhanVien").ValueKind);
            }

            var adminHtml = await HtmlAsync(admin, $"{Dashboard}?branchId={w.BranchA}");
            Assert.Contains("Chờ bạn duyệt", adminHtml);
            Assert.Contains("Đối soát giao dịch SePay", adminHtml);
            Assert.Contains("href=\"/QuanLyNhaTro/GiaoDichSePay\"", adminHtml);

            var staffHtml = await HtmlAsync(staff, Dashboard);
            Assert.DoesNotContain("Chờ bạn duyệt", staffHtml);
            Assert.DoesNotContain("Đối soát giao dịch SePay", staffHtml);
        }

        [Fact]
        public async Task ChoBanDuyet_KhongCoHd2_HienDongKhongCoViecChoDuyet_VaVanCoLoiTatSePay()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = await HtmlAsync(admin, $"{Dashboard}?branchId={w.BranchB}");

            Assert.Contains("Chờ bạn duyệt", html);
            Assert.Contains("Không có việc chờ duyệt", html);
            Assert.Contains("href=\"/QuanLyNhaTro/GiaoDichSePay\"", html);
        }

        // Test 9
        [Fact]
        public async Task LienKetCuaCacOSo_DungThangChiNhanh_HtmlTrungJson()
        {
            var w = await SeedAsync(_factory);
            var (staff, _) = await LoginAsync(_factory, w.Staff);
            var (admin, _) = await LoginAsync(_factory, w.Admin);
            var a = w.BranchA;

            var html = await HtmlAsync(staff, $"{Dashboard}?branchId={a}&month=11&year=2026");
            var quaHan = $"/QuanLyNhaTro/ViecCanLam?chiNhanhId={a}#hoa-don-qua-han";
            var choThu = $"/QuanLyNhaTro/QuanLyHoaDon?thang=11&nam=2026&trangThaiPhatHanh=3&trangThai=-2&chiNhanhId={a}";
            var phongNo = $"/QuanLyNhaTro/QuanLyHoaDon?thang=0&nam=0&trangThaiPhatHanh=3&trangThai=-2&chiNhanhId={a}";
            var tienMat = $"/QuanLyNhaTro/LichSuThanhToan?nguoiThu=toi&phuongThuc=tien-mat&tuNgay=2026-11-01&denNgay=2026-11-30&chiNhanhId={a}";
            Assert.Contains($"href=\"{quaHan}\"", html);
            Assert.Contains($"href=\"{choThu}\"", html);
            Assert.Contains($"href=\"{phongNo}\"", html);
            Assert.Contains($"href=\"{tienMat}\"", html);

            using (var doc = await JsonAsync(staff, Ajax(a, 11, 2026)))
            {
                var links = doc.RootElement.GetProperty("links");
                Assert.Equal(quaHan, links.GetProperty("quaHan").GetString());
                Assert.Equal(choThu, links.GetProperty("choThu").GetString());
                Assert.Equal(phongNo, links.GetProperty("phongDangNo").GetString());
                Assert.Equal(tienMat, links.GetProperty("tienMatCuaToi").GetString());
                Assert.Equal(JsonValueKind.Null, links.GetProperty("daThuKy").ValueKind);
            }

            // Không chọn chi nhánh: bỏ hẳn tham số chiNhanhId.
            var htmlTatCa = await HtmlAsync(staff, $"{Dashboard}?month=11&year=2026");
            Assert.Contains("href=\"/QuanLyNhaTro/ViecCanLam#hoa-don-qua-han\"", htmlTatCa);
            Assert.Contains("href=\"/QuanLyNhaTro/QuanLyHoaDon?thang=11&nam=2026&trangThaiPhatHanh=3&trangThai=-2\"", htmlTatCa);
            Assert.Contains("href=\"/QuanLyNhaTro/LichSuThanhToan?nguoiThu=toi&phuongThuc=tien-mat&tuNgay=2026-11-01&denNgay=2026-11-30\"", htmlTatCa);

            var daThuKy = $"/QuanLyNhaTro/BaoCao?tab=doanh-thu&chiNhanhId={a}&tuThang=11&tuNam=2026&denThang=11&denNam=2026";
            var adminHtml = await HtmlAsync(admin, $"{Dashboard}?branchId={a}&month=11&year=2026");
            Assert.Contains($"href=\"{daThuKy}\"", adminHtml);
            using (var doc = await JsonAsync(admin, Ajax(a, 11, 2026)))
            {
                Assert.Equal(daThuKy, doc.RootElement.GetProperty("links").GetProperty("daThuKy").GetString());
            }
        }

        [Fact]
        public async Task TrangDich_HoaDonLocChuaThuDu_ChiTraChuaThuVaMotPhanCuaChiNhanhMinh()
        {
            var w = await SeedAsync(_factory);
            // Ba hóa đơn đã gửi của hợp đồng A ở ba kỳ khác nhau (chỉ mục duy nhất theo hợp đồng và kỳ).
            var chua = await AddInvoiceAsync(w.ContractA, 1, 2031, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan);
            var motPhan = await AddInvoiceAsync(w.ContractA, 2, 2031, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan);
            var daThu = await AddInvoiceAsync(w.ContractA, 3, 2031, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan);
            var cuaB = await AddInvoiceAsync(w.ContractB, 1, 2031, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan);
            var (staff, token) = await LoginAsync(_factory, w.Staff, "/QuanLyNhaTro/QuanLyHoaDon");

            var res = await PostFormAsync(staff, token, "/HoaDon/GetList", new Dictionary<string, string>
            {
                ["trangThai"] = "-2",
                ["trangThaiPhatHanh"] = "3",
                ["thang"] = "0",
                ["nam"] = "2031",
                ["draw"] = "1",
                ["start"] = "0",
                ["length"] = "50"
            });

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var ids = doc.RootElement.GetProperty("data").EnumerateArray().Select(d => d.GetProperty("hoaDonId").GetInt32()).ToList();
            Assert.Contains(chua, ids);
            Assert.Contains(motPhan, ids);
            Assert.DoesNotContain(daThu, ids);
            Assert.DoesNotContain(cuaB, ids);
        }

        // Test 5 (đầu cuối)
        [Fact]
        public async Task TienMat_NhanVienThayCuaMinh_AdminThayDongTheoTenNhanVien()
        {
            var w = await SeedAsync(_factory);
            var hoaDonId = await AddInvoiceAsync(w.ContractA, 1, 2032, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var staffId = await db.NguoiDungs.Where(u => u.TenDangNhap == w.Staff).Select(u => u.NguoiDungId).SingleAsync();
                db.LichSuThanhToans.Add(new LichSuThanhToan
                {
                    HoaDonId = hoaDonId,
                    MaGiaoDich = $"TM-{Guid.NewGuid():N}",
                    SoTienThanhToan = 321_000m,
                    PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                    NgayThanhToan = DateTime.UtcNow,
                    NgayXacNhan = DateTime.UtcNow,
                    NguoiXacNhanId = staffId
                });
                await db.SaveChangesAsync();
            }

            var (staff, _) = await LoginAsync(_factory, w.Staff);
            var (admin, _) = await LoginAsync(_factory, w.Admin);
            var vn = DateTime.UtcNow.AddHours(7);

            using (var doc = await JsonAsync(staff, Ajax(w.BranchA, vn.Month, vn.Year)))
            {
                var cuaToi = doc.RootElement.GetProperty("tienMatCuaToi");
                Assert.Equal(1, cuaToi.GetProperty("soKhoan").GetInt32());
                Assert.Equal(321_000m, cuaToi.GetProperty("tongTien").GetDecimal());
            }

            using (var doc = await JsonAsync(admin, Ajax(w.BranchA, vn.Month, vn.Year)))
            {
                var dong = doc.RootElement.GetProperty("tienMatNhanVien").EnumerateArray()
                    .Single(n => n.GetProperty("ten").GetString() == w.Staff);
                Assert.Equal(1, dong.GetProperty("soKhoan").GetInt32());
                Assert.Equal(321_000m, dong.GetProperty("tongTien").GetDecimal());
                Assert.StartsWith("/QuanLyNhaTro/LichSuThanhToan?nguoiThu=", dong.GetProperty("link").GetString());
            }

            var adminHtml = await HtmlAsync(admin, $"{Dashboard}?branchId={w.BranchA}");
            Assert.Contains(w.Staff, adminHtml);
            Assert.Contains("Tiền mặt nhân viên đang giữ", adminHtml);
        }

        [Fact]
        public async Task KyKhongHopLe_DungThangNamHienTai()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);
            var vn = DateTime.UtcNow.AddHours(7);

            var html = await HtmlAsync(admin, $"{Dashboard}?month=13&year=1999");
            Assert.Contains($"Đã thu kỳ T{vn.Month}/{vn.Year}", html);

            using var doc = await JsonAsync(admin, Ajax(null, 0, 0));
            Assert.Equal(12, doc.RootElement.GetProperty("chartData").GetArrayLength());
        }
    }
}
