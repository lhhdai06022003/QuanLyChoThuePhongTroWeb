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
    // T25, T26: màn Quản lý phòng trọ chỉ coi hóa đơn đã công bố (DaGui) là công nợ và là hóa đơn thu tiền được.
    [Collection("PostgreSqlCollection")]
    public class PhongTroDebtScopeTests
    {
        private readonly PostgreSqlFixture _fixture;

        public PhongTroDebtScopeTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static async Task<(ChiNhanh Branch, PhongTro Room, HopDong Contract)> SeedRoomAsync(ApplicationDbContext context, string suffix)
        {
            var branch = new ChiNhanh { TenChiNhanh = $"CN PT {suffix}", MaChiNhanh = TestCodes.MaChiNhanh("P"), DiaChi = "A", SoDienThoai = "0900000003", MoTa = "M" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"PT_{suffix}", GiaThue = 3_000_000m, DienTich = 20, MoTa = "M", TrangThai = TrangThaiPhong.DaThue };
            var tenant = new NguoiThue { HoVaTen = $"Khach PT {suffix}", SoDienThoai = "0912345670", Email = $"pt_{suffix}@test.com", CCCD = $"081{suffix}00" };
            context.AddRange(room, tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"PTH_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TienThuePhong = 3_000_000m,
                TienCocPhong = 3_000_000m,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-3),
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();
            return (branch, room, contract);
        }

        private static HoaDon Invoice(HopDong contract, string code, int thang, decimal tongTien, TrangThaiPhatHanhHoaDon phatHanh, DateTime ngayTao, DateTime? hanPhanHoi = null)
            => new HoaDon
            {
                MaHoaDon = code,
                HopDongId = contract.HopDongId,
                Thang = thang,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                TrangThaiPhatHanh = phatHanh,
                HanPhanHoi = hanPhanHoi,
                NgayTao = ngayTao
            };

        [Fact]
        public async Task QuickPaymentAndRoomDebt_IgnoreDraftPendingAndProvisional_CountOnlySent()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var (branch, room, contract) = await SeedRoomAsync(context, suffix);
            var now = DateTime.UtcNow;

            var sent = Invoice(contract, $"PT_SENT_{suffix}", 7, 1_000_000m, TrangThaiPhatHanhHoaDon.DaGui, now.AddDays(-30));
            context.HoaDons.AddRange(
                sent,
                Invoice(contract, $"PT_DRAFT_{suffix}", 8, 500_000m, TrangThaiPhatHanhHoaDon.Nhap, now.AddDays(-2)),
                // Hóa đơn tạm tính mới nhất: trước đây modal "Thu tiền" lấy nhầm bản này.
                Invoice(contract, $"PT_PROV_{suffix}", 9, 3_200_000m, TrangThaiPhatHanhHoaDon.ChoDuyet, now.AddMinutes(-5), now.AddHours(24)));
            await context.SaveChangesAsync();

            var store = new PhongTroStore(context);

            var unpaid = await store.GetUnpaidInvoiceAsync(room.PhongTroId);
            Assert.NotNull(unpaid);
            Assert.Equal(sent.HoaDonId, unpaid!.hoaDonId);

            var card = (await store.GetSoDoPhongAsync(branch.ChiNhanhId)).Single(c => c.PhongTroId == room.PhongTroId);
            Assert.True(card.CoNoTien);
            Assert.Equal(1_000_000m, card.SoTienNo);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task RoomWithOnlyProvisionalInvoice_HasNoDebtAndNoInvoiceToCollect()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var (branch, room, contract) = await SeedRoomAsync(context, suffix);
            var now = DateTime.UtcNow;

            context.HoaDons.AddRange(
                Invoice(contract, $"PT_PROV_{suffix}", 9, 3_200_000m, TrangThaiPhatHanhHoaDon.ChoDuyet, now.AddMinutes(-5), now.AddHours(24)),
                Invoice(contract, $"PT_CHOT_{suffix}", 8, 2_000_000m, TrangThaiPhatHanhHoaDon.DaChot, now.AddDays(-1)));
            await context.SaveChangesAsync();

            var store = new PhongTroStore(context);

            Assert.Null(await store.GetUnpaidInvoiceAsync(room.PhongTroId));

            var card = (await store.GetSoDoPhongAsync(branch.ChiNhanhId)).Single(c => c.PhongTroId == room.PhongTroId);
            Assert.False(card.CoNoTien);
            Assert.Equal(0m, card.SoTienNo);

            await tx.RollbackAsync();
        }
    }
}
