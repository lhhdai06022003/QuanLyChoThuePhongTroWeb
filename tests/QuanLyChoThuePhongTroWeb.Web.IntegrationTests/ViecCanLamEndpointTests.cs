using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Trung tâm việc cần làm qua HTTP: JSON Dashboard, tóm tắt cho huy hiệu, phân quyền chi nhánh và việc chỉ Admin.
    [Collection(WebTestCollection.Name)]
    public class ViecCanLamEndpointTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public ViecCanLamEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Mỗi chi nhánh có thêm một hóa đơn ChoDuyet (việc HD2, chỉ Admin được thấy).
        private async Task<BranchScopeWorld> SeedWithPendingInvoicesAsync()
        {
            var w = await SeedAsync(_factory);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];
            db.HoaDons.AddRange(
                new HoaDon { MaHoaDon = $"VA{s}", HopDongId = w.ContractA, Thang = 1, Nam = 2030, TongTien = 100_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet },
                new HoaDon { MaHoaDon = $"VB{s}", HopDongId = w.ContractB, Thang = 1, Nam = 2030, TongTien = 100_000m, TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet });
            await db.SaveChangesAsync();
            return w;
        }

        private static string Url(int? branchId)
        {
            var now = DateTime.Now;
            return $"/QuanLyNhaTro/Dashboard/GetAjaxData?{(branchId.HasValue ? $"branchId={branchId}&" : string.Empty)}year={now.Year}&month={now.Month}";
        }

        private static JsonElement[] Items(JsonElement root) => root.GetProperty("viecCanLam").GetProperty("items").EnumerateArray().ToArray();

        [Fact]
        public async Task Staff_GetAjaxData_HasNewKeys_NoToDos_NoBranchBText_NoAdminOnlyJob()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var root = await JsonAsync(await staff.GetAsync(Url(null)));

            Assert.True(root.TryGetProperty("tienQuaHan", out _));
            Assert.True(root.TryGetProperty("soHoaDonQuaHan", out _));
            Assert.True(root.TryGetProperty("viecCanLam", out _));
            Assert.False(root.TryGetProperty("toDos", out _));
            Assert.Equal(root.GetProperty("viecCanLam").GetProperty("tongSo").GetInt32(),
                root.GetProperty("viecCanLam").GetProperty("soKhan").GetInt32()
                + root.GetProperty("viecCanLam").GetProperty("soCanLam").GetInt32()
                + root.GetProperty("viecCanLam").GetProperty("soTheoDoi").GetInt32());

            var items = Items(root);
            Assert.NotEmpty(items); // chi nhánh A có hợp đồng chưa chốt điện nước
            Assert.All(items, i =>
            {
                Assert.DoesNotContain(w.BranchNameB, i.GetProperty("moTa").GetString());
                Assert.NotEqual("HD2", i.GetProperty("maViec").GetString());
            });
        }

        [Fact]
        public async Task Staff_ChoosingBranchB_GetsNoWork()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var root = await JsonAsync(await staff.GetAsync(Url(w.BranchB)));

            var viec = root.GetProperty("viecCanLam");
            Assert.Equal(0, viec.GetProperty("tongSo").GetInt32());
            Assert.Empty(Items(root));
        }

        [Fact]
        public async Task Admin_SeesHd2PerBranch_AndBranchBDescriptionNamesBranchB()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var rootA = await JsonAsync(await admin.GetAsync(Url(w.BranchA)));
            var hd2A = Items(rootA).SingleOrDefault(i => i.GetProperty("maViec").GetString() == "HD2");
            Assert.NotEqual(default, hd2A);
            Assert.Equal(1, hd2A.GetProperty("soLuong").GetInt32());

            var rootB = await JsonAsync(await admin.GetAsync(Url(w.BranchB)));
            var itemsB = Items(rootB);
            Assert.Contains(itemsB, i => i.GetProperty("moTa").GetString()!.Contains(w.BranchNameB));
            Assert.Contains(itemsB, i => i.GetProperty("maViec").GetString() == "HD2");
            Assert.All(itemsB, i => Assert.DoesNotContain($"CN_A_{w.BranchNameB[5..]}", i.GetProperty("moTa").GetString()));
        }

        [Fact]
        public async Task TomTat_Staff_EqualsDashboardTotal_AndAdminTotalMinusHd2()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var (staff, _) = await LoginAsync(_factory, w.Staff);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var tomTatRes = await staff.GetAsync("/QuanLyNhaTro/ViecCanLam/TomTat");
            Assert.Equal(HttpStatusCode.OK, tomTatRes.StatusCode);
            var tomTat = await JsonAsync(tomTatRes);
            var staffDash = await JsonAsync(await staff.GetAsync(Url(null)));
            var adminA = await JsonAsync(await admin.GetAsync(Url(w.BranchA)));

            var hd2 = Items(adminA).Single(i => i.GetProperty("maViec").GetString() == "HD2").GetProperty("soLuong").GetInt32();

            Assert.Equal(staffDash.GetProperty("viecCanLam").GetProperty("tongSo").GetInt32(), tomTat.GetProperty("tongSo").GetInt32());
            Assert.Equal(adminA.GetProperty("viecCanLam").GetProperty("tongSo").GetInt32() - hd2, tomTat.GetProperty("tongSo").GetInt32());
            Assert.True(tomTat.TryGetProperty("soKhan", out _));
            Assert.True(tomTat.TryGetProperty("soCanLam", out _));
            Assert.True(tomTat.TryGetProperty("soTheoDoi", out _));
        }

        [Fact]
        public async Task DashboardHtml_StaffDoesNotSeeAdminOnlyJob_AdminDoes()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var (staff, _) = await LoginAsync(_factory, w.Staff);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            // Razor mã hóa ký tự tiếng Việt thành thực thể HTML, nên giải mã trước khi so sánh.
            var staffHtml = WebUtility.HtmlDecode(await (await staff.GetAsync("/QuanLyNhaTro/Dashboard")).Content.ReadAsStringAsync());
            var adminHtml = WebUtility.HtmlDecode(await (await admin.GetAsync($"/QuanLyNhaTro/Dashboard?branchId={w.BranchA}")).Content.ReadAsStringAsync());

            Assert.Contains("Hóa đơn chờ Admin chốt", adminHtml);
            Assert.DoesNotContain("Hóa đơn chờ Admin chốt", staffHtml);
        }

        [Fact]
        public async Task TomTat_Tenant_IsRedirectedToLogin()
        {
            var w = await SeedAsync(_factory);
            string tenantUser;
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                tenantUser = "kh_" + Guid.NewGuid().ToString("N")[..8];
                Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = tenantUser, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = w.TenantA })).Success);
            }

            var (tenant, _) = await LoginAsync(_factory, tenantUser, "/KhachThue/HoaDon");
            var response = await tenant.GetAsync("/QuanLyNhaTro/ViecCanLam/TomTat");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task TomTat_Anonymous_IsRedirectedToLogin()
        {
            var response = await NewClient(_factory).GetAsync("/QuanLyNhaTro/ViecCanLam/TomTat");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", response.Headers.Location!.ToString());
        }

        // Hóa đơn quá hạn 3 ngày của ContractA: tổng 3.590.000, đã thu 590.000 (ledger chưa xóa), ledger đã xóa 100.000 bị bỏ qua.
        private async Task<(BranchScopeWorld World, HoaDon Invoice, DateTime Due)> SeedWithOverdueInvoiceAsync()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var vn = DateTime.UtcNow.AddHours(7);
            var due = DateTime.UtcNow.AddDays(-3);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];
            var inv = new HoaDon
            {
                MaHoaDon = $"VQ{s}",
                HopDongId = w.ContractA,
                Thang = vn.Month,
                Nam = vn.Year,
                TongTien = 3_590_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan,
                HanThanhToan = due
            };
            db.HoaDons.Add(inv);
            await db.SaveChangesAsync();
            db.LichSuThanhToans.AddRange(
                new LichSuThanhToan
                {
                    HoaDonId = inv.HoaDonId,
                    MaGiaoDich = $"VL{s}1",
                    SoTienThanhToan = 590_000m,
                    PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                    NgayThanhToan = DateTime.UtcNow.AddMinutes(-5)
                },
                new LichSuThanhToan
                {
                    HoaDonId = inv.HoaDonId,
                    MaGiaoDich = $"VL{s}2",
                    SoTienThanhToan = 100_000m,
                    PhuongThucThanhToan = PhuongThucThanhToan.TienMat,
                    NgayThanhToan = DateTime.UtcNow.AddMinutes(-5),
                    IsDeleted = true
                });
            await db.SaveChangesAsync();
            return (w, inv, due);
        }

        private static string DashboardAjaxUrl(int branchId, DateTime vn) =>
            $"/QuanLyNhaTro/Dashboard/GetAjaxData?branchId={branchId}&year={vn.Year}&month={vn.Month}";

        [Fact]
        public async Task GetAjaxData_OverdueAndPending_AreScopedByBranch()
        {
            var (w, _, _) = await SeedWithOverdueInvoiceAsync();
            var vn = DateTime.UtcNow.AddHours(7);
            var (admin, _) = await LoginAsync(_factory, w.Admin);
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var adminA = await JsonAsync(await admin.GetAsync(DashboardAjaxUrl(w.BranchA, vn)));
            Assert.Equal(3_000_000m, adminA.GetProperty("tienQuaHan").GetDecimal());
            Assert.Equal(1, adminA.GetProperty("soHoaDonQuaHan").GetInt32());
            Assert.Equal(3_000_000m, adminA.GetProperty("tongTienChoThu").GetDecimal());

            var staffAll = await JsonAsync(await staff.GetAsync($"/QuanLyNhaTro/Dashboard/GetAjaxData?year={vn.Year}&month={vn.Month}"));
            Assert.Equal(3_000_000m, staffAll.GetProperty("tienQuaHan").GetDecimal());
            Assert.Equal(1, staffAll.GetProperty("soHoaDonQuaHan").GetInt32());
            Assert.Equal(3_000_000m, staffAll.GetProperty("tongTienChoThu").GetDecimal());

            var staffB = await JsonAsync(await staff.GetAsync(DashboardAjaxUrl(w.BranchB, vn)));
            Assert.Equal(0m, staffB.GetProperty("tienQuaHan").GetDecimal());
            Assert.Equal(0, staffB.GetProperty("soHoaDonQuaHan").GetInt32());
            Assert.Equal(0m, staffB.GetProperty("tongTienChoThu").GetDecimal());
        }

        [Fact]
        public async Task DashboardHtml_ShowsMoneyWithDong_AndSeeAllLink()
        {
            var (w, _, _) = await SeedWithOverdueInvoiceAsync();
            var vn = DateTime.UtcNow.AddHours(7);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = WebUtility.HtmlDecode(await (await admin.GetAsync($"/QuanLyNhaTro/Dashboard?branchId={w.BranchA}&year={vn.Year}&month={vn.Month}")).Content.ReadAsStringAsync());

            Assert.Contains("3.000.000 đ", html);
            Assert.Contains("1 hóa đơn", html);
            Assert.Contains("Số tiền: 590.000 đ", html);
            Assert.Contains("Giá thuê: 2.000.000 đ", html);
            Assert.Contains("Xem tất cả", html);
            Assert.DoesNotContain("₫", html);
        }

        [Fact]
        public async Task ViecCanLamPage_Admin_ShowsOverdueTable()
        {
            var (w, inv, due) = await SeedWithOverdueInvoiceAsync();
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var html = WebUtility.HtmlDecode(await (await admin.GetAsync($"/QuanLyNhaTro/ViecCanLam?chiNhanhId={w.BranchA}")).Content.ReadAsStringAsync());

            Assert.Contains("id=\"hoa-don-qua-han\"", html);
            Assert.Contains(inv.MaHoaDon, html);
            Assert.Contains("3.000.000 đ", html);
            Assert.Contains(due.AddHours(7).ToString("dd/MM/yyyy"), html);
            Assert.Contains("3 ngày", html);
            Assert.Contains($"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={inv.HoaDonId}", html);
            Assert.Contains("Hóa đơn chờ Admin chốt", html);
        }

        [Fact]
        public async Task ViecCanLamPage_Staff_OwnBranchOnly_NoAdminOnlyJob()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var res = await staff.GetAsync("/QuanLyNhaTro/ViecCanLam");
            var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("Việc cần làm (", html);
            Assert.DoesNotContain(w.BranchNameB, html);
            Assert.DoesNotContain("Hóa đơn chờ Admin chốt", html);
        }

        [Fact]
        public async Task ViecCanLamPage_Staff_BranchOutsideScope_IsEmptyWithoutError()
        {
            var w = await SeedWithPendingInvoicesAsync();
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var res = await staff.GetAsync($"/QuanLyNhaTro/ViecCanLam?chiNhanhId={w.BranchB}");
            var html = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Contains("Việc cần làm (0)", html);
            Assert.Contains("Không có việc cần xử lý", html);
            Assert.Contains("Không có hóa đơn quá hạn.", html);
            Assert.DoesNotContain(w.BranchNameB, html);
            // Dropdown về "Tất cả chi nhánh" nên phải báo rõ lý do trang trống.
            Assert.Contains("Chi nhánh đã chọn không thuộc phạm vi của bạn", html);

            var own = WebUtility.HtmlDecode(await (await staff.GetAsync($"/QuanLyNhaTro/ViecCanLam?chiNhanhId={w.BranchA}")).Content.ReadAsStringAsync());
            Assert.DoesNotContain("Chi nhánh đã chọn không thuộc phạm vi của bạn", own);
        }

        [Fact]
        public async Task ViecCanLamPage_Tenant_IsRedirectedToLogin()
        {
            var w = await SeedAsync(_factory);
            string tenantUser;
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
                tenantUser = "kh_" + Guid.NewGuid().ToString("N")[..8];
                Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = tenantUser, MatKhau = Password, Role = AppRole.KhachThue, NguoiThueId = w.TenantA })).Success);
            }

            var (tenant, _) = await LoginAsync(_factory, tenantUser, "/KhachThue/HoaDon");
            var response = await tenant.GetAsync("/QuanLyNhaTro/ViecCanLam");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/QuanLyNhaTro/DangNhap", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Pages_EncodeBranchName_NoRawScript()
        {
            var w = await SeedWithPendingInvoicesAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var branch = await db.ChiNhanhs.FirstAsync(c => c.ChiNhanhId == w.BranchA);
                branch.TenChiNhanh = $"CN_A_{Guid.NewGuid():N}<script>alert(1)</script>";
                await db.SaveChangesAsync();
            }
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            var page = await (await admin.GetAsync($"/QuanLyNhaTro/ViecCanLam?chiNhanhId={w.BranchA}")).Content.ReadAsStringAsync();
            var dashboard = await (await admin.GetAsync($"/QuanLyNhaTro/Dashboard?branchId={w.BranchA}")).Content.ReadAsStringAsync();

            Assert.DoesNotContain("<script>alert(1)</script>", page);
            Assert.Contains("&lt;script&gt;", page);
            Assert.DoesNotContain("<script>alert(1)</script>", dashboard);
            Assert.Contains("&lt;script&gt;", dashboard);
        }

        [Fact]
        public async Task Dashboard_Staff_HasMenuItemAndBadge()
        {
            var w = await SeedAsync(_factory);
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var html = await (await staff.GetAsync("/QuanLyNhaTro/Dashboard")).Content.ReadAsStringAsync();

            Assert.Contains("href=\"/QuanLyNhaTro/ViecCanLam\"", html);
            Assert.Contains("id=\"badge-viec-can-lam\"", html);
            Assert.Contains("viec-can-lam-badge.js", html);
        }

        [Fact]
        public async Task Dashboard_DefaultMonthYear_UseVietnamTimeFromTimeProvider()
        {
            // 31/01/2027 18:00 UTC = 01/02/2027 01:00 giờ VN.
            var w = await SeedAsync(_factory);
            var fixedHost = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2027, 1, 31, 18, 0, 0, TimeSpan.Zero)));
            }));
            var (admin, _) = await LoginAsync(fixedHost, w.Admin);

            var html = await (await admin.GetAsync("/QuanLyNhaTro/Dashboard")).Content.ReadAsStringAsync();

            Assert.True(OptionSelected(html, "monthSelect", "2"), "Tháng mặc định phải là 2");
            Assert.True(OptionSelected(html, "yearSelect", "2027"), "Năm mặc định phải là 2027");
        }

        private static bool OptionSelected(string html, string selectId, string value)
        {
            var select = Regex.Match(html, "<select[^>]*id=\"" + selectId + "\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
            if (!select.Success) return false;
            // Option không có thuộc tính value (danh sách năm) thì giá trị là nội dung chữ.
            foreach (Match option in Regex.Matches(select.Groups[1].Value, "<option([^>]*)>(.*?)</option>", RegexOptions.Singleline))
            {
                var attrs = option.Groups[1].Value;
                var optionValue = Regex.Match(attrs, "value=\"([^\"]*)\"");
                var actual = optionValue.Success ? optionValue.Groups[1].Value : option.Groups[2].Value.Trim();
                if (actual == value && attrs.Contains("selected")) return true;
            }
            return false;
        }

        private sealed class FixedTime : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTime(DateTimeOffset now)
            {
                _now = now;
            }

            public override DateTimeOffset GetUtcNow() => _now;
        }
    }
}
