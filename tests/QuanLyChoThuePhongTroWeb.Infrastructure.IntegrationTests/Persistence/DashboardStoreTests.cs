using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // KPI Dashboard theo định nghĩa công nợ mới: Chờ thu, Phòng đang nợ, Quá hạn và Đã thu chỉ tính hóa đơn DaGui
    // (Đã thu khớp Báo cáo).
    [Collection("PostgreSqlCollection")]
    public class DashboardStoreTests
    {
        private static readonly DateTime Now = new(2003, 6, 15, 5, 0, 0, DateTimeKind.Utc);

        private readonly PostgreSqlFixture _fixture;

        public DashboardStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class World
        {
            public ChiNhanh A = null!;
            public ChiNhanh B = null!;
        }

        private static async Task<World> SeedAsync(ApplicationDbContext db)
        {
            var seed = new DebtTestSeeder(db);
            var w = new World { A = await seed.BranchAsync("A"), B = await seed.BranchAsync("B") };
            var tenA = await seed.TenantAsync("A");
            var tenB = await seed.TenantAsync("B");

            async Task<HopDong> NewContract(ChiNhanh b, NguoiThue t)
            {
                var room = await seed.RoomAsync(b, "D");
                return await seed.ContractAsync(room, t);
            }

            const TrangThaiHoaDon Chua = TrangThaiHoaDon.ChuaThanhToan;

            // Tháng 4/2003, chi nhánh A.
            await seed.InvoiceAsync(await NewContract(w.A, tenA), TrangThaiPhatHanhHoaDon.Nhap, Chua, 500_000m, Now.AddDays(-9), paid: 100_000m, month: 4, year: 2003);
            await seed.InvoiceAsync(await NewContract(w.A, tenA), TrangThaiPhatHanhHoaDon.ChoDuyet, Chua, 500_000m, null, month: 4, year: 2003);
            await seed.InvoiceAsync(await NewContract(w.A, tenA), TrangThaiPhatHanhHoaDon.DaChot, Chua, 500_000m, null, month: 4, year: 2003);

            var room4 = await NewContract(w.A, tenA);
            await seed.InvoiceAsync(room4, TrangThaiPhatHanhHoaDon.DaGui, Chua, 1_000_000m, Now.AddDays(-1), paid: 300_000m, paidDeleted: 50_000m, month: 4, year: 2003);
            await seed.InvoiceAsync(room4, TrangThaiPhatHanhHoaDon.DaGui, Chua, 400_000m, Now, month: 5, year: 2003); // hạn đúng nowUtc

            await seed.InvoiceAsync(await NewContract(w.A, tenA), TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 200_000m, Now.AddDays(-2), paid: 200_000m, month: 4, year: 2003);
            await seed.InvoiceAsync(await NewContract(w.A, tenA), TrangThaiPhatHanhHoaDon.DaGui, Chua, 999_000m, Now.AddDays(-2), deleted: true, paid: 10_000m, month: 4, year: 2003);
            await seed.InvoiceAsync(await NewContract(w.A, tenA), TrangThaiPhatHanhHoaDon.DaGui, Chua, 111_000m, Now.AddDays(-2), month: 4, year: 2004); // năm khác

            // Chi nhánh B.
            await seed.InvoiceAsync(await NewContract(w.B, tenB), TrangThaiPhatHanhHoaDon.DaGui, Chua, 2_000_000m, Now.AddDays(-2), month: 4, year: 2003);
            return w;
        }

        [Fact]
        public async Task MonthlyStats_ChoThuVaDaThuChiTinhHoaDonDaGui()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var stats = await new DashboardStore(db).GetInvoiceMonthlyStatsAsync(w.A.ChiNhanhId, 2003);

            var april = stats.Single(s => s.Thang == 4);
            // Chờ thu: chỉ hóa đơn DaGui chưa thu đủ (1.000.000 - 300.000); Nhap/ChoDuyet/DaChot không tính, ledger xóa mềm không trừ.
            Assert.Equal(700_000m, april.ChoThu);
            // Đã thu: sổ chưa xóa của hóa đơn đã gửi, chưa xóa: 300.000 + 200.000; hóa đơn nháp (100.000) và hóa đơn xóa mềm bị loại.
            Assert.Equal(500_000m, april.DaThu);

            var may = stats.Single(s => s.Thang == 5);
            Assert.Equal(400_000m, may.ChoThu);
            Assert.Equal(0m, may.DaThu);

            // Năm khác không lẫn vào.
            Assert.DoesNotContain(stats, s => s.ChoThu == 111_000m);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task MonthlyStats_IsolatesBranches_AndEmptyScopeSeesNothing()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            var onlyB = await store.GetInvoiceMonthlyStatsAsync(null, 2003, new[] { w.B.ChiNhanhId });
            Assert.Equal(2_000_000m, Assert.Single(onlyB).ChoThu);

            Assert.Empty(await store.GetInvoiceMonthlyStatsAsync(null, 2003, Array.Empty<int>()));
            Assert.Empty(await store.GetInvoiceMonthlyStatsAsync(w.A.ChiNhanhId, 2003, new[] { w.B.ChiNhanhId }));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task CongNo_PhongDangNo_BoQuaHoaDonChuaGui_VaDemMoiPhongMotLan()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            // Phòng nợ: phòng có hai hóa đơn DaGui chưa thu (đếm một lần) và phòng năm 2004. Nháp/Chờ duyệt/Đã chốt/đã thu đủ/xóa mềm không tính.
            Assert.Equal(2, (await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 4, Now)).SoPhongDangNo);
            Assert.Equal(1, (await store.GetCongNoAsync(w.B.ChiNhanhId, 2003, 4, Now)).SoPhongDangNo);
            Assert.Equal(0, (await store.GetCongNoAsync(null, 2003, 4, Now, Array.Empty<int>())).SoPhongDangNo);
            Assert.Equal(0, (await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 4, Now, new[] { w.B.ChiNhanhId })).SoPhongDangNo);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task CongNo_ChoThuThang_ChiTinhHoaDonDaGuiCuaKyDangChon()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            Assert.Equal(700_000m, (await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 4, Now)).ChoThuThang);
            Assert.Equal(400_000m, (await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 5, Now)).ChoThuThang);
            Assert.Equal(0m, (await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 6, Now)).ChoThuThang);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task CongNo_QuaHan_TinhHoaDonDaGuiChuaThuDuQuaHan_VaLoaiHanBangNow()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            var atNow = await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 4, Now);
            var justAfter = await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 4, Now.AddTicks(10));

            // Chi nhánh A tại nowUtc: hóa đơn 4/2003 (còn nợ 700.000) và hóa đơn 4/2004 (111.000); hóa đơn hạn đúng nowUtc chưa quá hạn;
            // nháp quá hạn, xóa mềm, đã thu đủ không tính.
            Assert.Equal(2, atNow.SoHoaDonQuaHan);
            Assert.Equal(700_000m + 111_000m, atNow.TienQuaHan);
            Assert.Equal(3, justAfter.SoHoaDonQuaHan);
            Assert.Equal(700_000m + 111_000m + 400_000m, justAfter.TienQuaHan);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task CongNo_LocTheoChiNhanh_VaPhamViRongLaZero()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            var onlyB = await store.GetCongNoAsync(null, 2003, 4, Now, new[] { w.B.ChiNhanhId });
            Assert.Equal(1, onlyB.SoHoaDonQuaHan);
            Assert.Equal(2_000_000m, onlyB.TienQuaHan);

            var empty = await store.GetCongNoAsync(null, 2003, 4, Now, Array.Empty<int>());
            Assert.Equal(0, empty.SoHoaDonQuaHan);
            Assert.Equal(0m, empty.TienQuaHan);
            Assert.Equal(0m, empty.ChoThuThang);

            var outside = await store.GetCongNoAsync(w.A.ChiNhanhId, 2003, 4, Now, new[] { w.B.ChiNhanhId });
            Assert.Equal(0, outside.SoHoaDonQuaHan);

            await tx.RollbackAsync();
        }

        // Test 5: tiền mặt theo người thu trong tháng giờ VN.
        [Fact]
        public async Task TienMatTheoNguoi_ChiTienMatTrongThangVn_BoHuyVaKhongNguoiXacNhan_DungPhamVi()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var a = await seed.BranchAsync("A");
            var b = await seed.BranchAsync("B");
            var tenant = await seed.TenantAsync("T");
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var u1 = new NguoiDung { TenDangNhap = $"tm1_{suffix}", MatKhauHash = "x", Role = Role.NhanVien, IsActive = true };
            var u2 = new NguoiDung { TenDangNhap = $"tm2_{suffix}", MatKhauHash = "x", Role = Role.NhanVien, IsActive = true };
            db.NguoiDungs.AddRange(u1, u2);
            await db.SaveChangesAsync();

            var invA = await seed.InvoiceAsync(await seed.ContractAsync(await seed.RoomAsync(a, "T"), tenant), TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 9_000_000m, null, month: 6, year: 2003);
            var invB = await seed.InvoiceAsync(await seed.ContractAsync(await seed.RoomAsync(b, "T"), tenant), TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 9_000_000m, null, month: 6, year: 2003);
            // Không lọc theo trạng thái hóa đơn: hóa đơn nháp cũng tính nếu có sổ tiền mặt hợp lệ.
            var invNhap = await seed.InvoiceAsync(await seed.ContractAsync(await seed.RoomAsync(a, "T"), tenant), TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 9_000_000m, null, month: 6, year: 2003);

            var batDau = new DateTime(2003, 5, 31, 17, 0, 0, DateTimeKind.Utc);   // 01/06 00:00 giờ VN
            var ketThuc = new DateTime(2003, 6, 30, 17, 0, 0, DateTimeKind.Utc);  // 01/07 00:00 giờ VN

            void Them(HoaDon inv, decimal soTien, DateTime ngay, int? nguoiId, PhuongThucThanhToan pt = PhuongThucThanhToan.TienMat, bool deleted = false) =>
                db.LichSuThanhToans.Add(new LichSuThanhToan
                {
                    HoaDonId = inv.HoaDonId,
                    MaGiaoDich = $"TM-{Guid.NewGuid():N}",
                    SoTienThanhToan = soTien,
                    PhuongThucThanhToan = pt,
                    NgayThanhToan = ngay,
                    NguoiXacNhanId = nguoiId,
                    IsDeleted = deleted
                });

            Them(invA, 1_000m, batDau, u1.NguoiDungId);                                       // đúng biên đầu: tính
            Them(invA, 2_000m, ketThuc.AddSeconds(-1), u1.NguoiDungId);                       // 16:59:59Z cuối tháng: tính
            Them(invNhap, 4_000m, batDau.AddDays(3), u1.NguoiDungId);                         // hóa đơn nháp: vẫn tính
            Them(invA, 8_000m, batDau.AddSeconds(-1), u1.NguoiDungId);                        // trước biên đầu: không tính
            Them(invA, 16_000m, ketThuc, u1.NguoiDungId);                                     // đúng biên cuối: không tính
            Them(invA, 32_000m, batDau.AddDays(1), u1.NguoiDungId, deleted: true);            // đã hủy: không tính
            Them(invA, 64_000m, batDau.AddDays(1), null);                                     // không người xác nhận: không tính
            Them(invA, 128_000m, batDau.AddDays(1), u1.NguoiDungId, PhuongThucThanhToan.ChuyenKhoan); // không phải tiền mặt
            Them(invA, 256_000m, batDau.AddDays(2), u2.NguoiDungId);
            Them(invB, 512_000m, batDau.AddDays(2), u2.NguoiDungId);                          // chi nhánh B
            await db.SaveChangesAsync();

            var store = new DashboardStore(db);

            var cuaU1 = await store.GetTienMatTheoNguoiAsync(null, batDau, ketThuc, u1.NguoiDungId, new[] { a.ChiNhanhId });
            var dongU1 = Assert.Single(cuaU1);
            Assert.Equal(3, dongU1.SoKhoan);
            Assert.Equal(7_000m, dongU1.TongTien);
            Assert.Equal(u1.TenDangNhap, dongU1.Ten);

            var phamViA = await store.GetTienMatTheoNguoiAsync(null, batDau, ketThuc, null, new[] { a.ChiNhanhId });
            Assert.Equal(new[] { u1.NguoiDungId, u2.NguoiDungId }.OrderBy(x => x), phamViA.Select(x => x.NguoiDungId).OrderBy(x => x));
            Assert.Equal(256_000m, phamViA.Single(x => x.NguoiDungId == u2.NguoiDungId).TongTien);

            Assert.Empty(await store.GetTienMatTheoNguoiAsync(null, batDau, ketThuc, null, Array.Empty<int>()));

            // Admin (không giới hạn): hai dòng, người 2 gộp cả chi nhánh A và B.
            var admin = (await store.GetTienMatTheoNguoiAsync(null, batDau, ketThuc, null))
                .Where(x => x.NguoiDungId == u1.NguoiDungId || x.NguoiDungId == u2.NguoiDungId).ToList();
            Assert.Equal(2, admin.Count);
            var dong2 = admin.Single(x => x.NguoiDungId == u2.NguoiDungId);
            Assert.Equal(2, dong2.SoKhoan);
            Assert.Equal(256_000m + 512_000m, dong2.TongTien);

            var chiB = await store.GetTienMatTheoNguoiAsync(b.ChiNhanhId, batDau, ketThuc, null);
            Assert.Equal(512_000m, Assert.Single(chiB).TongTien);

            await tx.RollbackAsync();
        }
    }
}
