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
    [Collection("PostgreSqlCollection")]
    public class MeterConsumptionHistoryStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public MeterConsumptionHistoryStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

        // Mặc định kỳ đã duyệt: điện tiêu thụ = dien, nước tiêu thụ = dien / 10 để phân biệt các kỳ.
        private static async Task PeriodAsync(ApplicationDbContext db, PhongTro room, int thang, int nam, decimal dien,
            TrangThaiGhiNhan trangThai = TrangThaiGhiNhan.DaDuyet, bool deleted = false)
        {
            db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = thang,
                Nam = nam,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 100 + dien,
                DonGiaDien = 3500,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 50 + dien / 10,
                DonGiaNuoc = 20000,
                TrangThaiGhiNhan = trangThai,
                IsDeleted = deleted
            });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task ChiLayKyTronThangCuaHopDongHienTai()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H1");
            var room = await seeder.RoomAsync(branch, "H1");
            var oldTenant = await seeder.TenantAsync("Cu");
            var newTenant = await seeder.TenantAsync("Moi");
            await seeder.ContractAsync(room, oldTenant, Utc(2026, 1, 1), Utc(2026, 5, 15), TrangThaiHopDong.DaKetThuc);
            // Bắt đầu 00:00 ngày 01/07 giờ VN.
            var current = await seeder.ContractAsync(room, newTenant, Utc(2026, 6, 30, 17));
            await PeriodAsync(db, room, 4, 2026, 40);
            await PeriodAsync(db, room, 5, 2026, 50);
            await PeriodAsync(db, room, 7, 2026, 70);
            await PeriodAsync(db, room, 8, 2026, 80);
            await PeriodAsync(db, room, 9, 2026, 90);
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Equal(current.HopDongId, result.HopDongId);
            Assert.Equal(new[] { 90m, 80m, 70m }, result.CacKyTronThang.Select(x => x.Dien).ToArray());
            Assert.Equal(new[] { 9m, 8m, 7m }, result.CacKyTronThang.Select(x => x.Nuoc).ToArray());
            Assert.Equal(31, result.SoNgayCuaThang);
            Assert.Equal(31, result.SoNgayPhu);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task BienGioVietNam()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H2");
            var tenant = await seeder.TenantAsync("B");
            var roomOk = await seeder.RoomAsync(branch, "H2a");
            var roomLate = await seeder.RoomAsync(branch, "H2b");
            // 00:00 01/06 giờ VN: kỳ 6/2026 nằm trọn trong hợp đồng.
            await seeder.ContractAsync(roomOk, tenant, Utc(2026, 5, 31, 17));
            // Trễ một phút: kỳ 6/2026 không còn trọn.
            await seeder.ContractAsync(roomLate, tenant, Utc(2026, 5, 31, 17, 1));
            await PeriodAsync(db, roomOk, 6, 2026, 60);
            await PeriodAsync(db, roomLate, 6, 2026, 60);
            var store = new MeterConsumptionHistoryStore(db);

            var ok = await store.GetAsync(roomOk.PhongTroId, 7, 2026, 6);
            var late = await store.GetAsync(roomLate.PhongTroId, 7, 2026, 6);

            Assert.Single(ok.CacKyTronThang);
            Assert.Empty(late.CacKyTronThang);
            Assert.NotNull(late.HopDongId);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task ThangVaoGiuaChung_KhongTinh()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H3");
            var room = await seeder.RoomAsync(branch, "H3");
            var tenant = await seeder.TenantAsync("G");
            // Vào ngày 20/08 giờ VN.
            await seeder.ContractAsync(room, tenant, Utc(2026, 8, 19, 17));
            await PeriodAsync(db, room, 8, 2026, 80);
            await PeriodAsync(db, room, 9, 2026, 90);
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Equal(new[] { 90m }, result.CacKyTronThang.Select(x => x.Dien).ToArray());

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task SoNgayPhu_HopDongVaoGiuaThang()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H4");
            var room = await seeder.RoomAsync(branch, "H4");
            var tenant = await seeder.TenantAsync("N1");
            // 00:00 ngày 20/10 giờ VN → ngày 20..31 = 12 ngày.
            await seeder.ContractAsync(room, tenant, Utc(2026, 10, 19, 17));
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Equal(12, result.SoNgayPhu);
            Assert.Equal(31, result.SoNgayCuaThang);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task SoNgayPhu_HopDongKetThucGiuaThang()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H5");
            var room = await seeder.RoomAsync(branch, "H5");
            var tenant = await seeder.TenantAsync("N2");
            // Kết thúc 17:00 ngày 15/10 giờ VN, bắt đầu từ trước → ngày 1..15 = 15 ngày.
            await seeder.ContractAsync(room, tenant, Utc(2026, 1, 1), Utc(2026, 10, 15, 10));
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Equal(15, result.SoNgayPhu);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task SoNgayPhu_HopDongBatDauNgayCuoiThang_ToiThieuMotNgay()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H6");
            var room = await seeder.RoomAsync(branch, "H6");
            var tenant = await seeder.TenantAsync("N3");
            // 00:00 ngày 31/10 giờ VN → đúng 1 ngày.
            await seeder.ContractAsync(room, tenant, Utc(2026, 10, 30, 17));
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Equal(1, result.SoNgayPhu);
            Assert.True(result.SoNgayCuaThang > 0);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HaiHopDongTrongThang_KhongCoLichSu()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H7");
            var room = await seeder.RoomAsync(branch, "H7");
            var t1 = await seeder.TenantAsync("K1");
            var t2 = await seeder.TenantAsync("K2");
            await seeder.ContractAsync(room, t1, Utc(2026, 1, 1), Utc(2026, 10, 10), TrangThaiHopDong.DaKetThuc);
            await seeder.ContractAsync(room, t2, Utc(2026, 10, 11));
            await PeriodAsync(db, room, 9, 2026, 90);
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Empty(result.CacKyTronThang);
            Assert.Null(result.HopDongId);
            Assert.Equal(31, result.SoNgayCuaThang);
            Assert.Equal(31, result.SoNgayPhu);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GioiHanSoKy()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H8");
            var room = await seeder.RoomAsync(branch, "H8");
            var tenant = await seeder.TenantAsync("L");
            await seeder.ContractAsync(room, tenant, Utc(2025, 12, 31, 17));
            for (var m = 1; m <= 8; m++)
            {
                await PeriodAsync(db, room, m, 2026, m * 10);
            }
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Equal(6, result.CacKyTronThang.Count);
            Assert.Equal(80m, result.CacKyTronThang[0].Dien);
            Assert.Equal(30m, result.CacKyTronThang[5].Dien);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task ChiLayKyDaDuyetChuaXoaTruocKyDangXet()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H9");
            var room = await seeder.RoomAsync(branch, "H9");
            var tenant = await seeder.TenantAsync("D");
            // 00:00 ngày 01/01/2026 giờ VN, không ngày kết thúc: mọi kỳ 2026 đều trọn tháng trong hợp đồng.
            await seeder.ContractAsync(room, tenant, Utc(2025, 12, 31, 17));
            await PeriodAsync(db, room, 5, 2026, 50, deleted: true);                       // đã xóa mềm
            await PeriodAsync(db, room, 6, 2026, 60, TrangThaiGhiNhan.Nhap);               // chưa duyệt
            await PeriodAsync(db, room, 7, 2026, 70, TrangThaiGhiNhan.ChoDuyet);           // chờ duyệt
            await PeriodAsync(db, room, 8, 2026, 80);                                      // hợp lệ
            await PeriodAsync(db, room, 10, 2026, 100);                                    // chính kỳ đang xét
            await PeriodAsync(db, room, 11, 2026, 110);                                    // kỳ sau
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 10, 2026, 6);

            Assert.Equal(new[] { 80m }, result.CacKyTronThang.Select(x => x.Dien).ToArray());
            Assert.Equal(new[] { 8m }, result.CacKyTronThang.Select(x => x.Nuoc).ToArray());

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task LichSuQuaNam()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("HA");
            var room = await seeder.RoomAsync(branch, "HA");
            var tenant = await seeder.TenantAsync("Y");
            // 00:00 ngày 01/11/2025 giờ VN: kỳ 11/2025 và 12/2025 trọn tháng trong hợp đồng.
            var contract = await seeder.ContractAsync(room, tenant, Utc(2025, 10, 31, 17));
            await PeriodAsync(db, room, 11, 2025, 110);
            await PeriodAsync(db, room, 12, 2025, 120);
            await PeriodAsync(db, room, 2, 2026, 20);                                       // sau kỳ đang xét
            var store = new MeterConsumptionHistoryStore(db);

            var result = await store.GetAsync(room.PhongTroId, 1, 2026, 6);

            Assert.Equal(contract.HopDongId, result.HopDongId);
            Assert.Equal(new[] { 120m, 110m }, result.CacKyTronThang.Select(x => x.Dien).ToArray());
            Assert.Equal(31, result.SoNgayCuaThang);
            Assert.Equal(31, result.SoNgayPhu);

            await tx.RollbackAsync();
        }
    }
}
