using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;
using static QuanLyChoThuePhongTroWeb.Web.IntegrationTests.BranchScopeKit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Nhân viên chi nhánh A gọi id thuộc chi nhánh B thì bị chặn: đọc theo id trả 404, ghi trả 403, danh sách không có dữ liệu B.
    [Collection(WebTestCollection.Name)]
    public class CoreBranchScopeTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public CoreBranchScopeTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static Dictionary<string, string> RoomForm(int phongTroId, int chiNhanhId, string soPhong) => new()
        {
            ["PhongTroId"] = phongTroId.ToString(),
            ["ChiNhanhId"] = chiNhanhId.ToString(),
            ["SoPhong"] = soPhong,
            ["TangLau"] = "1",
            ["GiaThue"] = "1900000",
            ["DienTich"] = "20",
            ["SoNguoiToiDa"] = "3",
            ["TrangThai"] = "1",
            ["MoTa"] = "sua"
        };

        [Fact]
        public async Task PhongTro_StaffOfBranchA_IsBlockedFromBranchB()
        {
            var w = await SeedAsync(_factory);
            var (staff, token) = await LoginAsync(_factory, w.Staff);

            // Dropdown chi nhánh trên trang chỉ có chi nhánh được phân công.
            var page = await (await staff.GetAsync("/PhongTros/QuanLyPhongTro")).Content.ReadAsStringAsync();
            Assert.DoesNotContain(w.BranchNameB, page);

            // Đọc theo id: phòng của mình 200, phòng chi nhánh khác 404.
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/PhongTros/GetPhongTro?id={w.RoomA}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/PhongTros/GetPhongTro?id={w.RoomB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/PhongTros/GetQuickContract?phongTroId={w.RoomB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/PhongTros/GetUnpaidInvoice?phongTroId={w.RoomB}")).StatusCode);

            // Danh sách và sơ đồ không có phòng chi nhánh B.
            var list = await JsonAsync(await staff.GetAsync("/PhongTros/DanhSachPhongTro"));
            var ids = list.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("phongTroId").GetInt32()).ToList();
            Assert.Contains(w.RoomA, ids);
            Assert.DoesNotContain(w.RoomB, ids);

            var soDoAll = await JsonAsync(await staff.GetAsync("/PhongTros/GetSoDoPhong?chiNhanhId=0"));
            Assert.DoesNotContain(soDoAll.EnumerateArray(), x => x.GetProperty("phongTroId").GetInt32() == w.RoomB);
            var soDoB = await JsonAsync(await staff.GetAsync($"/PhongTros/GetSoDoPhong?chiNhanhId={w.BranchB}"));
            Assert.Equal(0, soDoB.GetArrayLength());

            // Ghi: sửa phòng chi nhánh B và chuyển phòng A sang B đều 403, dữ liệu không đổi.
            Assert.Equal(HttpStatusCode.Forbidden, (await PostFormAsync(staff, token, "/PhongTros/CapNhatPhongTro", RoomForm(w.RoomB, w.BranchB, w.RoomNumberB))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await PostFormAsync(staff, token, "/PhongTros/CapNhatPhongTro", RoomForm(w.RoomA, w.BranchB, "MOVED"))).StatusCode);

            var (roomABranch, roomBPrice) = await ReadAsync(_factory, async db => (
                await db.PhongTros.Where(p => p.PhongTroId == w.RoomA).Select(p => p.ChiNhanhId).SingleAsync(),
                await db.PhongTros.Where(p => p.PhongTroId == w.RoomB).Select(p => p.GiaThue).SingleAsync()));
            Assert.Equal(w.BranchA, roomABranch);
            Assert.Equal(2500000m, roomBPrice);

            // Thêm, xóa phòng chỉ dành cho Admin: nhân viên bị chuyển về trang đăng nhập, phòng không bị xóa.
            Assert.NotEqual(HttpStatusCode.OK, (await PostFormAsync(staff, token, "/PhongTros/XoaPhongTro", new Dictionary<string, string> { ["id"] = w.RoomA.ToString() })).StatusCode);
            Assert.False(await ReadAsync(_factory, db => db.PhongTros.Where(p => p.PhongTroId == w.RoomA).Select(p => p.IsDeleted).SingleAsync()));
        }

        [Fact]
        public async Task HopDong_StaffOfBranchA_IsBlockedFromBranchB()
        {
            var w = await SeedAsync(_factory);
            var (staff, token) = await LoginAsync(_factory, w.Staff);

            // Danh sách DataTable chỉ có hợp đồng chi nhánh A.
            var list = await JsonAsync(await PostFormAsync(staff, token, "/HopDong/DanhSachHopDongSideAsync", new Dictionary<string, string>
            {
                ["draw"] = "1", ["start"] = "0", ["length"] = "500"
            }));
            var ids = list.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("hopDongId").GetInt32()).ToList();
            Assert.Contains(w.ContractA, ids);
            Assert.DoesNotContain(w.ContractB, ids);

            // Đọc, in, tải Word hợp đồng chi nhánh B: 404.
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/HopDong/GetById/{w.ContractA}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/HopDong/GetById/{w.ContractB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/HopDong/Print/{w.ContractB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/HopDong/DownloadWord/{w.ContractB}")).StatusCode);

            // Ghi lên hợp đồng / phòng chi nhánh B: 403.
            var body = new
            {
                HopDongId = w.ContractB,
                PhongTroId = w.RoomB,
                NguoiThueId = w.TenantFree,
                ThoiDiemBatDau = System.DateTime.UtcNow,
                TienCocPhong = 1000000m,
                TienThuePhong = 2000000m,
                TrangThaiHopDong = 0
            };
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, "/HopDong/Create", body)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Put, $"/HopDong/Update/{w.ContractB}", body)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Delete, $"/HopDong/Delete/{w.ContractB}", null)).StatusCode);

            // Thành viên hợp đồng chi nhánh B: danh sách rỗng, thêm / báo rời / xóa đều 403.
            var members = await JsonAsync(await staff.GetAsync($"/ThanhVienHopDong/GetDanhSach/{w.ContractB}"));
            Assert.Equal(0, members.GetArrayLength());
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, "/ThanhVienHopDong/AddThanhVien",
                new { HopDongId = w.ContractB, NguoiThueId = w.TenantFree, NgayVao = System.DateTime.UtcNow })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, $"/ThanhVienHopDong/BaoRoiPhong/{w.MemberB}", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Delete, $"/ThanhVienHopDong/XoaThanhVien/{w.MemberB}", null)).StatusCode);

            // Không gắn được người thuê chỉ thuộc chi nhánh B vào hợp đồng / phòng của chi nhánh A (theo id hoặc CCCD).
            var cccdB = await ReadAsync(_factory, db => db.NguoiThues.Where(x => x.NguoiThueId == w.TenantB).Select(x => x.CCCD).SingleAsync());
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, "/HopDong/Create",
                new { PhongTroId = w.RoomA, NguoiThueId = w.TenantB, ThoiDiemBatDau = System.DateTime.UtcNow, TienCocPhong = 1m, TienThuePhong = 1m, TrangThaiHopDong = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, "/ThanhVienHopDong/AddThanhVien",
                new { HopDongId = w.ContractA, NguoiThueId = w.TenantB, NgayVao = System.DateTime.UtcNow })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, "/ThanhVienHopDong/AddThanhVien",
                new { HopDongId = w.ContractA, HoVaTen = "Gia mao", SoDienThoai = "0900000009", CCCD = cccdB, NgayVao = System.DateTime.UtcNow })).StatusCode);

            var (contractDeleted, memberLeft, contractCount) = await ReadAsync(_factory, async db => (
                await db.HopDongs.Where(h => h.HopDongId == w.ContractB).Select(h => h.IsDeleted).SingleAsync(),
                await db.ChiTietThanhVienHopDongs.Where(m => m.ChiTietThanhVienHopDongId == w.MemberB).Select(m => m.NgayChuyenDi != null || m.IsDeleted).SingleAsync(),
                await db.HopDongs.CountAsync(h => h.PhongTroId == w.RoomB)));
            Assert.False(contractDeleted);
            Assert.False(memberLeft);
            Assert.Equal(1, contractCount);
        }

        [Fact]
        public async Task NguoiThue_StaffOfBranchA_IsBlockedFromBranchB()
        {
            var w = await SeedAsync(_factory);
            var (staff, token) = await LoginAsync(_factory, w.Staff);

            // Danh sách: có người thuê chi nhánh A và người chưa có hợp đồng, không có người chỉ thuộc chi nhánh B.
            var all = await JsonAsync(await staff.GetAsync("/NguoiThue/GetAll"));
            var ids = all.EnumerateArray().Select(x => x.GetProperty("nguoiThueId").GetInt32()).ToList();
            Assert.Contains(w.TenantA, ids);
            Assert.Contains(w.TenantFree, ids);
            Assert.DoesNotContain(w.TenantB, ids);

            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/NguoiThue/GetById/{w.TenantA}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/NguoiThue/GetById/{w.TenantFree}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/NguoiThue/GetById/{w.TenantB}")).StatusCode);

            var update = new { NguoiThueId = w.TenantB, HoVaTen = "Bi sua", SoDienThoai = "0999999999", CCCD = "079999999999", Email = "sua@test.com" };
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Put, $"/NguoiThue/Update/{w.TenantB}", update)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Delete, $"/NguoiThue/Delete/{w.TenantB}", null)).StatusCode);

            var (name, deleted) = await ReadAsync(_factory, async db =>
            {
                var t = await db.NguoiThues.SingleAsync(x => x.NguoiThueId == w.TenantB);
                return (t.HoVaTen, t.IsDeleted);
            });
            Assert.StartsWith("Khach B", name);
            Assert.False(deleted);
        }

        [Fact]
        public async Task DichVu_StaffOfBranchA_IsBlockedFromBranchB()
        {
            var w = await SeedAsync(_factory);
            var (dichVuId, giaA, giaB) = await ReadAsync(_factory, async db =>
            {
                var dv = new Domain.Entities.DichVu { TenDichVu = "DV " + System.Guid.NewGuid().ToString("N")[..8], DonVi = "tháng" };
                db.DichVus.Add(dv);
                await db.SaveChangesAsync();
                var a = new Domain.Entities.DichVuChiNhanh { ChiNhanhId = w.BranchA, DichVuId = dv.DichVuId, GiaDichVu = 100000m };
                var b = new Domain.Entities.DichVuChiNhanh { ChiNhanhId = w.BranchB, DichVuId = dv.DichVuId, GiaDichVu = 200000m };
                db.DichVuChiNhanhs.AddRange(a, b);
                await db.SaveChangesAsync();
                return (dv.DichVuId, a.DichVuChiNhanhId, b.DichVuChiNhanhId);
            });
            var (staff, token) = await LoginAsync(_factory, w.Staff);

            // Bảng giá: danh sách không có dòng chi nhánh B; xem theo id 404; thêm, sửa, chuyển, xóa 403.
            var list = await JsonAsync(await PostFormAsync(staff, token, "/DichVuChiNhanh/GetList", new Dictionary<string, string>
            {
                ["draw"] = "1", ["start"] = "0", ["length"] = "500", ["chiNhanhId"] = "0"
            }));
            var ids = list.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("dichVuChiNhanhId").GetInt32()).ToList();
            Assert.Contains(giaA, ids);
            Assert.DoesNotContain(giaB, ids);

            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/DichVuChiNhanh/GetById/{giaB}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Put, $"/DichVuChiNhanh/Update/{giaB}",
                new { DichVuChiNhanhId = giaB, ChiNhanhId = w.BranchB, DichVuId = dichVuId, GiaDichVu = 1m })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Put, $"/DichVuChiNhanh/Update/{giaA}",
                new { DichVuChiNhanhId = giaA, ChiNhanhId = w.BranchB, DichVuId = dichVuId, GiaDichVu = 1m })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Delete, $"/DichVuChiNhanh/Delete/{giaB}", null)).StatusCode);

            // Đăng ký dịch vụ: phòng chi nhánh B bị chặn; phòng A không được dùng bảng giá của chi nhánh B.
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/DangKyDichVu/GetByPhong?phongTroId={w.RoomB}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/DangKyDichVu/GetByPhong?phongTroId={w.RoomA}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, "/DangKyDichVu/Luu",
                new { PhongTroId = w.RoomB, DichVus = new[] { new { DichVuChiNhanhId = giaB, IsSelected = true, SoLuong = 1 } } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Post, "/DangKyDichVu/Luu",
                new { PhongTroId = w.RoomA, DichVus = new[] { new { DichVuChiNhanhId = giaB, IsSelected = true, SoLuong = 1 } } })).StatusCode);

            // Danh mục dịch vụ hệ thống chỉ Admin sửa: nhân viên không xóa được.
            Assert.NotEqual(HttpStatusCode.OK, (await SendJsonAsync(staff, token, System.Net.Http.HttpMethod.Delete, $"/DichVu/Delete/{dichVuId}", null)).StatusCode);

            var (priceB, deletedB, catalogDeleted, dangKyCount) = await ReadAsync(_factory, async db => (
                await db.DichVuChiNhanhs.Where(x => x.DichVuChiNhanhId == giaB).Select(x => x.GiaDichVu).SingleAsync(),
                await db.DichVuChiNhanhs.Where(x => x.DichVuChiNhanhId == giaB).Select(x => x.IsDeleted).SingleAsync(),
                await db.DichVus.Where(x => x.DichVuId == dichVuId).Select(x => x.IsDeleted).SingleAsync(),
                await db.DangKyDichVus.CountAsync(x => x.DichVuChiNhanhId == giaB)));
            Assert.Equal(200000m, priceB);
            Assert.False(deletedB);
            Assert.False(catalogDeleted);
            Assert.Equal(0, dangKyCount);
        }

        [Fact]
        public async Task Dashboard_StaffOfBranchA_SeesOnlyBranchA()
        {
            var w = await SeedAsync(_factory);
            var (staff, _) = await LoginAsync(_factory, w.Staff);
            var now = System.DateTime.Now;

            // Không chọn chi nhánh: chỉ tính chi nhánh A (đúng 1 phòng, 1 hợp đồng đang hoạt động).
            var mine = await JsonAsync(await staff.GetAsync($"/QuanLyNhaTro/Dashboard/GetAjaxData?year={now.Year}&month={now.Month}"));
            Assert.Equal(1, mine.GetProperty("tongSoPhong").GetInt32());
            Assert.Equal(1, mine.GetProperty("tongSoHopDongHoatDong").GetInt32());

            // Cố chọn chi nhánh B: mọi số liệu bằng 0.
            var other = await JsonAsync(await staff.GetAsync($"/QuanLyNhaTro/Dashboard/GetAjaxData?branchId={w.BranchB}&year={now.Year}&month={now.Month}"));
            Assert.Equal(0, other.GetProperty("tongSoPhong").GetInt32());
            Assert.Equal(0, other.GetProperty("tongSoHopDongHoatDong").GetInt32());

            // Dropdown chi nhánh trên Dashboard không có chi nhánh B.
            var page = await (await staff.GetAsync("/QuanLyNhaTro/Dashboard")).Content.ReadAsStringAsync();
            Assert.DoesNotContain(w.BranchNameB, page);

            var (admin, _) = await LoginAsync(_factory, w.Admin);
            var adminB = await JsonAsync(await admin.GetAsync($"/QuanLyNhaTro/Dashboard/GetAjaxData?branchId={w.BranchB}&year={now.Year}&month={now.Month}"));
            Assert.Equal(1, adminB.GetProperty("tongSoPhong").GetInt32());
        }

        [Fact]
        public async Task SuCo_StaffOfBranchA_IsBlockedFromBranchB()
        {
            var w = await SeedAsync(_factory);
            var s = System.Guid.NewGuid().ToString("N")[..8];
            var (incidentA, incidentB) = await ReadAsync(_factory, async db =>
            {
                QuanLyChoThuePhongTroWeb.Domain.Entities.YeuCauSuCo Incident(int room, int tenant, string title) => new()
                {
                    PhongTroId = room, NguoiThueId = tenant, TieuDe = title, MoTa = "M",
                    TrangThai = QuanLyChoThuePhongTroWeb.Domain.Enums.TrangThaiSuCo.ChoTiepNhan, NgayGui = System.DateTime.UtcNow
                };
                var a = Incident(w.RoomA, w.TenantA, $"SuCoA_{s}");
                var b = Incident(w.RoomB, w.TenantB, $"SuCoB_{s}");
                db.YeuCauSuCos.AddRange(a, b);
                await db.SaveChangesAsync();
                return (a.Id, b.Id);
            });
            var (staff, token) = await LoginAsync(_factory, w.Staff, "/QuanLyNhaTro/YeuCauSuCo");

            // Danh sách và dropdown chi nhánh chỉ có chi nhánh A, kể cả khi cố lọc chi nhánh B.
            var page = await (await staff.GetAsync("/QuanLyNhaTro/YeuCauSuCo")).Content.ReadAsStringAsync();
            Assert.Contains($"SuCoA_{s}", page);
            Assert.DoesNotContain($"SuCoB_{s}", page);
            Assert.DoesNotContain(w.BranchNameB, page);
            var filteredB = await (await staff.GetAsync($"/QuanLyNhaTro/YeuCauSuCo?chiNhanhId={w.BranchB}")).Content.ReadAsStringAsync();
            Assert.DoesNotContain($"SuCoB_{s}", filteredB);

            // Cập nhật / xóa sự cố chi nhánh B bị từ chối, dữ liệu không đổi.
            var update = await JsonAsync(await PostFormAsync(staff, token, "/QuanLyNhaTro/YeuCauSuCo/UpdateStatus", new Dictionary<string, string>
            {
                ["Id"] = incidentB.ToString(),
                ["TrangThai"] = "1",
                ["ChiPhiSuaChua"] = "0",
                ["CongVaoHoaDon"] = "false",
                ["LyDoTuChoi"] = "",
                ["GhiChuAdmin"] = "sua"
            }));
            Assert.False(update.GetProperty("success").GetBoolean());
            var delete = await JsonAsync(await PostFormAsync(staff, token, "/QuanLyNhaTro/YeuCauSuCo/Delete", new Dictionary<string, string>
            {
                ["id"] = incidentB.ToString()
            }));
            Assert.False(delete.GetProperty("success").GetBoolean());

            var (stateB, deletedB) = await ReadAsync(_factory, async db =>
            {
                var b = await db.YeuCauSuCos.AsNoTracking().SingleAsync(x => x.Id == incidentB);
                return (b.TrangThai, b.IsDeleted);
            });
            Assert.Equal(QuanLyChoThuePhongTroWeb.Domain.Enums.TrangThaiSuCo.ChoTiepNhan, stateB);
            Assert.False(deletedB);

            // Admin thấy sự cố và chi nhánh B.
            var (admin, _) = await LoginAsync(_factory, w.Admin, "/QuanLyNhaTro/YeuCauSuCo");
            var adminPage = await (await admin.GetAsync("/QuanLyNhaTro/YeuCauSuCo")).Content.ReadAsStringAsync();
            Assert.Contains($"SuCoB_{s}", adminPage);
            Assert.Contains(w.BranchNameB, adminPage);
        }

        [Fact]
        public async Task PhongTro_Admin_SeesAllBranches()
        {
            var w = await SeedAsync(_factory);
            var (admin, _) = await LoginAsync(_factory, w.Admin);

            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/PhongTros/GetPhongTro?id={w.RoomB}")).StatusCode);
            var page = await (await admin.GetAsync("/PhongTros/QuanLyPhongTro")).Content.ReadAsStringAsync();
            Assert.Contains(w.BranchNameB, page);
        }
    }
}
