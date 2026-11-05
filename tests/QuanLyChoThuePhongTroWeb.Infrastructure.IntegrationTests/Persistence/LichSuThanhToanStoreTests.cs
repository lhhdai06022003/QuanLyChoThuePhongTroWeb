using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Lọc ngày của màn Lịch sử thanh toán theo ngày lịch Việt Nam (dữ liệu lưu UTC) và ô Cấn trừ cọc.
    [Collection("PostgreSqlCollection")]
    public class LichSuThanhToanStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public LichSuThanhToanStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static async Task AddLedgerAsync(ApplicationDbContext db, HoaDon hoaDon, decimal soTien, PhuongThucThanhToan phuongThuc, DateTime ngayUtc, string tag)
        {
            db.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = hoaDon.HoaDonId,
                MaGiaoDich = $"LS-{tag}-{Guid.NewGuid():N}",
                SoTienThanhToan = soTien,
                PhuongThucThanhToan = phuongThuc,
                NgayThanhToan = ngayUtc
            });
            await db.SaveChangesAsync();
        }

        private static DataTableRequest Request() => new() { Draw = 1, Start = 0, Length = 50 };

        [Fact]
        public async Task LocNgay_TheoNgayLichVn_CaThongKeVaDanhSach()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("L");
            var tenant = await seed.TenantAsync("L");
            var room = await seed.RoomAsync(branch, "L");
            var contract = await seed.ContractAsync(room, tenant);
            var invoice = await seed.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m, null, month: 3, year: 2003);

            // 16:59:59Z là 23:59:59 ngày 09/03 giờ VN; 17:00:00Z là 00:00:00 ngày 10/03.
            await AddLedgerAsync(db, invoice, 100m, PhuongThucThanhToan.TienMat, new DateTime(2003, 3, 9, 16, 59, 59, DateTimeKind.Utc), "A");
            await AddLedgerAsync(db, invoice, 200m, PhuongThucThanhToan.ChuyenKhoan, new DateTime(2003, 3, 9, 17, 0, 0, DateTimeKind.Utc), "B");

            var store = new LichSuThanhToanStore(db);
            var ngay9 = new DateTime(2003, 3, 9);
            var ngay10 = new DateTime(2003, 3, 10);

            var stats9 = await store.GetThongKeThanhToanAsync(branch.ChiNhanhId, ngay9, ngay9);
            Assert.Equal(1, stats9.TongSoGiaoDich);
            Assert.Equal(100m, stats9.TongDoanhThu);

            var stats10 = await store.GetThongKeThanhToanAsync(branch.ChiNhanhId, ngay10, ngay10);
            Assert.Equal(1, stats10.TongSoGiaoDich);
            Assert.Equal(200m, stats10.TongDoanhThu);

            var statsCa = await store.GetThongKeThanhToanAsync(branch.ChiNhanhId, ngay9, ngay10);
            Assert.Equal(2, statsCa.TongSoGiaoDich);

            var list9 = await store.GetDanhSachThanhToanAsync(Request(), branch.ChiNhanhId, -1, ngay9, ngay9);
            Assert.Equal(new[] { 100m }, list9.data.Select(r => r.SoTienThanhToan).ToArray());
            var list10 = await store.GetDanhSachThanhToanAsync(Request(), branch.ChiNhanhId, -1, ngay10, ngay10);
            Assert.Equal(new[] { 200m }, list10.data.Select(r => r.SoTienThanhToan).ToArray());
            var listCa = await store.GetDanhSachThanhToanAsync(Request(), branch.ChiNhanhId, -1, ngay9, ngay10);
            Assert.Equal(2, listCa.recordsTotal);

            // Chỉ có ngày bắt đầu hoặc chỉ có ngày kết thúc.
            Assert.Equal(1, (await store.GetThongKeThanhToanAsync(branch.ChiNhanhId, ngay10, null)).TongSoGiaoDich);
            Assert.Equal(1, (await store.GetThongKeThanhToanAsync(branch.ChiNhanhId, null, ngay9)).TongSoGiaoDich);
            await tx.RollbackAsync();
        }

        [Fact]
        public async Task ThongKe_CoDongCanTru_TongBangTienMatCongChuyenKhoanCongCanTru()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seed = new DebtTestSeeder(db);
            var branch = await seed.BranchAsync("L");
            var tenant = await seed.TenantAsync("L");
            var contract = await seed.ContractAsync(await seed.RoomAsync(branch, "L"), tenant);
            var invoice = await seed.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan, 1_000_000m, null, month: 3, year: 2003);
            var ngay = new DateTime(2003, 3, 9, 3, 0, 0, DateTimeKind.Utc);

            await AddLedgerAsync(db, invoice, 100m, PhuongThucThanhToan.TienMat, ngay, "T");
            await AddLedgerAsync(db, invoice, 200m, PhuongThucThanhToan.ChuyenKhoan, ngay, "C");
            await AddLedgerAsync(db, invoice, 50m, PhuongThucThanhToan.CanTruTienCoc, ngay, "X");

            var stats = await new LichSuThanhToanStore(db).GetThongKeThanhToanAsync(branch.ChiNhanhId, null, null);

            Assert.Equal(50m, stats.DoanhThuCanTruCoc);
            Assert.Equal(350m, stats.TongDoanhThu);
            Assert.Equal(stats.DoanhThuTienMat + stats.DoanhThuChuyenKhoan + stats.DoanhThuCanTruCoc, stats.TongDoanhThu);

            var rong = await new LichSuThanhToanStore(db).GetThongKeThanhToanAsync(branch.ChiNhanhId, new DateTime(2010, 1, 1), new DateTime(2010, 1, 1));
            Assert.Equal(0m, rong.DoanhThuCanTruCoc);
            await tx.RollbackAsync();
        }
    }
}
