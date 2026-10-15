using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    // Giao diện hóa đơn tự động (đợt C): JSON quản lý, màn hóa đơn, màn chi nhánh,
    // việc cần làm HD2 và quy tắc hiển thị mã QR chuyển khoản.
    [Collection(WebTestCollection.Name)]
    public class AutoInvoiceUiEndpointTests
    {
        private const string LoiSoGio = "Số giờ phản hồi hóa đơn phải là số nguyên từ 1 đến 168.";

        private readonly CustomWebApplicationFactory _factory;

        public AutoInvoiceUiEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // Hóa đơn tạm tính ở chi nhánh A (kỳ 10/2026) cùng giá dịch vụ để điều chỉnh hóa đơn tính được.
        private async Task<(BranchScopeWorld World, HoaDon Invoice)> SeedWithProvisionalAsync()
        {
            var w = await SeedAsync(_factory);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = Guid.NewGuid().ToString("N")[..8];

            var dien = new DichVu { TenDichVu = $"Dien {s}", DonVi = "kWh", LoaiDichVu = LoaiDichVu.Dien };
            var nuoc = new DichVu { TenDichVu = $"Nuoc {s}", DonVi = "m3", LoaiDichVu = LoaiDichVu.Nuoc };
            db.DichVus.AddRange(dien, nuoc);
            await db.SaveChangesAsync();
            db.DichVuChiNhanhs.AddRange(
                new DichVuChiNhanh { ChiNhanhId = w.BranchA, DichVuId = dien.DichVuId, GiaDichVu = 3500m },
                new DichVuChiNhanh { ChiNhanhId = w.BranchA, DichVuId = nuoc.DichVuId, GiaDichVu = 20000m });

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = w.RoomA, Thang = 10, Nam = 2026,
                ChiSoDienCu = 100, ChiSoDienMoi = 120, ChiSoNuocCu = 50, ChiSoNuocMoi = 60,
                DonGiaDien = 3500, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            db.DichVuDienNuocCuaPhongs.Add(period);
            await db.SaveChangesAsync();

            var invoice = new HoaDon
            {
                HopDongId = w.ContractA, MaHoaDon = $"HD-UI-{s}", Thang = 10, Nam = 2026,
                TongTien = 3_270_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                HanPhanHoi = DateTime.UtcNow.AddHours(24),
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
            return (w, invoice);
        }

        private async Task<string> CreateStaffOfBranchAsync(int branchId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<INguoiDungService>();
            var name = $"nvb_{Guid.NewGuid():N}"[..14];
            Assert.True((await users.AddAsync(new CreateNguoiDungReq { TenDangNhap = name, MatKhau = Password, Role = AppRole.NhanVien })).Success);
            var staff = await db.NguoiDungs.FirstAsync(u => u.TenDangNhap == name);
            db.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId, ChiNhanhId = branchId, IsActive = true,
                NguoiPhanCongId = staff.NguoiDungId, NgayPhanCong = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return name;
        }

        private static Dictionary<string, string> ListForm(int branchId) => new()
        {
            ["draw"] = "1", ["start"] = "0", ["length"] = "50",
            ["chiNhanhId"] = branchId.ToString(), ["thang"] = "10", ["nam"] = "2026", ["trangThaiPhatHanh"] = "1"
        };

        private static string VnText(DateTime utc)
            => utc.AddHours(7).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        private static Dictionary<string, string> BranchForm(ChiNhanh b, string? soGio) => new()
        {
            ["ChiNhanhId"] = b.ChiNhanhId.ToString(),
            ["MaChiNhanh"] = b.MaChiNhanh,
            ["TenChiNhanh"] = b.TenChiNhanh,
            ["DiaChi"] = b.DiaChi,
            ["SoDienThoai"] = b.SoDienThoai,
            ["MoTa"] = b.MoTa ?? string.Empty,
            ["SoGioPhanHoiHoaDon"] = soGio ?? string.Empty
        };

        private Task<ChiNhanh> LoadBranchAsync(int id) => ReadAsync(_factory, db => db.ChiNhanhs.AsNoTracking().SingleAsync(b => b.ChiNhanhId == id));

        [Fact]
        public async Task Staff_GetList_NoLongerReturnsProvisionalKeys_AndGetByIdReturnsMeterReadings()
        {
            var (w, invoice) = await SeedWithProvisionalAsync();
            var (staff, token) = await LoginAsync(_factory, w.Staff);

            var list = await JsonAsync(await PostFormAsync(staff, token, "/HoaDon/GetList", ListForm(w.BranchA)));
            var row = list.GetProperty("data").EnumerateArray().Single(x => x.GetProperty("hoaDonId").GetInt32() == invoice.HoaDonId);
            Assert.False(row.TryGetProperty("laTamTinh", out _));
            Assert.False(row.TryGetProperty("hanTuChot", out _));
            // Cờ tự động chỉ theo thẻ lịch sử [TU_DONG]; hóa đơn mẫu chỉ có HanPhanHoi và ở ChoDuyet.
            Assert.False(row.GetProperty("laHoaDonTuDong").GetBoolean());
            Assert.Equal(JsonValueKind.Null, row.GetProperty("hanPhanHoiUtc").ValueKind);
            Assert.Equal(string.Empty, row.GetProperty("hanPhanHoiHienThi").GetString());

            var detailRes = await staff.GetAsync($"/HoaDon/GetById/{invoice.HoaDonId}");
            Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
            var detail = await JsonAsync(detailRes);
            Assert.False(detail.TryGetProperty("laTamTinh", out _));
            Assert.False(detail.TryGetProperty("hanTuChot", out _));
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("hanPhanHoiUtc").ValueKind);
            Assert.Equal(string.Empty, detail.GetProperty("hanPhanHoiHienThi").GetString());
            Assert.Equal(100m, detail.GetProperty("chiSoDienCu").GetDecimal());
            Assert.Equal(120m, detail.GetProperty("chiSoDienMoi").GetDecimal());
            Assert.Equal(50m, detail.GetProperty("chiSoNuocCu").GetDecimal());
            Assert.Equal(60m, detail.GetProperty("chiSoNuocMoi").GetDecimal());
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, detail.GetProperty("trangThaiPhatHanhValue").GetInt32());
        }

        [Fact]
        public async Task Staff_GetListAndGetById_DaGui_ReturnVietnamTimeDeadline()
        {
            var (w, invoice) = await SeedWithProvisionalAsync();
            var han = DateTime.UtcNow.AddHours(30);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var h = await db.HoaDons.SingleAsync(x => x.HoaDonId == invoice.HoaDonId);
                h.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
                h.NgayGui = DateTime.UtcNow.AddHours(-1);
                h.HanPhanHoi = han;
                await db.SaveChangesAsync();
            }
            var (staff, token) = await LoginAsync(_factory, w.Staff);

            var form = ListForm(w.BranchA);
            form["trangThaiPhatHanh"] = "3";
            var list = await JsonAsync(await PostFormAsync(staff, token, "/HoaDon/GetList", form));
            var row = list.GetProperty("data").EnumerateArray().Single(x => x.GetProperty("hoaDonId").GetInt32() == invoice.HoaDonId);
            Assert.Equal(VnText(han), row.GetProperty("hanPhanHoiHienThi").GetString());

            var detail = await JsonAsync(await staff.GetAsync($"/HoaDon/GetById/{invoice.HoaDonId}"));
            Assert.Equal(VnText(han), detail.GetProperty("hanPhanHoiHienThi").GetString());
        }

        [Fact]
        public async Task Staff_DieuChinhInfo_ManualChoDuyetInvoice_IsAdjustable()
        {
            var (w, invoice) = await SeedWithProvisionalAsync();
            var (staff, _) = await LoginAsync(_factory, w.Staff);

            var info = await JsonAsync(await staff.GetAsync($"/HoaDon/DieuChinh/{invoice.HoaDonId}"));
            Assert.True(info.GetProperty("coTheDieuChinh").GetBoolean());
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, info.GetProperty("trangThaiPhatHanhValue").GetInt32());
            Assert.True(info.TryGetProperty("hanPhanHoi", out _));
            Assert.False(info.TryGetProperty("hanTuChot", out _));
        }

        [Fact]
        public async Task StaffOfAnotherBranch_CannotSeeProvisionalInvoice()
        {
            var (w, invoice) = await SeedWithProvisionalAsync();
            var otherStaff = await CreateStaffOfBranchAsync(w.BranchB);
            var (client, token) = await LoginAsync(_factory, otherStaff);

            var list = await JsonAsync(await PostFormAsync(client, token, "/HoaDon/GetList", ListForm(w.BranchA)));
            Assert.Empty(list.GetProperty("data").EnumerateArray());

            var detail = await client.GetAsync($"/HoaDon/GetById/{invoice.HoaDonId}");
            Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        }

        [Fact]
        public async Task InvoicePage_LoadsAdjustScript_AndShowsQrOnlyForChotOrGui()
        {
            var w = await SeedAsync(_factory);
            var (staff, _) = await LoginAsync(_factory, w.Staff, "/QuanLyNhaTro/QuanLyHoaDon");

            var response = await staff.GetAsync("/QuanLyNhaTro/QuanLyHoaDon");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("hoadon-dieuchinh.js", html);
            Assert.Contains("Điều chỉnh hóa đơn", html);
            Assert.DoesNotContain("Điều chỉnh và gửi lại", html);
            Assert.DoesNotContain("Tạm tính, tự chốt lúc", html);
            Assert.Contains("data-trangthai", html);

            // QR chuyển khoản trong modal chi tiết chỉ dựng khi hóa đơn ở trạng thái Đã chốt (2) hoặc Đã gửi (3).
            Assert.Contains("const hienQrChuyenKhoan = !res.daHuy && res.conLai > 0", html);
            Assert.Contains("(res.trangThaiPhatHanhValue === 2 || res.trangThaiPhatHanhValue === 3)", html);
            Assert.Contains("if (hienQrChuyenKhoan) {", html);
            // Điều kiện cũ (mọi hóa đơn chưa hủy còn nợ) không còn.
            Assert.DoesNotContain("if (!res.daHuy && res.conLai > 0) {", html);
        }

        [Fact]
        public async Task Admin_CapNhatChiNhanh_RejectsInvalidHours_AndKeepsDatabase()
        {
            var (w, _) = await SeedWithProvisionalAsync();
            var (admin, token) = await LoginAsync(_factory, w.Admin);
            var branch = await LoadBranchAsync(w.BranchA);

            foreach (var bad in new[] { "0", "169", "-5", "abc", "1.5" })
            {
                var res = await JsonAsync(await PostFormAsync(admin, token, "/ChiNhanhs/CapNhatChiNhanh", BranchForm(branch, bad)));
                Assert.False(res.GetProperty("success").GetBoolean(), bad);
                Assert.Equal(LoiSoGio, res.GetProperty("message").GetString());
            }

            Assert.Equal(24, (await LoadBranchAsync(w.BranchA)).SoGioPhanHoiHoaDon);
        }

        [Fact]
        public async Task Admin_ChangingHours_KeepsExistingDeadline_AndAdjustmentNoLongerSetsOne()
        {
            var (w, invoice) = await SeedWithProvisionalAsync();
            var (admin, adminToken) = await LoginAsync(_factory, w.Admin);
            var (staff, staffToken) = await LoginAsync(_factory, w.Staff);
            var branch = await LoadBranchAsync(w.BranchA);

            var update = await JsonAsync(await PostFormAsync(admin, adminToken, "/ChiNhanhs/CapNhatChiNhanh", BranchForm(branch, "48")));
            Assert.True(update.GetProperty("success").GetBoolean());
            Assert.Equal(48, (await LoadBranchAsync(w.BranchA)).SoGioPhanHoiHoaDon);

            var before = await ReadAsync(_factory, db => db.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == invoice.HoaDonId));
            Assert.Equal(invoice.HanPhanHoi!.Value, before.HanPhanHoi!.Value, TimeSpan.FromSeconds(1));

            var adjust = await SendJsonAsync(staff, staffToken, System.Net.Http.HttpMethod.Post, $"/HoaDon/DieuChinh/{invoice.HoaDonId}",
                new { trangThaiPhatHanhHienTai = 1, chiSoDienMoi = 130m, chiSoNuocMoi = 60m });
            Assert.Equal(HttpStatusCode.OK, adjust.StatusCode);

            var after = await ReadAsync(_factory, db => db.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == invoice.HoaDonId));
            // Điều chỉnh không gửi tạm tính nữa: hóa đơn vẫn chờ duyệt, không còn hạn phản hồi.
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, after.TrangThaiPhatHanh);
            Assert.Null(after.HanPhanHoi);
        }

        [Fact]
        public async Task Admin_CapNhatChiNhanh_WithoutField_KeepsCurrentHours()
        {
            var (w, _) = await SeedWithProvisionalAsync();
            var (admin, token) = await LoginAsync(_factory, w.Admin);
            var branch = await LoadBranchAsync(w.BranchA);
            Assert.True((await JsonAsync(await PostFormAsync(admin, token, "/ChiNhanhs/CapNhatChiNhanh", BranchForm(branch, "72")))).GetProperty("success").GetBoolean());

            var form = BranchForm(branch, null);
            form.Remove("SoGioPhanHoiHoaDon");
            var res = await JsonAsync(await PostFormAsync(admin, token, "/ChiNhanhs/CapNhatChiNhanh", form));

            Assert.True(res.GetProperty("success").GetBoolean());
            Assert.Equal(72, (await LoadBranchAsync(w.BranchA)).SoGioPhanHoiHoaDon);
        }

        [Fact]
        public async Task Admin_ThemChiNhanhMoi_WithoutField_Defaults24_AndGetChiNhanhReturnsHours()
        {
            var w = await SeedAsync(_factory);
            var (admin, token) = await LoginAsync(_factory, w.Admin);
            var ma = $"N{Guid.NewGuid():N}"[..8];

            var add = await JsonAsync(await PostFormAsync(admin, token, "/ChiNhanhs/ThemChiNhanhMoi", new Dictionary<string, string>
            {
                ["ChiNhanhId"] = "0", ["MaChiNhanh"] = ma, ["TenChiNhanh"] = $"Chi nhanh {ma}", ["DiaChi"] = "Dia chi",
                ["SoDienThoai"] = "0912345678", ["MoTa"] = "Tao boi test"
            }));
            Assert.True(add.GetProperty("success").GetBoolean());

            var created = await ReadAsync(_factory, db => db.ChiNhanhs.AsNoTracking().SingleAsync(b => b.MaChiNhanh == ma.ToUpper()));
            Assert.Equal(24, created.SoGioPhanHoiHoaDon);

            var get = await JsonAsync(await admin.GetAsync($"/ChiNhanhs/GetChiNhanh?id={created.ChiNhanhId}"));
            Assert.Equal(24, get.GetProperty("data").GetProperty("soGioPhanHoiHoaDon").GetInt32());
        }

        [Fact]
        public async Task Staff_CapNhatChiNhanh_IsNotAllowed_AndHoursUnchanged()
        {
            var w = await SeedAsync(_factory);
            var (staff, token) = await LoginAsync(_factory, w.Staff);
            var branch = await LoadBranchAsync(w.BranchA);

            var res = await PostFormAsync(staff, token, "/ChiNhanhs/CapNhatChiNhanh", BranchForm(branch, "99"));

            Assert.True(res.StatusCode == HttpStatusCode.Redirect || res.StatusCode == HttpStatusCode.Forbidden);
            Assert.Equal(24, (await LoadBranchAsync(w.BranchA)).SoGioPhanHoiHoaDon);
        }

        private static JsonElement[] Items(JsonElement root) => root.GetProperty("viecCanLam").GetProperty("items").EnumerateArray().ToArray();

        private static string DashboardUrl(int? branchId)
        {
            var now = DateTime.UtcNow.AddHours(7);
            return $"/QuanLyNhaTro/Dashboard/GetAjaxData?{(branchId.HasValue ? $"branchId={branchId}&" : string.Empty)}year={now.Year}&month={now.Month}";
        }

        [Fact]
        public async Task ViecCanLam_Hd2CountsLegacyProvisionalForAdmin_NoHd4_NotShownToOtherBranch()
        {
            var (w, _) = await SeedWithProvisionalAsync();
            var otherStaff = await CreateStaffOfBranchAsync(w.BranchB);
            var (admin, _) = await LoginAsync(_factory, w.Admin);
            var (staff, _) = await LoginAsync(_factory, w.Staff);
            var (other, _) = await LoginAsync(_factory, otherStaff);

            var adminItems = Items(await JsonAsync(await admin.GetAsync(DashboardUrl(w.BranchA))));
            var hd2 = adminItems.Single(i => i.GetProperty("maViec").GetString() == "HD2");
            Assert.Equal(1, hd2.GetProperty("soLuong").GetInt32());
            Assert.Equal("Hóa đơn chờ Admin chốt", hd2.GetProperty("tieuDe").GetString());
            Assert.DoesNotContain(adminItems, i => i.GetProperty("maViec").GetString() == "HD4");

            // HD2 chỉ Admin; nhân viên không còn việc HD4 nào.
            var staffItems = Items(await JsonAsync(await staff.GetAsync(DashboardUrl(null))));
            Assert.DoesNotContain(staffItems, i => i.GetProperty("maViec").GetString() == "HD2");
            Assert.DoesNotContain(staffItems, i => i.GetProperty("maViec").GetString() == "HD4");

            var otherRoot = await JsonAsync(await other.GetAsync(DashboardUrl(null)));
            var branchAName = await ReadAsync(_factory, db => db.ChiNhanhs.AsNoTracking().Where(b => b.ChiNhanhId == w.BranchA).Select(b => b.TenChiNhanh).SingleAsync());
            Assert.All(Items(otherRoot), i => Assert.DoesNotContain(branchAName, i.GetProperty("moTa").GetString()));

            var otherChoosingA = await JsonAsync(await other.GetAsync(DashboardUrl(w.BranchA)));
            Assert.Equal(0, otherChoosingA.GetProperty("viecCanLam").GetProperty("tongSo").GetInt32());
        }
    }
}
