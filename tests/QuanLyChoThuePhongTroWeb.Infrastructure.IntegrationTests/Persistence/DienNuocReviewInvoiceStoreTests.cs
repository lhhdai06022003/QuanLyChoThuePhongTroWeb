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
    public class DienNuocReviewInvoiceStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public DienNuocReviewInvoiceStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static async Task<DichVuDienNuocCuaPhong> PeriodAsync(ApplicationDbContext db, PhongTro room, int thang, int nam)
        {
            var p = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = thang,
                Nam = nam,
                ChiSoDienCu = 100,
                ChiSoDienMoi = 150,
                DonGiaDien = 3500,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 55,
                DonGiaNuoc = 20000,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            };
            db.DichVuDienNuocCuaPhongs.Add(p);
            await db.SaveChangesAsync();
            return p;
        }

        [Fact]
        public async Task GetRoomIdsWithReviewInvoice_ChiTinhChoDuyetVaDaChotDungKy()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("R1");
            var tenant = await seeder.TenantAsync("R1");
            var roomReview = await seeder.RoomAsync(branch, "Ra");
            var roomApproved = await seeder.RoomAsync(branch, "Rb");
            var roomSent = await seeder.RoomAsync(branch, "Rc");
            var roomDeleted = await seeder.RoomAsync(branch, "Rd");
            var roomOtherMonth = await seeder.RoomAsync(branch, "Re");
            var roomNone = await seeder.RoomAsync(branch, "Rf");

            async Task AddAsync(PhongTro room, TrangThaiPhatHanhHoaDon status, bool deleted, int month)
            {
                var contract = await seeder.ContractAsync(room, tenant);
                var invoice = await seeder.InvoiceAsync(contract, status, TrangThaiHoaDon.ChuaThanhToan, 1000m, null, deleted, month: month, year: 2026);
                var period = await PeriodAsync(db, room, month, 2026);
                invoice.DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId;
                await db.SaveChangesAsync();
            }

            await AddAsync(roomReview, TrangThaiPhatHanhHoaDon.ChoDuyet, false, 10);
            await AddAsync(roomApproved, TrangThaiPhatHanhHoaDon.DaChot, false, 10);
            await AddAsync(roomSent, TrangThaiPhatHanhHoaDon.DaGui, false, 10);
            await AddAsync(roomDeleted, TrangThaiPhatHanhHoaDon.ChoDuyet, true, 10);
            await AddAsync(roomOtherMonth, TrangThaiPhatHanhHoaDon.ChoDuyet, false, 9);
            await PeriodAsync(db, roomNone, 10, 2026);
            var store = new DienNuocStore(db);
            var ids = new[] { roomReview, roomApproved, roomSent, roomDeleted, roomOtherMonth, roomNone }.Select(r => r.PhongTroId).ToList();

            var result = await store.GetRoomIdsWithReviewInvoiceAsync(ids, 10, 2026);

            Assert.Equal(
                new[] { roomReview.PhongTroId, roomApproved.PhongTroId }.OrderBy(x => x),
                result.OrderBy(x => x));

            await tx.RollbackAsync();
        }
    }
}
