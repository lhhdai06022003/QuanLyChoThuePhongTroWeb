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
    // KPI Dashboard theo định nghĩa công nợ mới: Chờ thu, Phòng đang nợ, Quá hạn chỉ tính hóa đơn DaGui;
    // Đã thu giữ nguyên (mọi hóa đơn chưa xóa).
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
        public async Task MonthlyStats_ChoThuOnlySentUnpaid_DaThuCountsEveryNotDeletedInvoice()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);

            var stats = await new DashboardStore(db).GetInvoiceMonthlyStatsAsync(w.A.ChiNhanhId, 2003);

            var april = stats.Single(s => s.Thang == 4);
            // Chờ thu: chỉ hóa đơn DaGui chưa thu đủ (1.000.000 - 300.000); Nhap/ChoDuyet/DaChot không tính, ledger xóa mềm không trừ.
            Assert.Equal(700_000m, april.ChoThu);
            // Đã thu: ledger chưa xóa của mọi hóa đơn chưa xóa: 100.000 (nháp) + 300.000 + 200.000; hóa đơn xóa mềm bị loại.
            Assert.Equal(600_000m, april.DaThu);

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
        public async Task CountUnpaidRooms_IgnoresRoomsWithOnlyUnsentInvoices_AndCountsRoomOnce()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            // Phòng nợ: phòng có hai hóa đơn DaGui chưa thu (đếm một lần) và phòng năm 2004. Nháp/Chờ duyệt/Đã chốt/đã thu đủ/xóa mềm không tính.
            Assert.Equal(2, await store.CountUnpaidRoomsAsync(w.A.ChiNhanhId));
            Assert.Equal(1, await store.CountUnpaidRoomsAsync(w.B.ChiNhanhId));
            Assert.Equal(0, await store.CountUnpaidRoomsAsync(null, Array.Empty<int>()));
            Assert.Equal(0, await store.CountUnpaidRoomsAsync(w.A.ChiNhanhId, new[] { w.B.ChiNhanhId }));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task OverdueSummary_CountsSentUnpaidPastDue_AndExcludesDueEqualToNow()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            var atNow = await store.GetOverdueSummaryAsync(w.A.ChiNhanhId, Now);
            var justAfter = await store.GetOverdueSummaryAsync(w.A.ChiNhanhId, Now.AddTicks(10));

            // Chi nhánh A tại nowUtc: hóa đơn 4/2003 (còn nợ 700.000) và hóa đơn 4/2004 (111.000); hóa đơn hạn đúng nowUtc chưa quá hạn;
            // nháp quá hạn, xóa mềm, đã thu đủ không tính.
            Assert.Equal(2, atNow.SoHoaDon);
            Assert.Equal(700_000m + 111_000m, atNow.TongConNo);
            Assert.Equal(3, justAfter.SoHoaDon);
            Assert.Equal(700_000m + 111_000m + 400_000m, justAfter.TongConNo);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task OverdueSummary_FiltersByBranch_AndEmptyScopeIsZero()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new DashboardStore(db);

            var onlyB = await store.GetOverdueSummaryAsync(null, Now, new[] { w.B.ChiNhanhId });
            Assert.Equal(1, onlyB.SoHoaDon);
            Assert.Equal(2_000_000m, onlyB.TongConNo);

            var empty = await store.GetOverdueSummaryAsync(null, Now, Array.Empty<int>());
            Assert.Equal(0, empty.SoHoaDon);
            Assert.Equal(0m, empty.TongConNo);

            var outside = await store.GetOverdueSummaryAsync(w.A.ChiNhanhId, Now, new[] { w.B.ChiNhanhId });
            Assert.Equal(0, outside.SoHoaDon);

            await tx.RollbackAsync();
        }
    }
}
