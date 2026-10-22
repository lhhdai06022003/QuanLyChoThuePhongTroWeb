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
    // Bổ sung: nhân viên chi nhánh B không thấy dữ liệu chi nhánh A ở từng truy vấn, hóa đơn đã hủy bị loại khỏi công nợ,
    // phạm vi rỗng không thấy gì ở mọi truy vấn.
    [Collection("PostgreSqlCollection")]
    public class AiAssistantStoreScopeTests
    {
        private readonly PostgreSqlFixture _fixture;

        public AiAssistantStoreScopeTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class World
        {
            public string Suffix = "";
            public string Shared = "";
            public ChiNhanh A = null!;
            public ChiNhanh B = null!;
            public PhongTro RoomA = null!;
            public PhongTro RoomB = null!;
            public PhongTro VacantA = null!;
            public NguoiThue TenantA = null!;
            public NguoiThue TenantB = null!;
            public HopDong ContractA = null!;
            public HopDong ContractB = null!;
            public HoaDon CancelledSent = null!;
            public HoaDon UnpaidA = null!;
            public HoaDon DeletedContractInvoice = null!;
            public DateTime PayDate = new(2003, 4, 15, 3, 0, 0, DateTimeKind.Utc);
        }

        private static async Task<World> SeedAsync(ApplicationDbContext db)
        {
            var w = new World { Suffix = Guid.NewGuid().ToString("N")[..8] };
            var s = w.Suffix;
            w.Shared = $"T{s[..6]}";
            w.A = new ChiNhanh { TenChiNhanh = $"ScA {s}", MaChiNhanh = $"SA{s[..5]}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "A" };
            w.B = new ChiNhanh { TenChiNhanh = $"ScB {s}", MaChiNhanh = $"SB{s[..5]}", DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            db.ChiNhanhs.AddRange(w.A, w.B);
            await db.SaveChangesAsync();

            PhongTro Room(ChiNhanh b, string no, TrangThaiPhong st) => new()
            { ChiNhanhId = b.ChiNhanhId, SoPhong = no, GiaThue = 1_000_000m, DienTich = 10, MoTa = "x", TrangThai = st };
            w.RoomA = Room(w.A, w.Shared, TrangThaiPhong.DaThue);
            w.RoomB = Room(w.B, w.Shared, TrangThaiPhong.DaThue);
            w.VacantA = Room(w.A, $"VA{s[..5]}", TrangThaiPhong.Trong);
            var vacantB = Room(w.B, $"VB{s[..5]}", TrangThaiPhong.Trong);
            var deletedContractRoom = Room(w.A, $"DC{s[..5]}", TrangThaiPhong.DaThue);
            db.PhongTros.AddRange(w.RoomA, w.RoomB, w.VacantA, vacantB, deletedContractRoom);
            await db.SaveChangesAsync();

            w.TenantA = new NguoiThue { HoVaTen = $"Sc Khach A {s}", SoDienThoai = "0911111111", CCCD = $"SA{s}", Email = $"sa{s}@t.vn" };
            w.TenantB = new NguoiThue { HoVaTen = $"Sc Khach B {s}", SoDienThoai = "0922222222", CCCD = $"SB{s}", Email = $"sb{s}@t.vn" };
            db.NguoiThues.AddRange(w.TenantA, w.TenantB);
            await db.SaveChangesAsync();

            HopDong Contract(string code, PhongTro r, NguoiThue t, bool deleted = false) => new()
            {
                MaHopDong = $"{code}{s}", PhongTroId = r.PhongTroId, NguoiThueId = t.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1), ThoiDiemKetThuc = DateTime.UtcNow.AddDays(5),
                TienCocPhong = 1m, TienThuePhong = 1m, TrangThaiHopDong = TrangThaiHopDong.DangHoatDong, IsDeleted = deleted
            };
            w.ContractA = Contract("SCA", w.RoomA, w.TenantA);
            w.ContractB = Contract("SCB", w.RoomB, w.TenantB);
            var deletedContract = Contract("SCD", deletedContractRoom, w.TenantA, deleted: true);
            db.HopDongs.AddRange(w.ContractA, w.ContractB, deletedContract);
            await db.SaveChangesAsync();

            HoaDon Invoice(string code, HopDong c, int thang, int nam, TrangThaiPhatHanhHoaDon issue, TrangThaiHoaDon pay = TrangThaiHoaDon.ChuaThanhToan) => new()
            { MaHoaDon = $"{code}{s}", HopDongId = c.HopDongId, Thang = thang, Nam = nam, TongTien = 100_000m, TrangThaiPhatHanh = issue, TrangThaiHoaDon = pay };
            w.UnpaidA = Invoice("SU", w.ContractA, 4, 2003, TrangThaiPhatHanhHoaDon.DaGui);
            w.CancelledSent = Invoice("SX", w.ContractA, 5, 2003, TrangThaiPhatHanhHoaDon.DaHuy);
            w.DeletedContractInvoice = Invoice("SD", deletedContract, 4, 2003, TrangThaiPhatHanhHoaDon.DaGui);
            var unpaidB = Invoice("SB", w.ContractB, 4, 2003, TrangThaiPhatHanhHoaDon.DaGui);
            db.HoaDons.AddRange(w.UnpaidA, w.CancelledSent, w.DeletedContractInvoice, unpaidB);
            await db.SaveChangesAsync();

            db.LichSuThanhToans.AddRange(
                new LichSuThanhToan { HoaDonId = w.UnpaidA.HoaDonId, MaGiaoDich = $"SC{s}1", SoTienThanhToan = 10_000m, PhuongThucThanhToan = PhuongThucThanhToan.TienMat, NgayThanhToan = w.PayDate },
                new LichSuThanhToan { HoaDonId = unpaidB.HoaDonId, MaGiaoDich = $"SC{s}2", SoTienThanhToan = 20_000m, PhuongThucThanhToan = PhuongThucThanhToan.TienMat, NgayThanhToan = w.PayDate });

            db.DichVuDienNuocCuaPhongs.Add(
                new DichVuDienNuocCuaPhong { PhongTroId = w.RoomB.PhongTroId, Thang = 4, Nam = 2003, ChiSoDienCu = 2, ChiSoDienMoi = 99, DonGiaDien = 1, ChiSoNuocCu = 2, ChiSoNuocMoi = 50, DonGiaNuoc = 1, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet });
            await db.SaveChangesAsync();
            return w;
        }

        [Fact]
        public async Task Unpaid_ExcludesCancelledIssueStatus()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var codes = (await store.GetUnpaidInvoicesAsync(null, 2003, null, new[] { w.A.ChiNhanhId })).Select(r => r.MaHoaDon).ToList();

            Assert.Contains(w.UnpaidA.MaHoaDon, codes);
            Assert.DoesNotContain(w.CancelledSent.MaHoaDon, codes);     // Đã hủy bị loại
            Assert.Equal(10_000m, (await store.GetUnpaidInvoicesAsync(null, 2003, null, new[] { w.A.ChiNhanhId })).Single(r => r.MaHoaDon == w.UnpaidA.MaHoaDon).DaThu);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task StaffOfBranchB_SeesNothingOfBranchA_InEveryScopedQuery()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);
            var b = new[] { w.B.ChiNhanhId };
            var from = w.PayDate.AddDays(-1);
            var to = w.PayDate.AddDays(1);

            var rented = await store.GetRentedRoomsWithoutMeterReadingAsync(1, 2030, b);
            Assert.DoesNotContain(rented, r => r.ChiNhanhId == w.A.ChiNhanhId);
            Assert.Contains(rented, r => r.PhongTroId == w.RoomB.PhongTroId);

            var invoices = await store.GetUnpaidInvoicesAsync(4, 2003, null, b);
            Assert.DoesNotContain(invoices, r => r.MaHoaDon == w.UnpaidA.MaHoaDon);
            Assert.Single(invoices);

            var revenue = await store.GetRevenueAsync(from, to, b);
            Assert.Equal(20_000m, revenue.TongTien);
            Assert.Equal(1, revenue.SoGiaoDich);

            var vacant = await store.GetVacantRoomsAsync(null, b);
            Assert.DoesNotContain(vacant, r => r.PhongTroId == w.VacantA.PhongTroId);

            var expiring = await store.GetExpiringContractsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(30), b);
            Assert.DoesNotContain(expiring, r => r.TenChiNhanh == w.A.TenChiNhanh);
            Assert.Contains(expiring, r => r.TenChiNhanh == w.B.TenChiNhanh);

            Assert.Empty(await store.SearchTenantsAsync($"sc khach a {w.Suffix}", b));
            Assert.DoesNotContain(await store.SearchTenantsAsync(w.Shared, b), x => x.HoVaTen == w.TenantA.HoVaTen);

            var rooms = await store.FindRoomsByNumberAsync(w.Shared, null, b);
            Assert.Equal(w.RoomB.PhongTroId, Assert.Single(rooms).PhongTroId);

            var branchRevenue = await store.GetRevenueByBranchAsync(4, 2003, b);
            var row = Assert.Single(branchRevenue);
            Assert.Equal(w.B.ChiNhanhId, row.ChiNhanhId);
            Assert.Equal(20_000m, row.TongTien);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task EmptyScope_SeesNothing_InEveryScopedQuery()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);
            var none = Array.Empty<int>();
            var from = w.PayDate.AddDays(-1);
            var to = w.PayDate.AddDays(1);

            Assert.Empty(await store.GetRentedRoomsWithoutMeterReadingAsync(1, 2030, none));
            Assert.Empty(await store.GetUnpaidInvoicesAsync(null, null, null, none));
            Assert.Equal(0, (await store.GetRevenueAsync(from, to, none)).SoGiaoDich);
            Assert.Empty(await store.GetVacantRoomsAsync(null, none));
            Assert.Empty(await store.GetExpiringContractsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(30), none));
            Assert.Empty(await store.SearchTenantsAsync("sc", none));
            Assert.Empty(await store.FindRoomsByNumberAsync(w.Shared, null, none));
            Assert.Empty(await store.GetRevenueByBranchAsync(4, 2003, none));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task MeterReadings_OnlyRequestedRoomAndPeriod()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var row = Assert.Single(await store.GetMeterReadingsAsync(new[] { w.RoomB.PhongTroId }, 4, 2003));

            Assert.Equal(99m, row.ChiSoDienMoi);
            Assert.Equal(50m, row.ChiSoNuocMoi);
            Assert.Empty(await store.GetMeterReadingsAsync(new[] { w.RoomA.PhongTroId }, 4, 2003));
            Assert.Empty(await store.GetMeterReadingsAsync(new[] { w.RoomB.PhongTroId }, 5, 2003));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task TenantQueries_ExcludeDeletedContractsAndOtherTenants()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var contracts = await store.GetActiveContractsOfTenantAsync(w.TenantA.NguoiThueId);
            Assert.Equal(w.ContractA.MaHopDong, Assert.Single(contracts).MaHopDong);   // hợp đồng xóa mềm bị loại

            var unpaid = (await store.GetTenantUnpaidInvoicesAsync(w.TenantA.NguoiThueId, null, null)).Select(r => r.MaHoaDon).ToList();
            Assert.Contains(w.UnpaidA.MaHoaDon, unpaid);
            Assert.DoesNotContain(w.CancelledSent.MaHoaDon, unpaid);

            // Khách không có hợp đồng nào.
            Assert.Empty(await store.GetActiveContractsOfTenantAsync(int.MaxValue));
            Assert.Empty(await store.GetTenantUnpaidInvoicesAsync(int.MaxValue, null, null));

            await tx.RollbackAsync();
        }
    }
}
