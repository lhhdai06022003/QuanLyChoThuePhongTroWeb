using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Truy vấn đọc của báo cáo thống kê. Kỳ dùng năm 2004 để không đụng test khác; mỗi test chạy trong transaction rồi RollbackAsync.
    [Collection("PostgreSqlCollection")]
    public class BaoCaoStoreTests
    {
        private static readonly DateTime Now = new(2004, 6, 15, 5, 0, 0, DateTimeKind.Utc);

        private readonly PostgreSqlFixture _fixture;

        public BaoCaoStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class StubAccess : IEmployeeAccessService
        {
            public Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default) =>
                Task.FromResult<EmployeeAccessScope?>(new EmployeeAccessScope(1, true));
        }

        private sealed class FixedTime : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(Now);
        }

        private sealed class World
        {
            public DebtTestSeeder Seed = null!;
            public ChiNhanh A = null!;
            public ChiNhanh B = null!;
            public NguoiThue Tenant = null!;
        }

        private static async Task<World> NewWorldAsync(ApplicationDbContext db)
        {
            var seed = new DebtTestSeeder(db);
            return new World
            {
                Seed = seed,
                A = await seed.BranchAsync("A"),
                B = await seed.BranchAsync("B"),
                Tenant = await seed.TenantAsync("A")
            };
        }

        private static async Task<HopDong> NewContractAsync(World w, ChiNhanh branch, DateTime? start = null, DateTime? end = null,
            TrangThaiHopDong status = TrangThaiHopDong.DangHoatDong, bool deleted = false, decimal deposit = 1m)
        {
            var room = await w.Seed.RoomAsync(branch, "R");
            var contract = await w.Seed.ContractAsync(room, w.Tenant, start, end, status, deleted);
            if (deposit != 1m)
            {
                contract.TienCocPhong = deposit;
            }

            return contract;
        }

        private static async Task<LichSuThanhToan> AddLedgerAsync(
            ApplicationDbContext db, HoaDon hoaDon, decimal soTien, PhuongThucThanhToan phuongThuc, string maGiaoDich,
            string ghiChu, DateTime ngayThanhToanUtc, bool deleted = false)
        {
            var row = new LichSuThanhToan
            {
                HoaDonId = hoaDon.HoaDonId,
                MaGiaoDich = maGiaoDich + "-" + Guid.NewGuid().ToString("N")[..8],
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = ngayThanhToanUtc,
                GhiChu = ghiChu,
                IsDeleted = deleted
            };
            db.LichSuThanhToans.Add(row);
            await db.SaveChangesAsync();
            return row;
        }

        private static DateTime Utc(int y, int m, int d, int h = 3) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

        // ----- Doanh thu -----

        [Fact]
        public async Task HoaDonDaGui_ChiLayDaGuiChuaXoaTrongKhoangKy()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);

            const TrangThaiHoaDon chua = TrangThaiHoaDon.ChuaThanhToan;
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.Nhap, chua, 100m, null, month: 4, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.ChoDuyet, chua, 100m, null, month: 4, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaChot, chua, 100m, null, month: 4, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 100m, null, deleted: true, month: 4, year: 2004);
            var ok4 = await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 400m, null, month: 4, year: 2004);
            var ok5 = await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 500m, null, month: 5, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 600m, null, month: 6, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.B), TrangThaiPhatHanhHoaDon.DaGui, chua, 700m, null, month: 4, year: 2004);

            var store = new BaoCaoStore(db);
            var rows = await store.GetHoaDonDaGuiAsync(w.A.ChiNhanhId, null, 200404, 200405);

            Assert.Equal(new[] { ok4.HoaDonId, ok5.HoaDonId }.OrderBy(x => x), rows.Select(r => r.HoaDonId).OrderBy(x => x));
            Assert.All(rows, r => Assert.Equal(w.A.ChiNhanhId, r.ChiNhanhId));

            // Phạm vi rỗng thì không thấy dòng nào; phạm vi chi nhánh khác không lẫn vào.
            Assert.Empty(await store.GetHoaDonDaGuiAsync(null, Array.Empty<int>(), 200404, 200405));
            Assert.Empty(await store.GetHoaDonDaGuiAsync(w.A.ChiNhanhId, new[] { w.B.ChiNhanhId }, 200404, 200405));
            Assert.Equal(2, (await store.GetHoaDonDaGuiAsync(null, new[] { w.A.ChiNhanhId }, 200404, 200405)).Count);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task ThanhToan_BoDongXoaMem_VaGiuDongTraTreThuocKyCuaHoaDon()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);

            var inv = await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000m, null, month: 1, year: 2004);
            await AddLedgerAsync(db, inv, 100m, PhuongThucThanhToan.TienMat, "TM", "", Utc(2004, 1, 20));
            await AddLedgerAsync(db, inv, 200m, PhuongThucThanhToan.ChuyenKhoan, "CK", "", Utc(2004, 2, 20)); // trả trễ: tháng sau
            await AddLedgerAsync(db, inv, 999m, PhuongThucThanhToan.TienMat, "XOA", "", Utc(2004, 1, 21), deleted: true);

            var rows = await new BaoCaoStore(db).GetThanhToanCuaHoaDonDaGuiAsync(w.A.ChiNhanhId, null, 200401, 200401);

            Assert.Equal(new[] { 100m, 200m }, rows.Select(r => r.SoTien).OrderBy(x => x).ToArray());
            Assert.All(rows, r => Assert.Equal(inv.HoaDonId, r.HoaDonId));
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task TienCocDangGiu_ChiTinhHopDongDangHoatDongChuaXoa()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);

            await NewContractAsync(w, w.A, deposit: 1_000m);
            await NewContractAsync(w, w.A, deposit: 2_000m);
            await NewContractAsync(w, w.A, status: TrangThaiHopDong.DaKetThuc, deposit: 4_000m);
            await NewContractAsync(w, w.A, status: TrangThaiHopDong.DaHuy, deposit: 8_000m);
            await NewContractAsync(w, w.A, deleted: true, deposit: 16_000m);
            await NewContractAsync(w, w.B, deposit: 32_000m);
            await db.SaveChangesAsync();

            var store = new BaoCaoStore(db);
            Assert.Equal(3_000m, await store.GetTienCocDangGiuAsync(w.A.ChiNhanhId, null));
            Assert.Equal(32_000m, await store.GetTienCocDangGiuAsync(null, new[] { w.B.ChiNhanhId }));
            Assert.Equal(0m, await store.GetTienCocDangGiuAsync(null, Array.Empty<int>()));
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GiuChoDaNhanTien_TraDungDaNhanDaApDungDaHoan()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);
            var suffix = w.Seed.Suffix;

            var staff = new NguoiDung { TenDangNhap = "bc_staff_" + suffix, MatKhauHash = "h", Role = Role.NhanVien };
            db.NguoiDungs.Add(staff);
            await db.SaveChangesAsync();

            async Task<YeuCauGiuCho> HoldAsync(string tag, TrangThaiYeuCauGiuCho status, decimal received)
            {
                var guestUser = new NguoiDung { TenDangNhap = $"bc_g_{tag}_{suffix}", MatKhauHash = "h", Role = Role.KhachVangLai };
                db.NguoiDungs.Add(guestUser);
                await db.SaveChangesAsync();
                var guest = new KhachVangLai { NguoiDungId = guestUser.NguoiDungId, HoTen = "Khach " + tag, SoDienThoai = "0911222333" };
                db.KhachVangLais.Add(guest);
                var room = await w.Seed.RoomAsync(w.A, "G");
                await db.SaveChangesAsync();
                var hold = new YeuCauGiuCho { KhachVangLaiId = guest.KhachVangLaiId, PhongTroId = room.PhongTroId, TrangThai = status, SoTienGiuCho = received };
                db.YeuCauGiuChos.Add(hold);
                await db.SaveChangesAsync();
                var payment = new YeuCauThanhToanGiuCho
                {
                    YeuCauGiuChoId = hold.YeuCauGiuChoId,
                    MaYeuCau = $"BC{tag}{suffix}",
                    NoiDungChuyenKhoan = $"BC{tag}{suffix}",
                    SoTien = received,
                    HanThanhToan = Now.AddDays(1),
                    NguoiTaoId = staff.NguoiDungId,
                    TrangThai = TrangThaiYeuCauThanhToan.DaHoanTat
                };
                db.YeuCauThanhToanGiuChos.Add(payment);
                await db.SaveChangesAsync();
                db.GiaoDichGiuChos.Add(new GiaoDichGiuCho
                {
                    YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                    MaGiaoDich = $"BCPAY-{tag}-{suffix}",
                    SoTienThucNhan = received,
                    PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                    NguoiXacNhanId = staff.NguoiDungId,
                    NgayThucNhan = Now
                });
                await db.SaveChangesAsync();
                return hold;
            }

            // Hold 1: nhận 500.000, quyết định hoàn đã hoàn 200.000.
            var h1 = await HoldAsync("1", TrangThaiYeuCauGiuCho.DaHuy, 500_000m);
            var decision = new QuyetDinhHoanTienGiuCho
            {
                YeuCauGiuChoId = h1.YeuCauGiuChoId,
                SoTienHoanDuyet = 200_000m,
                LyDo = "Khach huy",
                NguoiQuyetDinhId = staff.NguoiDungId,
                NgayQuyetDinh = Now,
                TrangThai = TrangThaiHoanTien.DaHoanTien
            };
            db.QuyetDinhHoanTienGiuChos.Add(decision);
            await db.SaveChangesAsync();
            db.GiaoDichHoanTienGiuChos.Add(new GiaoDichHoanTienGiuCho
            {
                QuyetDinhHoanTienGiuChoId = decision.QuyetDinhHoanTienGiuChoId,
                SoTienHoan = 200_000m,
                MaGiaoDichHoan = $"BCRF-{suffix}",
                NguoiXacNhanId = staff.NguoiDungId,
                NgayHoan = Now
            });

            // Hold 2: nhận 300.000, đã áp 300.000 vào cọc của hợp đồng.
            var h2 = await HoldAsync("2", TrangThaiYeuCauGiuCho.DaChuyenHopDong, 300_000m);
            var contract = await NewContractAsync(w, w.A);
            db.ApDungTienGiuChoVaoTienCocs.Add(new ApDungTienGiuChoVaoTienCoc
            {
                YeuCauGiuChoId = h2.YeuCauGiuChoId,
                HopDongId = contract.HopDongId,
                SoTienApDung = 300_000m,
                NguoiThucHienId = staff.NguoiDungId
            });

            // Hold 3: chi nhánh B, không lẫn vào kết quả của A.
            var roomB = await w.Seed.RoomAsync(w.B, "G");
            await db.SaveChangesAsync();
            var guestB = new NguoiDung { TenDangNhap = "bc_gb_" + suffix, MatKhauHash = "h", Role = Role.KhachVangLai };
            db.NguoiDungs.Add(guestB);
            await db.SaveChangesAsync();
            var kvlB = new KhachVangLai { NguoiDungId = guestB.NguoiDungId, HoTen = "B", SoDienThoai = "0911222333" };
            db.KhachVangLais.Add(kvlB);
            await db.SaveChangesAsync();
            db.YeuCauGiuChos.Add(new YeuCauGiuCho { KhachVangLaiId = kvlB.KhachVangLaiId, PhongTroId = roomB.PhongTroId, TrangThai = TrangThaiYeuCauGiuCho.DaHuy, SoTienGiuCho = 1m });
            // Hold 4: chưa nhận tiền, không được trả về.
            var guestC = new NguoiDung { TenDangNhap = "bc_gc_" + suffix, MatKhauHash = "h", Role = Role.KhachVangLai };
            db.NguoiDungs.Add(guestC);
            await db.SaveChangesAsync();
            var kvlC = new KhachVangLai { NguoiDungId = guestC.NguoiDungId, HoTen = "C", SoDienThoai = "0911222333" };
            db.KhachVangLais.Add(kvlC);
            var roomC = await w.Seed.RoomAsync(w.A, "G");
            await db.SaveChangesAsync();
            db.YeuCauGiuChos.Add(new YeuCauGiuCho { KhachVangLaiId = kvlC.KhachVangLaiId, PhongTroId = roomC.PhongTroId, TrangThai = TrangThaiYeuCauGiuCho.DaHuy, SoTienGiuCho = 1m });
            await db.SaveChangesAsync();

            var rows = await new BaoCaoStore(db).GetGiuChoDaNhanTienAsync(w.A.ChiNhanhId, null);

            Assert.Equal(2, rows.Count);
            var r1 = rows.Single(r => r.YeuCauGiuChoId == h1.YeuCauGiuChoId);
            Assert.Equal(500_000m, r1.DaNhan);
            Assert.Equal(0m, r1.DaApDung);
            Assert.True(r1.CoQuyetDinh);
            Assert.Equal(TrangThaiHoanTien.DaHoanTien, r1.TrangThaiQuyetDinh);
            Assert.Equal(200_000m, r1.DaHoan);
            Assert.Equal(Now, r1.NgayQuyetDinhUtc);
            var r2 = rows.Single(r => r.YeuCauGiuChoId == h2.YeuCauGiuChoId);
            Assert.Equal(300_000m, r2.DaNhan);
            Assert.Equal(300_000m, r2.DaApDung);
            Assert.False(r2.CoQuyetDinh);
            Assert.Null(r2.TrangThaiQuyetDinh);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task DoanhThu_DaThuKhopDashboard_VaTachDungKenh()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);

            var inv = await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 2_000_000m, Now.AddDays(5), month: 3, year: 2004);
            var day = Utc(2004, 3, 10);
            await AddLedgerAsync(db, inv, 100_000m, PhuongThucThanhToan.TienMat, "TM", "", day);
            await AddLedgerAsync(db, inv, 200_000m, PhuongThucThanhToan.ChuyenKhoan, "CK", "", day);
            await AddLedgerAsync(db, inv, 300_000m, PhuongThucThanhToan.ChuyenKhoan, "SEPAY-X", "", day);
            await AddLedgerAsync(db, inv, 400_000m, PhuongThucThanhToan.ChuyenKhoan, "VNPAY-X", "", day);
            await AddLedgerAsync(db, inv, 50_000m, PhuongThucThanhToan.CanTruTienCoc, "CANTRU-HD1-1", "", day);
            await AddLedgerAsync(db, inv, 777m, PhuongThucThanhToan.TienMat, "XOA", "", day, deleted: true);

            var service = new BaoCaoService(new BaoCaoStore(db), new StubAccess(), null!, new FixedTime());
            var result = await service.GetDoanhThuAsync(1, new BaoCaoFilter { ChiNhanhId = w.A.ChiNhanhId, TuThang = 1, TuNam = 2004, DenThang = 6, DenNam = 2004 });
            Assert.True(result.Success, result.Message);

            var dashboard = (await new DashboardStore(db).GetInvoiceMonthlyStatsAsync(w.A.ChiNhanhId, 2004)).Single(s => s.Thang == 3).DaThu;
            var t3 = result.Data!.TheoKy.Single(k => k.Thang == 3);
            Assert.Equal(dashboard, t3.DaThu);
            Assert.Equal(1_050_000m, t3.DaThu);
            Assert.Equal(100_000m, t3.TienMat);
            Assert.Equal(200_000m, t3.ChuyenKhoan);
            Assert.Equal(300_000m, t3.SePay);
            Assert.Equal(400_000m, t3.VnPay);
            Assert.Equal(50_000m, t3.CanTruCoc);
            Assert.Equal(1_000_000m, t3.ThuTien);
            Assert.Equal(950_000m, t3.ConNo);
            Assert.Equal(1_050_000m, result.Data.Tong.DaThu);
            await tx.RollbackAsync();
        }

        // ----- Công nợ -----

        [Fact]
        public async Task HoaDonConNo_KhopDashboardQuaHan_VaBoHoaDonNhapDaThuDu()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);
            const TrangThaiHoaDon chua = TrangThaiHoaDon.ChuaThanhToan;

            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 1_000m, Now.AddDays(-40), paid: 300m, month: 1, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 500m, Now.AddDays(-1), month: 2, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 400m, Now.AddDays(9), month: 3, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, chua, 250m, null, month: 4, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.Nhap, chua, 999m, Now.AddDays(-9), month: 5, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.A), TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, 700m, Now.AddDays(-9), paid: 700m, month: 6, year: 2004);
            await w.Seed.InvoiceAsync(await NewContractAsync(w, w.B), TrangThaiPhatHanhHoaDon.DaGui, chua, 111m, Now.AddDays(-9), month: 7, year: 2004);

            var store = new BaoCaoStore(db);
            var rows = await store.GetHoaDonConNoAsync(w.A.ChiNhanhId, null);
            Assert.Equal(4, rows.Count);

            var builder = BaoCaoCongNoBuilder.Build(new BaoCaoPhamViDto(), rows, Now);
            var dashboard = await new DashboardStore(db).GetOverdueSummaryAsync(w.A.ChiNhanhId, Now);
            Assert.Equal(dashboard.TongConNo, builder.QuaHan);
            Assert.Equal(dashboard.SoHoaDon, builder.SoHoaDonQuaHan);
            Assert.Equal(700m + 500m + 400m + 250m, builder.TongConNo);

            Assert.Empty(await store.GetHoaDonConNoAsync(null, Array.Empty<int>()));
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HoaDonConNo_DaTraPhong_TheoQuyTacBoLocHopDongDaKetThuc()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);
            const TrangThaiHoaDon chua = TrangThaiHoaDon.ChuaThanhToan;

            // Hợp đồng đã kết thúc, khách không còn hợp đồng đang hoạt động ở phòng đó: đã trả phòng.
            var traPhong = await NewContractAsync(w, w.A, status: TrangThaiHopDong.DaKetThuc);
            await w.Seed.InvoiceAsync(traPhong, TrangThaiPhatHanhHoaDon.DaGui, chua, 100m, Now.AddDays(-5), month: 1, year: 2004);

            // Hợp đồng đã kết thúc nhưng cùng phòng và cùng người thuê có hợp đồng đang hoạt động (gia hạn): chưa trả phòng.
            var room = await w.Seed.RoomAsync(w.A, "GH");
            var daKetThuc = await w.Seed.ContractAsync(room, w.Tenant, status: TrangThaiHopDong.DaKetThuc);
            await w.Seed.ContractAsync(room, w.Tenant, new DateTime(2003, 6, 1, 0, 0, 0, DateTimeKind.Utc), null, TrangThaiHopDong.DangHoatDong);
            await w.Seed.InvoiceAsync(daKetThuc, TrangThaiPhatHanhHoaDon.DaGui, chua, 200m, Now.AddDays(-5), month: 2, year: 2004);

            var rows = await new BaoCaoStore(db).GetHoaDonConNoAsync(w.A.ChiNhanhId, null);
            Assert.True(rows.Single(r => r.TongTien == 100m).DaTraPhong);
            Assert.False(rows.Single(r => r.TongTien == 200m).DaTraPhong);
            await tx.RollbackAsync();
        }

        // ----- Công suất, hợp đồng -----

        [Fact]
        public async Task PhongVaHopDong_BoDaHuyXoaMem_VaCoLapChiNhanh()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);

            var okRoom = await w.Seed.RoomAsync(w.A, "OK");
            var deletedRoom = await w.Seed.RoomAsync(w.A, "DEL");
            deletedRoom.IsDeleted = true;
            await w.Seed.RoomAsync(w.B, "OTHER");

            var ok = await w.Seed.ContractAsync(okRoom, w.Tenant);
            await w.Seed.ContractAsync(await w.Seed.RoomAsync(w.A, "H"), w.Tenant, status: TrangThaiHopDong.DaHuy);
            await w.Seed.ContractAsync(await w.Seed.RoomAsync(w.A, "D"), w.Tenant, deleted: true);
            var ended = await w.Seed.ContractAsync(await w.Seed.RoomAsync(w.A, "E"), w.Tenant, status: TrangThaiHopDong.DaKetThuc);
            await w.Seed.ContractAsync(await w.Seed.RoomAsync(w.B, "B"), w.Tenant);
            await db.SaveChangesAsync();

            var store = new BaoCaoStore(db);
            var phongs = await store.GetPhongAsync(w.A.ChiNhanhId, null);
            Assert.DoesNotContain(phongs, p => p.PhongTroId == deletedRoom.PhongTroId);
            Assert.Contains(phongs, p => p.PhongTroId == okRoom.PhongTroId);
            Assert.All(phongs, p => Assert.Equal(w.A.ChiNhanhId, p.ChiNhanhId));

            var hopDongs = await store.GetHopDongAsync(w.A.ChiNhanhId, null);
            Assert.Equal(new[] { ok.HopDongId, ended.HopDongId }.OrderBy(x => x), hopDongs.Select(h => h.HopDongId).OrderBy(x => x));

            Assert.Empty(await store.GetPhongAsync(null, Array.Empty<int>()));
            Assert.Empty(await store.GetHopDongAsync(w.A.ChiNhanhId, new[] { w.B.ChiNhanhId }));
            await tx.RollbackAsync();
        }

        // ----- Điện nước -----

        [Fact]
        public async Task DienNuoc_ChiLayBanGhiDaDuyetChuaXoaTrongKhoang()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);

            DichVuDienNuocCuaPhong Rec(PhongTro room, int month, TrangThaiGhiNhan status, bool deleted = false) => new()
            {
                PhongTroId = room.PhongTroId,
                Thang = month,
                Nam = 2004,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 150,
                DonGiaDien = 3000m,
                ChiSoNuocCu = 10,
                ChiSoNuocMoi = 15,
                DonGiaNuoc = 10000m,
                TrangThaiGhiNhan = status,
                IsDeleted = deleted
            };

            var room = await w.Seed.RoomAsync(w.A, "DN");
            var roomB = await w.Seed.RoomAsync(w.B, "DN");
            db.DichVuDienNuocCuaPhongs.AddRange(
                Rec(room, 3, TrangThaiGhiNhan.DaDuyet),
                Rec(room, 4, TrangThaiGhiNhan.Nhap),
                Rec(room, 5, TrangThaiGhiNhan.ChoDuyet),
                Rec(room, 6, TrangThaiGhiNhan.TuChoi),
                Rec(room, 7, TrangThaiGhiNhan.DaDuyet, deleted: true),
                Rec(room, 9, TrangThaiGhiNhan.DaDuyet),
                Rec(roomB, 3, TrangThaiGhiNhan.DaDuyet));
            await db.SaveChangesAsync();

            var store = new BaoCaoStore(db);
            var rows = await store.GetDienNuocDaDuyetAsync(w.A.ChiNhanhId, null, 200401, 200808);
            Assert.Equal(new[] { 3, 9 }, rows.Select(r => r.Thang).OrderBy(x => x).ToArray());

            var trong = await store.GetDienNuocDaDuyetAsync(w.A.ChiNhanhId, null, 200403, 200403);
            Assert.Single(trong);
            Assert.Equal(150m, trong[0].DienMoi);
            Assert.Equal(3000m, trong[0].DonGiaDien);

            Assert.Empty(await store.GetDienNuocDaDuyetAsync(null, Array.Empty<int>(), 200401, 200412));
            Assert.Empty(await store.GetDienNuocDaDuyetAsync(w.A.ChiNhanhId, new[] { w.B.ChiNhanhId }, 200401, 200412));
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetTenChiNhanh_ChiNhanhDaXoaTraNull()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await NewWorldAsync(db);

            Assert.Equal(w.A.TenChiNhanh, await new BaoCaoStore(db).GetTenChiNhanhAsync(w.A.ChiNhanhId));
            w.B.IsDeleted = true;
            await db.SaveChangesAsync();
            Assert.Null(await new BaoCaoStore(db).GetTenChiNhanhAsync(w.B.ChiNhanhId));
            Assert.Null(await new BaoCaoStore(db).GetTenChiNhanhAsync(-5));
            await tx.RollbackAsync();
        }
    }
}
