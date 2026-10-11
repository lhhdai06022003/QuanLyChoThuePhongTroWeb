using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Cờ tạm tính, hạn tự chốt (giờ Việt Nam) và chỉ số điện nước trong các DTO hóa đơn.
    [Collection("PostgreSqlCollection")]
    public class ProvisionalInvoiceProjectionTests
    {
        // 2026-10-12 03:00 UTC = 10:00 giờ Việt Nam
        private static readonly DateTime Han = new(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);
        private const string HanVn = "12/10/2026 10:00";

        private readonly PostgreSqlFixture _fixture;

        public ProvisionalInvoiceProjectionTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static async Task<DichVuDienNuocCuaPhong> PeriodAsync(
            ApplicationDbContext db, PhongTro room, int month, bool deleted = false)
        {
            var ky = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = month,
                Nam = 2026,
                ChiSoDienCu = 100m,
                ChiSoDienMoi = 150.5m,
                DonGiaDien = 3000m,
                ChiSoNuocCu = 10m,
                ChiSoNuocMoi = 17m,
                DonGiaNuoc = 15000m,
                IsDeleted = deleted
            };
            db.DichVuDienNuocCuaPhongs.Add(ky);
            await db.SaveChangesAsync();
            return ky;
        }

        [Fact]
        public async Task EmployeeList_FlagsProvisionalManualSentAndCancelled()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("P");
            var room = await seeder.RoomAsync(branch, "P");
            var tenant = await seeder.TenantAsync("P");
            var contract = await seeder.ContractAsync(room, tenant);

            var provisional = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 1, year: 2026);
            provisional.HanPhanHoi = Han;
            var manual = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 2, year: 2026);
            var sent = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 3, year: 2026);
            sent.HanPhanHoi = Han;
            var cancelled = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaHuy, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, deleted: true, month: 4, year: 2026);
            cancelled.HanPhanHoi = Han;
            await db.SaveChangesAsync();
            var store = new InvoiceViewStore(db);
            var req = new DataTableRequest { Start = 0, Length = 50 };

            var all = await store.GetEmployeeListAsync(req, new InvoiceListFilter { ChiNhanhId = branch.ChiNhanhId }, null);
            var byId = all.data.ToDictionary(x => x.HoaDonId);

            Assert.True(byId[provisional.HoaDonId].LaTamTinh);
            Assert.True(byId[provisional.HoaDonId].LaHoaDonTuDong);
            Assert.Equal(HanVn, byId[provisional.HoaDonId].HanTuChot);

            Assert.False(byId[manual.HoaDonId].LaTamTinh);
            Assert.False(byId[manual.HoaDonId].LaHoaDonTuDong);
            Assert.Equal(string.Empty, byId[manual.HoaDonId].HanTuChot);

            Assert.True(byId[sent.HoaDonId].LaHoaDonTuDong);
            Assert.False(byId[sent.HoaDonId].LaTamTinh);
            Assert.Equal(string.Empty, byId[sent.HoaDonId].HanTuChot);

            var cancelledList = await store.GetEmployeeListAsync(req,
                new InvoiceListFilter { ChiNhanhId = branch.ChiNhanhId, TrangThaiPhatHanh = (int)TrangThaiPhatHanhHoaDon.DaHuy }, null);
            var cancelledRow = Assert.Single(cancelledList.data, x => x.HoaDonId == cancelled.HoaDonId);
            Assert.False(cancelledRow.LaTamTinh);
            Assert.Equal(string.Empty, cancelledRow.HanTuChot);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task Detail_ReturnsFlagsAndMeterReadings_OfLinkedPeriod()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("D");
            var room = await seeder.RoomAsync(branch, "D");
            var tenant = await seeder.TenantAsync("D");
            var contract = await seeder.ContractAsync(room, tenant);
            var ky = await PeriodAsync(db, room, 1);

            var provisional = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 1, year: 2026);
            provisional.HanPhanHoi = Han;
            provisional.DichVuDienNuocCuaPhongId = ky.DichVuDienNuocCuaPhongId;
            await db.SaveChangesAsync();
            var store = new InvoiceViewStore(db);

            var detail = await store.GetDetailAsync(provisional.HoaDonId, includeHistory: false);

            Assert.NotNull(detail);
            Assert.True(detail!.LaTamTinh);
            Assert.True(detail.LaHoaDonTuDong);
            Assert.Equal(HanVn, detail.HanTuChot);
            Assert.Equal(100m, detail.ChiSoDienCu);
            Assert.Equal(150.5m, detail.ChiSoDienMoi);
            Assert.Equal(10m, detail.ChiSoNuocCu);
            Assert.Equal(17m, detail.ChiSoNuocMoi);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task Detail_SoftDeletedPeriodOrNoPeriod_HasNullMeterReadings()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("N");
            var room = await seeder.RoomAsync(branch, "N");
            var tenant = await seeder.TenantAsync("N");
            var contract = await seeder.ContractAsync(room, tenant);
            var kyDeleted = await PeriodAsync(db, room, 1, deleted: true);

            var withDeletedPeriod = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 1, year: 2026);
            withDeletedPeriod.HanPhanHoi = Han;
            withDeletedPeriod.DichVuDienNuocCuaPhongId = kyDeleted.DichVuDienNuocCuaPhongId;
            var withoutPeriod = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 2, year: 2026);
            await db.SaveChangesAsync();
            var store = new InvoiceViewStore(db);

            foreach (var id in new[] { withDeletedPeriod.HoaDonId, withoutPeriod.HoaDonId })
            {
                var detail = await store.GetDetailAsync(id, includeHistory: false);
                Assert.NotNull(detail);
                Assert.Null(detail!.ChiSoDienCu);
                Assert.Null(detail.ChiSoDienMoi);
                Assert.Null(detail.ChiSoNuocCu);
                Assert.Null(detail.ChiSoNuocMoi);
            }

            await tx.RollbackAsync();
        }
    }
}
