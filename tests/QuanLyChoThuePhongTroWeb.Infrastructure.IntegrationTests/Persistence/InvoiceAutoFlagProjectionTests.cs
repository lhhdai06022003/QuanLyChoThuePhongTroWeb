using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Cờ hóa đơn tự động (theo thẻ lịch sử [TU_DONG]), hạn phản hồi (chỉ hóa đơn đã gửi) và chỉ số điện nước trong các DTO hóa đơn.
    [Collection("PostgreSqlCollection")]
    public class InvoiceAutoFlagProjectionTests
    {
        private static readonly DateTime Han = new(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);

        private readonly PostgreSqlFixture _fixture;

        public InvoiceAutoFlagProjectionTests(PostgreSqlFixture fixture)
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

        private static void AddHistory(ApplicationDbContext db, HoaDon invoice, string? lyDo)
            => db.LichSuTrangThaiHoaDons.Add(new LichSuTrangThaiHoaDon
            {
                HoaDonId = invoice.HoaDonId,
                TrangThaiPhatHanhMoi = invoice.TrangThaiPhatHanh,
                TrangThaiThanhToanMoi = invoice.TrangThaiHoaDon,
                NgayThucHien = Han.AddDays(-1),
                LyDo = lyDo
            });

        [Fact]
        public async Task LaHoaDonTuDong_TheoNhanLichSu()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("P");
            var room = await seeder.RoomAsync(branch, "P");
            var tenant = await seeder.TenantAsync("P");
            var contract = await seeder.ContractAsync(room, tenant);

            // Hóa đơn lập tay có HanPhanHoi (đặt khi công bố) nhưng không có thẻ [TU_DONG].
            var manualSent = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 1, year: 2026);
            manualSent.HanPhanHoi = Han;
            var autoSent = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 2, year: 2026);
            autoSent.HanPhanHoi = Han;
            var autoDraft = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 3, year: 2026);
            var adjustedOnly = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 4, year: 2026);
            await db.SaveChangesAsync();
            AddHistory(db, manualSent, "Tạo hóa đơn nháp");
            AddHistory(db, autoSent, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.TuDong, "Tạo hóa đơn nháp tự động"));
            AddHistory(db, autoDraft, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.TuDong, "Tạo hóa đơn nháp tự động"));
            AddHistory(db, adjustedOnly, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.DieuChinh, "Sửa chỉ số"));
            await db.SaveChangesAsync();
            var store = new InvoiceViewStore(db);
            var req = new DataTableRequest { Start = 0, Length = 50 };

            var all = await store.GetEmployeeListAsync(req, new InvoiceListFilter { ChiNhanhId = branch.ChiNhanhId }, null);
            var byId = all.data.ToDictionary(x => x.HoaDonId);

            Assert.False(byId[manualSent.HoaDonId].LaHoaDonTuDong);
            Assert.True(byId[autoSent.HoaDonId].LaHoaDonTuDong);
            Assert.True(byId[autoDraft.HoaDonId].LaHoaDonTuDong);
            Assert.False(byId[adjustedOnly.HoaDonId].LaHoaDonTuDong);

            // Chi tiết (có và không nạp lịch sử) dùng cùng quy tắc.
            foreach (var includeHistory in new[] { true, false })
            {
                Assert.False((await store.GetDetailAsync(manualSent.HoaDonId, includeHistory))!.LaHoaDonTuDong);
                Assert.True((await store.GetDetailAsync(autoSent.HoaDonId, includeHistory))!.LaHoaDonTuDong);
            }

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HanPhanHoiUtc_ChiVoiHoaDonDaGui_HoaDonCuKhacTrangThaiKhongCoHan()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H");
            var room = await seeder.RoomAsync(branch, "H");
            var tenant = await seeder.TenantAsync("H");
            var contract = await seeder.ContractAsync(room, tenant);

            var sent = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 1, year: 2026);
            sent.NgayGui = Han.AddDays(-1);
            sent.HanPhanHoi = Han;
            // Hóa đơn tạm tính cũ: Admin chốt hoặc trả lại vẫn có thể còn HanPhanHoi.
            var legacyChoDuyet = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 2, year: 2026);
            legacyChoDuyet.HanPhanHoi = Han;
            var legacyChot = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaChot, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 3, year: 2026);
            legacyChot.HanPhanHoi = Han;
            var legacyNhap = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 4, year: 2026);
            legacyNhap.HanPhanHoi = Han;
            var cancelled = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaHuy, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, deleted: true, month: 5, year: 2026);
            cancelled.HanPhanHoi = Han;
            await db.SaveChangesAsync();
            var store = new InvoiceViewStore(db);
            var req = new DataTableRequest { Start = 0, Length = 50 };

            var all = await store.GetEmployeeListAsync(req, new InvoiceListFilter { ChiNhanhId = branch.ChiNhanhId }, null);
            var byId = all.data.ToDictionary(x => x.HoaDonId);

            Assert.Equal(Han, byId[sent.HoaDonId].HanPhanHoiUtc);
            Assert.Null(byId[legacyChoDuyet.HoaDonId].HanPhanHoiUtc);
            Assert.Null(byId[legacyChot.HoaDonId].HanPhanHoiUtc);
            Assert.Null(byId[legacyNhap.HoaDonId].HanPhanHoiUtc);
            // HanPhanHoi không còn làm hóa đơn bị coi là tự động.
            Assert.All(all.data, x => Assert.False(x.LaHoaDonTuDong));

            var cancelledList = await store.GetEmployeeListAsync(req,
                new InvoiceListFilter { ChiNhanhId = branch.ChiNhanhId, TrangThaiPhatHanh = (int)TrangThaiPhatHanhHoaDon.DaHuy }, null);
            Assert.Null(Assert.Single(cancelledList.data, x => x.HoaDonId == cancelled.HoaDonId).HanPhanHoiUtc);

            Assert.Equal(Han, (await store.GetDetailAsync(sent.HoaDonId, includeHistory: false))!.HanPhanHoiUtc);
            Assert.Null((await store.GetDetailAsync(legacyChot.HoaDonId, includeHistory: false))!.HanPhanHoiUtc);
            Assert.Null((await store.GetDetailAsync(legacyChoDuyet.HoaDonId, includeHistory: true))!.HanPhanHoiUtc);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HanPhanHoiUtc_HoaDonCuDoJobTuChotGui_HanTruocNgayGui_KhongHienThi()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("G");
            var room = await seeder.RoomAsync(branch, "G");
            var tenant = await seeder.TenantAsync("G");
            var contract = await seeder.ContractAsync(room, tenant);

            // Job tự chốt cũ gửi hóa đơn sau khi hết hạn xem xét: HanPhanHoi sớm hơn NgayGui.
            var legacy = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 1, year: 2026);
            legacy.HanPhanHoi = Han;
            legacy.NgayGui = Han.AddHours(1);
            // Hóa đơn đã gửi không ghi NgayGui: không đủ căn cứ, không hiển thị hạn.
            var noNgayGui = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 2, year: 2026);
            noNgayGui.HanPhanHoi = Han;
            var current = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 3, year: 2026);
            current.NgayGui = Han.AddHours(-24);
            current.HanPhanHoi = Han;
            await db.SaveChangesAsync();
            var viewStore = new InvoiceViewStore(db);
            var hoaDonStore = new HoaDonStore(db);

            var list = await viewStore.GetEmployeeListAsync(new DataTableRequest { Start = 0, Length = 50 },
                new InvoiceListFilter { ChiNhanhId = branch.ChiNhanhId }, null);
            var byId = list.data.ToDictionary(x => x.HoaDonId);
            Assert.Null(byId[legacy.HoaDonId].HanPhanHoiUtc);
            Assert.Equal(string.Empty, byId[legacy.HoaDonId].HanPhanHoiHienThi);
            Assert.Null(byId[noNgayGui.HoaDonId].HanPhanHoiUtc);
            Assert.Equal(Han, byId[current.HoaDonId].HanPhanHoiUtc);

            var legacyDetail = await viewStore.GetDetailAsync(legacy.HoaDonId, includeHistory: true);
            Assert.Null(legacyDetail!.HanPhanHoiUtc);
            Assert.Equal(string.Empty, legacyDetail.HanPhanHoiHienThi);

            var tenantList = (await hoaDonStore.GetInvoicesByContractIdsAsync(new[] { contract.HopDongId })).ToDictionary(x => x.HoaDonId);
            Assert.Null(tenantList[legacy.HoaDonId].HanPhanHoiUtc);
            Assert.Equal(Han, tenantList[current.HoaDonId].HanPhanHoiUtc);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task LyDoHuy_BoNhanNoiBo()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("C");
            var room = await seeder.RoomAsync(branch, "C");
            var tenant = await seeder.TenantAsync("C");
            var contract = await seeder.ContractAsync(room, tenant);

            var cancelled = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaHuy, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, deleted: true, month: 1, year: 2026);
            cancelled.NgayGui = Han.AddDays(-2);
            AddHistory(db, cancelled, AutoInvoiceHistoryTags.TaoLyDo(AutoInvoiceHistoryTags.DieuChinh, "Điều chỉnh hóa đơn"));
            await db.SaveChangesAsync();
            var store = new InvoiceViewStore(db);

            foreach (var includeHistory in new[] { true, false })
            {
                var detail = await store.GetDetailAsync(cancelled.HoaDonId, includeHistory);
                Assert.Equal("Điều chỉnh hóa đơn", detail!.LyDoHuy);
            }

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task Detail_ReturnsMeterReadings_OfLinkedPeriod()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("D");
            var room = await seeder.RoomAsync(branch, "D");
            var tenant = await seeder.TenantAsync("D");
            var contract = await seeder.ContractAsync(room, tenant);
            var ky = await PeriodAsync(db, room, 1);

            var sent = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, 100_000m, null, month: 1, year: 2026);
            sent.NgayGui = Han.AddDays(-1);
            sent.HanPhanHoi = Han;
            sent.DichVuDienNuocCuaPhongId = ky.DichVuDienNuocCuaPhongId;
            await db.SaveChangesAsync();
            var store = new InvoiceViewStore(db);

            var detail = await store.GetDetailAsync(sent.HoaDonId, includeHistory: false);

            Assert.NotNull(detail);
            Assert.Equal(Han, detail!.HanPhanHoiUtc);
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
