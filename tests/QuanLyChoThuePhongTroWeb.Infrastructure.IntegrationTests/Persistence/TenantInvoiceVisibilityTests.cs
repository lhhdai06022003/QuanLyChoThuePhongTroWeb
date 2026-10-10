using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class TenantInvoiceVisibilityTests
    {
        private static readonly DateTime Han = new(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);

        private readonly PostgreSqlFixture _fixture;

        public TenantInvoiceVisibilityTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ProvisionalInvoice_IsVisibleToTenant_ButManualChoDuyetAndDeletedAreNot()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("T");
            var room = await seeder.RoomAsync(branch, "T");
            var tenant = await seeder.TenantAsync("T");
            var otherTenant = await seeder.TenantAsync("U");
            var contract = await seeder.ContractAsync(room, tenant);

            var provisional = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 111_000m, null, month: 1, year: 2026);
            provisional.HanPhanHoi = Han;
            var manualReview = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 222_000m, null, month: 2, year: 2026);
            var provisionalDeleted = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 333_000m, null, deleted: true, month: 3, year: 2026);
            provisionalDeleted.HanPhanHoi = Han;
            var draft = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 444_000m, null, month: 4, year: 2026);
            draft.HanPhanHoi = Han;
            var sent = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 555_000m, null, month: 5, year: 2026);
            var cancelledSent = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaHuy, TrangThaiHoaDon.ChuaThanhToan, 666_000m, null, deleted: true, month: 6, year: 2026);
            cancelledSent.NgayGui = Han.AddDays(-5);
            var finalized = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaChot, TrangThaiHoaDon.ChuaThanhToan, 777_000m, null, month: 7, year: 2026);
            await db.SaveChangesAsync();
            var store = new HoaDonStore(db);

            var list = await store.GetInvoicesByContractIdsAsync(new[] { contract.HopDongId });
            var ids = list.Select(x => x.HoaDonId).ToHashSet();

            Assert.Contains(provisional.HoaDonId, ids);
            Assert.Contains(sent.HoaDonId, ids);
            Assert.Contains(cancelledSent.HoaDonId, ids);
            Assert.DoesNotContain(manualReview.HoaDonId, ids);
            Assert.DoesNotContain(provisionalDeleted.HoaDonId, ids);
            Assert.DoesNotContain(draft.HoaDonId, ids);
            Assert.DoesNotContain(finalized.HoaDonId, ids);

            Assert.True(await store.CheckHoaDonOwnershipAsync(provisional.HoaDonId, tenant.NguoiThueId));
            Assert.True(await store.CheckHoaDonOwnershipAsync(sent.HoaDonId, tenant.NguoiThueId));
            Assert.True(await store.CheckHoaDonOwnershipAsync(cancelledSent.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(manualReview.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(provisionalDeleted.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(draft.HoaDonId, tenant.NguoiThueId));
            Assert.False(await store.CheckHoaDonOwnershipAsync(finalized.HoaDonId, tenant.NguoiThueId));

            // Khách khác không thấy hóa đơn tạm tính của hợp đồng này.
            Assert.False(await store.CheckHoaDonOwnershipAsync(provisional.HoaDonId, otherTenant.NguoiThueId));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task ProvisionalInvoice_ListShowsAmountButNotAsPaidOrFinalStatus()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("L");
            var room = await seeder.RoomAsync(branch, "L");
            var tenant = await seeder.TenantAsync("L");
            var contract = await seeder.ContractAsync(room, tenant);
            var provisional = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 987_000m, null, month: 1, year: 2026);
            provisional.HanPhanHoi = Han;
            await db.SaveChangesAsync();
            var store = new HoaDonStore(db);

            var list = await store.GetInvoicesByContractIdsAsync(new[] { contract.HopDongId });

            var item = Assert.Single(list);
            Assert.Equal(987_000m, item.TongTien);
            Assert.Equal((int)TrangThaiPhatHanhHoaDon.ChoDuyet, item.TrangThaiPhatHanhValue);

            await tx.RollbackAsync();
        }
    }
}
