using System;
using System.Collections.Generic;
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
    // Truy vấn của trợ lý AI chạy được trên PostgreSQL thật và lọc đúng theo chi nhánh, trạng thái, xóa mềm.
    [Collection("PostgreSqlCollection")]
    public class AiAssistantStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public AiAssistantStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static readonly DateTime RevenueFrom = new(2001, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime RevenueToExclusive = new(2001, 3, 11, 0, 0, 0, DateTimeKind.Utc);

        private sealed class World
        {
            public string Suffix = "";
            public string SharedRoomNumber = "";
            public ChiNhanh BranchA = null!;
            public ChiNhanh BranchB = null!;
            public PhongTro RoomA = null!;
            public PhongTro RoomB = null!;
            public PhongTro RoomA2Vacant = null!;
            public PhongTro RoomA3WithMeter = null!;
            public PhongTro VacantA = null!;
            public PhongTro VacantB = null!;
            public NguoiThue TenantA = null!;
            public NguoiThue TenantB = null!;
            public HopDong ContractA = null!;
            public HopDong ContractB = null!;
            public HopDong ContractA2 = null!;
            public HoaDon Unpaid = null!;
            public HoaDon Partial = null!;
            public HoaDon Paid = null!;
            public HoaDon Nhap = null!;
            public HoaDon ChoDuyet = null!;
            public HoaDon DaChot = null!;
            public HoaDon DeletedSent = null!;
            public HoaDon VacantRoomInvoice = null!;
            public HoaDon UnpaidB = null!;
            public HoaDon RevenueInvoiceA = null!;
            public HoaDon RevenueInvoiceB = null!;
        }

        private static async Task<World> SeedAsync(ApplicationDbContext db)
        {
            var w = new World { Suffix = Guid.NewGuid().ToString("N")[..8] };
            var s = w.Suffix;
            w.SharedRoomNumber = $"S{s[..6]}";

            w.BranchA = new ChiNhanh { TenChiNhanh = $"AiA {s}", MaChiNhanh = $"AA{s[..5]}", DiaChi = "A", SoDienThoai = "0900000001", MoTa = "A" };
            w.BranchB = new ChiNhanh { TenChiNhanh = $"AiB {s}", MaChiNhanh = $"AB{s[..5]}", DiaChi = "B", SoDienThoai = "0900000002", MoTa = "B" };
            db.ChiNhanhs.AddRange(w.BranchA, w.BranchB);
            await db.SaveChangesAsync();

            PhongTro Room(ChiNhanh b, string number, TrangThaiPhong state, decimal price, bool deleted = false) => new()
            {
                ChiNhanhId = b.ChiNhanhId, SoPhong = number, GiaThue = price, DienTich = 10, MoTa = "x", TrangThai = state, TangLau = 2, IsDeleted = deleted
            };
            w.RoomA = Room(w.BranchA, w.SharedRoomNumber, TrangThaiPhong.DaThue, 3_000_000m);
            w.RoomB = Room(w.BranchB, w.SharedRoomNumber, TrangThaiPhong.DaThue, 3_500_000m);
            w.RoomA2Vacant = Room(w.BranchA, $"V2{s[..5]}", TrangThaiPhong.Trong, 900_000m);
            w.RoomA3WithMeter = Room(w.BranchA, $"M3{s[..5]}", TrangThaiPhong.DaThue, 1_000_000m);
            w.VacantA = Room(w.BranchA, $"VA{s[..5]}", TrangThaiPhong.Trong, 1_000_000m);
            w.VacantB = Room(w.BranchB, $"VB{s[..5]}", TrangThaiPhong.Trong, 2_000_000m);
            var deletedRoom = Room(w.BranchA, $"X{s[..6]}", TrangThaiPhong.DaThue, 1m, deleted: true);
            db.PhongTros.AddRange(w.RoomA, w.RoomB, w.RoomA2Vacant, w.RoomA3WithMeter, w.VacantA, w.VacantB, deletedRoom);
            await db.SaveChangesAsync();

            w.TenantA = new NguoiThue { HoVaTen = $"Khach A {s}", SoDienThoai = "0911111111", CCCD = $"A{s}", Email = $"a{s}@t.vn" };
            w.TenantB = new NguoiThue { HoVaTen = $"Khach B {s}", SoDienThoai = "0922222222", CCCD = $"B{s}", Email = $"b{s}@t.vn" };
            db.NguoiThues.AddRange(w.TenantA, w.TenantB);
            await db.SaveChangesAsync();

            HopDong Contract(string code, PhongTro room, NguoiThue tenant, TrangThaiHopDong state, DateTime? end) => new()
            {
                MaHopDong = $"{code}{s}", PhongTroId = room.PhongTroId, NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-3), ThoiDiemKetThuc = end,
                TienCocPhong = 6_000_000m, TienThuePhong = 3_000_000m, TrangThaiHopDong = state
            };
            w.ContractA = Contract("CA", w.RoomA, w.TenantA, TrangThaiHopDong.DangHoatDong, DateTime.UtcNow.AddDays(10));
            w.ContractB = Contract("CB", w.RoomB, w.TenantB, TrangThaiHopDong.DangHoatDong, DateTime.UtcNow.AddDays(10));
            w.ContractA2 = Contract("C2", w.RoomA2Vacant, w.TenantA, TrangThaiHopDong.DaKetThuc, DateTime.UtcNow.AddDays(-5));
            var contractA3 = Contract("C3", w.RoomA3WithMeter, w.TenantB, TrangThaiHopDong.DangHoatDong, DateTime.UtcNow.AddDays(200));
            db.HopDongs.AddRange(w.ContractA, w.ContractB, w.ContractA2, contractA3);
            await db.SaveChangesAsync();

            db.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
            {
                PhongTroId = w.RoomA3WithMeter.PhongTroId, Thang = 10, Nam = 2026, ChiSoDienCu = 100, ChiSoDienMoi = 150, DonGiaDien = 3500,
                ChiSoNuocCu = 10, ChiSoNuocMoi = 15, DonGiaNuoc = 20000, TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
            });

            var paymentSeq = 0;
            HoaDon Invoice(string code, HopDong contract, int thang, int nam, decimal total, TrangThaiPhatHanhHoaDon issue, TrangThaiHoaDon pay, bool deleted = false) => new()
            {
                MaHoaDon = $"{code}{s}", HopDongId = contract.HopDongId, Thang = thang, Nam = nam, TongTien = total,
                TrangThaiPhatHanh = issue, TrangThaiHoaDon = pay, IsDeleted = deleted
            };
            w.Unpaid = Invoice("HU", w.ContractA, 1, 2026, 1_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan);
            w.Partial = Invoice("HP", w.ContractA, 2, 2026, 1_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ThanhToanMotPhan);
            w.Paid = Invoice("HD", w.ContractA, 3, 2026, 1_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan);
            w.Nhap = Invoice("HN", w.ContractA, 4, 2026, 1_000_000m, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan);
            w.ChoDuyet = Invoice("HC", w.ContractA, 5, 2026, 1_000_000m, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan);
            w.DaChot = Invoice("HT", w.ContractA, 6, 2026, 1_000_000m, TrangThaiPhatHanhHoaDon.DaChot, TrangThaiHoaDon.ChuaThanhToan);
            w.DeletedSent = Invoice("HX", w.ContractA, 7, 2026, 1_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan, deleted: true);
            w.VacantRoomInvoice = Invoice("HV", w.ContractA2, 8, 2026, 700_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan);
            w.UnpaidB = Invoice("HB", w.ContractB, 1, 2026, 2_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.ChuaThanhToan);
            w.RevenueInvoiceA = Invoice("RA", w.ContractA, 5, 2002, 5_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan);
            w.RevenueInvoiceB = Invoice("RB", w.ContractB, 5, 2002, 5_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan);
            var revenueOtherPeriod = Invoice("RO", w.ContractA, 6, 2002, 5_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan);
            var revenueDeletedInvoice = Invoice("RD", w.ContractA, 7, 2002, 5_000_000m, TrangThaiPhatHanhHoaDon.DaGui, TrangThaiHoaDon.DaThanhToan, deleted: true);
            db.HoaDons.AddRange(w.Unpaid, w.Partial, w.Paid, w.Nhap, w.ChoDuyet, w.DaChot, w.DeletedSent, w.VacantRoomInvoice, w.UnpaidB,
                w.RevenueInvoiceA, w.RevenueInvoiceB, revenueOtherPeriod, revenueDeletedInvoice);
            await db.SaveChangesAsync();

            LichSuThanhToan Payment(HoaDon invoice, decimal amount, DateTime date, bool deleted = false) => new()
            {
                HoaDonId = invoice.HoaDonId, MaGiaoDich = $"AI{s}{++paymentSeq}", SoTienThanhToan = amount,
                PhuongThucThanhToan = PhuongThucThanhToan.TienMat, NgayThanhToan = date, IsDeleted = deleted
            };
            var anyDate = DateTime.UtcNow;
            db.LichSuThanhToans.AddRange(
                Payment(w.Partial, 400_000m, anyDate),
                Payment(w.Partial, 300_000m, anyDate, deleted: true),
                Payment(w.Paid, 1_000_000m, anyDate),
                // Doanh thu theo ngày thanh toán: biên trái gồm, biên phải loại, khoản đã xóa và hóa đơn đã xóa bị loại.
                Payment(w.RevenueInvoiceA, 100_000m, RevenueFrom),
                Payment(w.RevenueInvoiceA, 200_000m, RevenueToExclusive),
                Payment(w.RevenueInvoiceA, 50_000m, RevenueFrom.AddHours(5)),
                Payment(w.RevenueInvoiceA, 999_000m, RevenueFrom.AddHours(6), deleted: true),
                Payment(w.RevenueInvoiceB, 7_000m, RevenueFrom.AddHours(7)),
                Payment(revenueDeletedInvoice, 888_000m, RevenueFrom.AddHours(8)),
                Payment(revenueOtherPeriod, 11_000m, anyDate));
            await db.SaveChangesAsync();

            return w;
        }

        private static int[] A(World w) => new[] { w.BranchA.ChiNhanhId };
        private static int[] B(World w) => new[] { w.BranchB.ChiNhanhId };

        [Fact]
        public async Task UnpaidInvoices_OnlySentAndNotFullyPaid_IncludingVacatedRoom_ExcludingDeletedPayments()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var rows = await store.GetUnpaidInvoicesAsync(null, null, null, A(w));

            var codes = rows.Select(r => r.MaHoaDon).ToHashSet();
            Assert.Contains(w.Unpaid.MaHoaDon, codes);
            Assert.Contains(w.Partial.MaHoaDon, codes);
            Assert.Contains(w.VacantRoomInvoice.MaHoaDon, codes);          // phòng nay đã trống vẫn tính
            Assert.DoesNotContain(w.Paid.MaHoaDon, codes);
            Assert.DoesNotContain(w.Nhap.MaHoaDon, codes);
            Assert.DoesNotContain(w.ChoDuyet.MaHoaDon, codes);
            Assert.DoesNotContain(w.DaChot.MaHoaDon, codes);
            Assert.DoesNotContain(w.DeletedSent.MaHoaDon, codes);
            Assert.DoesNotContain(w.UnpaidB.MaHoaDon, codes);               // chi nhánh B ngoài phạm vi

            Assert.Equal(400_000m, rows.Single(r => r.MaHoaDon == w.Partial.MaHoaDon).DaThu); // khoản đã xóa không tính
            Assert.Equal(0m, rows.Single(r => r.MaHoaDon == w.Unpaid.MaHoaDon).DaThu);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task UnpaidInvoices_BranchScope_EmptyListSeesNothing_NullSeesAll()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            Assert.Empty(await store.GetUnpaidInvoicesAsync(null, null, null, Array.Empty<int>()));

            var b = await store.GetUnpaidInvoicesAsync(null, null, null, B(w));
            Assert.Equal(new[] { w.UnpaidB.MaHoaDon }, b.Select(r => r.MaHoaDon));

            var all = (await store.GetUnpaidInvoicesAsync(null, null, null, null)).Select(r => r.MaHoaDon).ToHashSet();
            Assert.Contains(w.Unpaid.MaHoaDon, all);
            Assert.Contains(w.UnpaidB.MaHoaDon, all);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task UnpaidInvoices_PeriodAndRoomFilters()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var feb = await store.GetUnpaidInvoicesAsync(2, 2026, null, A(w));
            Assert.Equal(new[] { w.Partial.MaHoaDon }, feb.Select(r => r.MaHoaDon));

            var byRoom = await store.GetUnpaidInvoicesAsync(null, null, w.RoomA2Vacant.PhongTroId, A(w));
            Assert.Equal(new[] { w.VacantRoomInvoice.MaHoaDon }, byRoom.Select(r => r.MaHoaDon));

            // Phòng thuộc chi nhánh ngoài phạm vi thì không có dữ liệu dù biết id phòng.
            Assert.Empty(await store.GetUnpaidInvoicesAsync(null, null, w.RoomB.PhongTroId, A(w)));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task Revenue_ByPaymentDate_LeftBoundaryIncluded_RightExcluded_AndBranchScoped()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var a = await store.GetRevenueAsync(RevenueFrom, RevenueToExclusive, A(w));
            Assert.Equal(150_000m, a.TongTien);   // 100k (biên trái) + 50k; loại 200k (biên phải), khoản xóa, hóa đơn xóa
            Assert.Equal(2, a.SoGiaoDich);

            var b = await store.GetRevenueAsync(RevenueFrom, RevenueToExclusive, B(w));
            Assert.Equal(7_000m, b.TongTien);
            Assert.Equal(1, b.SoGiaoDich);

            var none = await store.GetRevenueAsync(RevenueFrom, RevenueToExclusive, Array.Empty<int>());
            Assert.Equal(0m, none.TongTien);
            Assert.Equal(0, none.SoGiaoDich);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task RevenueByBranch_UsesInvoicePeriod_GroupsByBranch_AndSkipsDeleted()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var onlyA = await store.GetRevenueByBranchAsync(5, 2002, A(w));
            var rowA = Assert.Single(onlyA);
            Assert.Equal(w.BranchA.ChiNhanhId, rowA.ChiNhanhId);
            Assert.Equal(350_000m, rowA.TongTien);   // 100k + 200k + 50k, bỏ khoản xóa mềm
            Assert.Equal(3, rowA.SoGiaoDich);

            var both = await store.GetRevenueByBranchAsync(5, 2002, new[] { w.BranchA.ChiNhanhId, w.BranchB.ChiNhanhId });
            Assert.Equal(2, both.Count);
            Assert.Equal(7_000m, both.Single(r => r.ChiNhanhId == w.BranchB.ChiNhanhId).TongTien);

            var otherPeriod = await store.GetRevenueByBranchAsync(6, 2002, A(w));
            Assert.Equal(11_000m, Assert.Single(otherPeriod).TongTien);

            Assert.Empty(await store.GetRevenueByBranchAsync(5, 2002, Array.Empty<int>()));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task RevenueByBranch_ListsBranchesInScopeWithoutRevenue_AsZero()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            // Kỳ 6/2002 chỉ chi nhánh A có giao dịch.
            var rows = await store.GetRevenueByBranchAsync(6, 2002, new[] { w.BranchA.ChiNhanhId, w.BranchB.ChiNhanhId });

            Assert.Equal(2, rows.Count);
            Assert.Equal(11_000m, rows.Single(r => r.ChiNhanhId == w.BranchA.ChiNhanhId).TongTien);
            var zero = rows.Single(r => r.ChiNhanhId == w.BranchB.ChiNhanhId);
            Assert.Equal(0m, zero.TongTien);
            Assert.Equal(0, zero.SoGiaoDich);

            w.BranchB.IsDeleted = true;
            await db.SaveChangesAsync();
            var afterDelete = await store.GetRevenueByBranchAsync(6, 2002, new[] { w.BranchA.ChiNhanhId, w.BranchB.ChiNhanhId });
            Assert.Equal(w.BranchA.ChiNhanhId, Assert.Single(afterDelete).ChiNhanhId);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task RentedRoomsWithoutMeterReading_ListsOnlyRoomsWithoutReading_ByBranch()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var rows = await store.GetRentedRoomsWithoutMeterReadingAsync(10, 2026, A(w));

            var ids = rows.Select(r => r.PhongTroId).ToHashSet();
            Assert.Contains(w.RoomA.PhongTroId, ids);
            Assert.DoesNotContain(w.RoomA3WithMeter.PhongTroId, ids);   // đã có chỉ số tháng này
            Assert.DoesNotContain(w.RoomB.PhongTroId, ids);             // chi nhánh B
            Assert.DoesNotContain(w.VacantA.PhongTroId, ids);           // phòng trống

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task VacantRooms_FilterByPriceAndBranch()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var a = (await store.GetVacantRoomsAsync(null, A(w))).Select(r => r.PhongTroId).ToHashSet();
            Assert.Contains(w.VacantA.PhongTroId, a);
            Assert.DoesNotContain(w.VacantB.PhongTroId, a);

            var cheap = (await store.GetVacantRoomsAsync(950_000m, A(w))).Select(r => r.PhongTroId).ToHashSet();
            Assert.Contains(w.RoomA2Vacant.PhongTroId, cheap);
            Assert.DoesNotContain(w.VacantA.PhongTroId, cheap);

            var b = (await store.GetVacantRoomsAsync(null, B(w))).Select(r => r.PhongTroId).ToList();
            Assert.Equal(new[] { w.VacantB.PhongTroId }, b);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task ExpiringContracts_OnlyActiveInWindow_AndBranchScoped()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var rows = await store.GetExpiringContractsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(30), A(w));

            Assert.Contains(rows, r => r.TenChiNhanh == w.BranchA.TenChiNhanh && r.SoPhong == w.RoomA.SoPhong);
            Assert.DoesNotContain(rows, r => r.TenChiNhanh == w.BranchB.TenChiNhanh);
            Assert.DoesNotContain(rows, r => r.SoPhong == w.RoomA3WithMeter.SoPhong);   // hết hạn sau 200 ngày
            Assert.DoesNotContain(rows, r => r.SoPhong == w.RoomA2Vacant.SoPhong);      // hợp đồng đã kết thúc

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task SearchTenants_ByNameOrRoom_BranchScoped()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var inA = await store.SearchTenantsAsync($"khach a {w.Suffix}", A(w));
            Assert.All(inA, r => Assert.Equal(w.BranchA.TenChiNhanh, r.TenChiNhanh));
            Assert.Contains(inA, r => r.HoVaTen == w.TenantA.HoVaTen);

            Assert.Empty(await store.SearchTenantsAsync($"khach a {w.Suffix}", B(w)));
            Assert.NotEmpty(await store.SearchTenantsAsync($"khach a {w.Suffix}", null));

            var byRoom = await store.SearchTenantsAsync(w.SharedRoomNumber, A(w));
            Assert.Contains(byRoom, r => r.HoVaTen == w.TenantA.HoVaTen);
            Assert.DoesNotContain(byRoom, r => r.HoVaTen == w.TenantB.HoVaTen);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task FindRoomsByNumber_ScopeBranchNameAndDeleted()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            Assert.Equal(2, (await store.FindRoomsByNumberAsync(w.SharedRoomNumber, null, null)).Count);

            var inA = await store.FindRoomsByNumberAsync($" {w.SharedRoomNumber.ToLower()} ", null, A(w));
            Assert.Equal(w.RoomA.PhongTroId, Assert.Single(inA).PhongTroId);

            var byName = await store.FindRoomsByNumberAsync(w.SharedRoomNumber, $"aib {w.Suffix}", null);
            Assert.Equal(w.RoomB.PhongTroId, Assert.Single(byName).PhongTroId);

            Assert.Empty(await store.FindRoomsByNumberAsync(w.SharedRoomNumber, null, Array.Empty<int>()));
            Assert.Empty(await store.FindRoomsByNumberAsync($"X{w.Suffix[..6]}", null, null));   // phòng đã xóa mềm

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task MeterReadings_ForRoomAndPeriod()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var rows = await store.GetMeterReadingsAsync(new[] { w.RoomA3WithMeter.PhongTroId, w.RoomA.PhongTroId }, 10, 2026);

            var row = Assert.Single(rows);
            Assert.Equal(w.RoomA3WithMeter.PhongTroId, row.PhongTroId);
            Assert.Equal(150m, row.ChiSoDienMoi);
            Assert.Empty(await store.GetMeterReadingsAsync(new[] { w.RoomA3WithMeter.PhongTroId }, 9, 2026));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task TenantQueries_ReturnOnlyThatTenantsData()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var w = await SeedAsync(db);
            var store = new AiAssistantStore(db);

            var contracts = await store.GetActiveContractsOfTenantAsync(w.TenantA.NguoiThueId);
            var contract = Assert.Single(contracts);   // hợp đồng đã kết thúc không tính
            Assert.Equal(w.ContractA.MaHopDong, contract.MaHopDong);
            Assert.Equal(3_000_000m, contract.TienThuePhong);
            Assert.Equal(6_000_000m, contract.TienCocPhong);

            var unpaid = (await store.GetTenantUnpaidInvoicesAsync(w.TenantA.NguoiThueId, null, null)).Select(r => r.MaHoaDon).ToHashSet();
            Assert.Contains(w.Unpaid.MaHoaDon, unpaid);
            Assert.Contains(w.Partial.MaHoaDon, unpaid);
            Assert.DoesNotContain(w.UnpaidB.MaHoaDon, unpaid);
            Assert.DoesNotContain(w.Nhap.MaHoaDon, unpaid);

            var tenantB = (await store.GetTenantUnpaidInvoicesAsync(w.TenantB.NguoiThueId, 1, 2026)).Select(r => r.MaHoaDon).ToList();
            Assert.Equal(new[] { w.UnpaidB.MaHoaDon }, tenantB);

            await tx.RollbackAsync();
        }
    }
}
