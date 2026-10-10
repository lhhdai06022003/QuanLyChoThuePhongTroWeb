using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class AutoInvoiceStoreTests
    {
        private const int Thang = 10;
        private const int Nam = 2026;

        private readonly PostgreSqlFixture _fixture;

        public AutoInvoiceStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static DateTime Utc(int y, int m, int d, int h = 0) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

        private static async Task<DichVuDienNuocCuaPhong> PeriodAsync(
            ApplicationDbContext db, PhongTro room, int thang, int nam,
            TrangThaiGhiNhan status = TrangThaiGhiNhan.Nhap, decimal dienCu = 100, decimal dienMoi = 100,
            decimal nuocCu = 50, decimal nuocMoi = 50, bool deleted = false)
        {
            var p = new DichVuDienNuocCuaPhong
            {
                PhongTroId = room.PhongTroId,
                Thang = thang,
                Nam = nam,
                ChiSoDienCu = dienCu,
                ChiSoDienMoi = dienMoi,
                DonGiaDien = 3500,
                ChiSoNuocCu = nuocCu,
                ChiSoNuocMoi = nuocMoi,
                DonGiaNuoc = 20000,
                TrangThaiGhiNhan = status,
                IsDeleted = deleted
            };
            db.DichVuDienNuocCuaPhongs.Add(p);
            await db.SaveChangesAsync();
            return p;
        }

        private static async Task<AnhChiSoDongHo> ImageAsync(
            ApplicationDbContext db, DichVuDienNuocCuaPhong period, LoaiDongHo loai, DateTime ngayGui,
            TrangThaiXuLyAnhChiSo status = TrangThaiXuLyAnhChiSo.DocDuoc, bool deleted = false, int? confirmedBy = null, decimal? confirmed = null)
        {
            var img = new AnhChiSoDongHo
            {
                DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                LoaiDongHo = loai,
                Url = "https://example.test/x.jpg",
                PublicId = Guid.NewGuid().ToString("N"),
                NgayGui = ngayGui,
                TrangThaiXuLy = status,
                GiaTriAIGoiY = 120m,
                DoTinCay = 0.95,
                IsDeleted = deleted
            };
            if (confirmedBy.HasValue)
            {
                img.GiaTriXacNhan = confirmed ?? 120m;
                img.NguoiXacNhanId = confirmedBy;
                img.NgayXacNhan = ngayGui;
                img.DuocChonLamChiSoChinhThuc = true;
            }
            db.AnhChiSoDongHos.Add(img);
            await db.SaveChangesAsync();
            return img;
        }

        private static async Task<NguoiDung> UserAsync(ApplicationDbContext db, string tag, Role role = Role.NhanVien,
            bool active = true, bool deleted = false, int? nguoiThueId = null)
        {
            var u = new NguoiDung
            {
                TenDangNhap = $"{tag}_{Guid.NewGuid():N}"[..30],
                MatKhauHash = "x",
                Role = role,
                IsActive = active,
                IsDeleted = deleted,
                NguoiThueId = nguoiThueId
            };
            db.NguoiDungs.Add(u);
            await db.SaveChangesAsync();
            return u;
        }

        [Fact]
        public async Task GetRoomContext_UnknownOrDeletedRoom_ReturnsNull()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("D");
            var room = await seeder.RoomAsync(branch, "D");
            var store = new AutoInvoiceStore(db);

            Assert.Null(await store.GetRoomContextAsync(int.MaxValue, Thang, Nam, 3));

            room.IsDeleted = true;
            await db.SaveChangesAsync();
            Assert.Null(await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_NoPeriod_ReportsNullPeriodAndBranchData()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("P");
            var room = await seeder.RoomAsync(branch, "P");
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.NotNull(ctx);
            Assert.Null(ctx!.DichVuDienNuocCuaPhongId);
            Assert.Null(ctx.TrangThaiGhiNhan);
            Assert.Empty(ctx.Images);
            Assert.Equal(branch.ChiNhanhId, ctx.ChiNhanhId);
            Assert.Equal(room.SoPhong, ctx.SoPhong);
            Assert.Equal("0900000001", ctx.SoDienThoaiChiNhanh);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_ReturnsPeriodValuesAndImagesNewestFirst_ExcludingDeleted()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("I");
            var room = await seeder.RoomAsync(branch, "I");
            var period = await PeriodAsync(db, room, Thang, Nam, TrangThaiGhiNhan.ChoDuyet, 100, 130, 50, 60);
            var old = await ImageAsync(db, period, LoaiDongHo.Dien, Utc(2026, 10, 1, 1));
            var newest = await ImageAsync(db, period, LoaiDongHo.Dien, Utc(2026, 10, 2, 1));
            var sameTimeHigherId = await ImageAsync(db, period, LoaiDongHo.Nuoc, Utc(2026, 10, 2, 1));
            await ImageAsync(db, period, LoaiDongHo.Dien, Utc(2026, 10, 3, 1), deleted: true);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Equal(period.DichVuDienNuocCuaPhongId, ctx!.DichVuDienNuocCuaPhongId);
            Assert.Equal(TrangThaiGhiNhan.ChoDuyet, ctx.TrangThaiGhiNhan);
            Assert.Equal(100m, ctx.ChiSoDienCu);
            Assert.Equal(130m, ctx.ChiSoDienMoi);
            Assert.Equal(50m, ctx.ChiSoNuocCu);
            Assert.Equal(60m, ctx.ChiSoNuocMoi);
            Assert.Equal(
                new[] { sameTimeHigherId.AnhChiSoDongHoId, newest.AnhChiSoDongHoId, old.AnhChiSoDongHoId },
                ctx.Images.Select(i => i.AnhChiSoDongHoId).ToArray());

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_DeletedPeriod_IsIgnored()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("E");
            var room = await seeder.RoomAsync(branch, "E");
            await PeriodAsync(db, room, Thang, Nam, deleted: true);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Null(ctx!.DichVuDienNuocCuaPhongId);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_Consumption_OnlyApprovedPreviousPeriods_NewestFirst_LimitedAndSkippingDeleted()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("C");
            var room = await seeder.RoomAsync(branch, "C");
            await PeriodAsync(db, room, 6, 2026, TrangThaiGhiNhan.DaDuyet, 0, 10, 0, 1);      // quá cũ so với Take(3)
            await PeriodAsync(db, room, 7, 2026, TrangThaiGhiNhan.DaDuyet, 10, 30, 1, 3);     // 20 / 2
            await PeriodAsync(db, room, 8, 2026, TrangThaiGhiNhan.DaDuyet, 30, 60, 3, 6);     // 30 / 3
            await PeriodAsync(db, room, 9, 2026, TrangThaiGhiNhan.DaDuyet, 60, 100, 6, 10);   // 40 / 4
            await PeriodAsync(db, room, 5, 2026, TrangThaiGhiNhan.ChoDuyet, 0, 999, 0, 99);   // chưa duyệt
            await PeriodAsync(db, room, 4, 2026, TrangThaiGhiNhan.DaDuyet, 0, 888, 0, 88, deleted: true);
            await PeriodAsync(db, room, 11, 2026, TrangThaiGhiNhan.DaDuyet, 0, 777, 0, 77);   // kỳ sau
            await PeriodAsync(db, room, Thang, Nam, TrangThaiGhiNhan.DaDuyet, 100, 777, 10, 77); // chính kỳ đang xét
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Equal(3, ctx!.TieuThuCacKyTruoc.Count);
            Assert.Equal(40m, ctx.TieuThuCacKyTruoc[0].Dien);
            Assert.Equal(4m, ctx.TieuThuCacKyTruoc[0].Nuoc);
            Assert.Equal(30m, ctx.TieuThuCacKyTruoc[1].Dien);
            Assert.Equal(20m, ctx.TieuThuCacKyTruoc[2].Dien);

            var two = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 2);
            Assert.Equal(2, two!.TieuThuCacKyTruoc.Count);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_Consumption_CrossesYearBoundary()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("Y");
            var room = await seeder.RoomAsync(branch, "Y");
            await PeriodAsync(db, room, 12, 2025, TrangThaiGhiNhan.DaDuyet, 0, 15, 0, 1);
            await PeriodAsync(db, room, 11, 2025, TrangThaiGhiNhan.DaDuyet, 0, 5, 0, 1);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, 1, 2026, 3);

            Assert.Equal(new[] { 15m, 5m }, ctx!.TieuThuCacKyTruoc.Select(c => c.Dien).ToArray());

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_Contracts_ExcludesCancelledDeletedAndOutsideMonth()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("H");
            var room = await seeder.RoomAsync(branch, "H");
            var t1 = await seeder.TenantAsync("H1");
            var t2 = await seeder.TenantAsync("H2");
            var t3 = await seeder.TenantAsync("H3");
            var t4 = await seeder.TenantAsync("H4");
            var t5 = await seeder.TenantAsync("H5");

            var valid = await seeder.ContractAsync(room, t1, Utc(2026, 1, 1), Utc(2026, 12, 31));
            await seeder.ContractAsync(room, t2, Utc(2026, 1, 1), Utc(2026, 12, 31), TrangThaiHopDong.DaHuy);
            await seeder.ContractAsync(room, t3, Utc(2026, 1, 1), Utc(2026, 12, 31), deleted: true);
            await seeder.ContractAsync(room, t4, Utc(2026, 1, 1), Utc(2026, 9, 20));      // hết hạn trước tháng
            await seeder.ContractAsync(room, t5, Utc(2026, 11, 5), null);                 // bắt đầu sau tháng
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            var contract = Assert.Single(ctx!.HopDongHieuLuc);
            Assert.Equal(valid.HopDongId, contract.HopDongId);
            Assert.Equal(t1.NguoiThueId, contract.NguoiThueId);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_Contracts_UsesVietnamMonthBoundaries()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("V");
            var room = await seeder.RoomAsync(branch, "V");
            var t1 = await seeder.TenantAsync("V1");
            var t2 = await seeder.TenantAsync("V2");
            // 2026-10-31 17:30 UTC = 01/11/2026 00:30 giờ VN: đã thuộc tháng 11.
            await seeder.ContractAsync(room, t1, Utc(2026, 10, 31, 17), null);
            // 2026-10-31 16:30 UTC = 31/10/2026 23:30 giờ VN: còn thuộc tháng 10.
            var inMonth = await seeder.ContractAsync(room, t2, new DateTime(2026, 10, 31, 16, 30, 0, DateTimeKind.Utc), null);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Equal(new[] { inMonth.HopDongId }, ctx!.HopDongHieuLuc.Select(c => c.HopDongId).ToArray());

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_ActiveInvoice_IgnoresDeletedInvoicesAndOtherMonths()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("F");
            var room = await seeder.RoomAsync(branch, "F");
            var tenant = await seeder.TenantAsync("F");
            var contract = await seeder.ContractAsync(room, tenant);
            await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.DaHuy, TrangThaiHoaDon.ChuaThanhToan, 1m, null, deleted: true, month: Thang, year: Nam);
            await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 1m, null, month: 9, year: Nam);
            var store = new AutoInvoiceStore(db);

            Assert.False((await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3))!.CoHoaDonHoatDong);

            await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 1m, null, month: Thang, year: Nam);
            Assert.True((await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3))!.CoHoaDonHoatDong);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_PreviousPeriodNotClosed_Scenarios()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("K");
            var tenant = await seeder.TenantAsync("K");
            var store = new AutoInvoiceStore(db);

            // Có hợp đồng tháng trước, chưa có bản ghi kỳ trước: chưa chốt.
            var roomA = await seeder.RoomAsync(branch, "KA");
            await seeder.ContractAsync(roomA, tenant, Utc(2026, 1, 1), null);
            Assert.True((await store.GetRoomContextAsync(roomA.PhongTroId, Thang, Nam, 3))!.KyTruocChuaChot);

            // Kỳ trước đã duyệt: đã chốt.
            var roomB = await seeder.RoomAsync(branch, "KB");
            await seeder.ContractAsync(roomB, tenant, Utc(2026, 1, 1), null);
            await PeriodAsync(db, roomB, 9, 2026, TrangThaiGhiNhan.DaDuyet);
            Assert.False((await store.GetRoomContextAsync(roomB.PhongTroId, Thang, Nam, 3))!.KyTruocChuaChot);

            // Kỳ trước mới ở trạng thái chờ duyệt: chưa chốt.
            var roomC = await seeder.RoomAsync(branch, "KC");
            await seeder.ContractAsync(roomC, tenant, Utc(2026, 1, 1), null);
            await PeriodAsync(db, roomC, 9, 2026, TrangThaiGhiNhan.ChoDuyet);
            Assert.True((await store.GetRoomContextAsync(roomC.PhongTroId, Thang, Nam, 3))!.KyTruocChuaChot);

            // Kỳ trước bị xóa mềm coi như không có.
            var roomD = await seeder.RoomAsync(branch, "KD");
            await seeder.ContractAsync(roomD, tenant, Utc(2026, 1, 1), null);
            await PeriodAsync(db, roomD, 9, 2026, TrangThaiGhiNhan.DaDuyet, deleted: true);
            Assert.True((await store.GetRoomContextAsync(roomD.PhongTroId, Thang, Nam, 3))!.KyTruocChuaChot);

            // Hợp đồng chỉ bắt đầu trong tháng này: không có kỳ trước cần chốt.
            var roomE = await seeder.RoomAsync(branch, "KE");
            await seeder.ContractAsync(roomE, tenant, Utc(2026, 10, 3), null);
            Assert.False((await store.GetRoomContextAsync(roomE.PhongTroId, Thang, Nam, 3))!.KyTruocChuaChot);

            // Hợp đồng đã hủy ở tháng trước không bắt buộc kỳ trước.
            var roomF = await seeder.RoomAsync(branch, "KF");
            await seeder.ContractAsync(roomF, tenant, Utc(2026, 1, 1), Utc(2026, 8, 31));
            Assert.False((await store.GetRoomContextAsync(roomF.PhongTroId, Thang, Nam, 3))!.KyTruocChuaChot);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_Recipient_SingleContract_TrimmedEmailAndActiveAccount()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("R");
            var room = await seeder.RoomAsync(branch, "R");
            var tenant = await seeder.TenantAsync("R");
            tenant.Email = "  khach@example.com  ";
            await db.SaveChangesAsync();
            await seeder.ContractAsync(room, tenant);
            // nguoi_dung.NguoiThueId là duy nhất nên mỗi khách tối đa một tài khoản.
            var account = await UserAsync(db, "ok1", Role.KhachThue, nguoiThueId: tenant.NguoiThueId);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Equal("khach@example.com", ctx!.EmailKhach);
            Assert.Equal(account.NguoiDungId, ctx.NguoiDungKhachId);

            await tx.RollbackAsync();
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task GetRoomContext_Recipient_DeletedOrInactiveAccount_IsNotUsed(bool deleted, bool inactive)
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("Q");
            var room = await seeder.RoomAsync(branch, "Q");
            var tenant = await seeder.TenantAsync("Q");
            await seeder.ContractAsync(room, tenant);
            await UserAsync(db, "bad", Role.KhachThue, active: !inactive, deleted: deleted, nguoiThueId: tenant.NguoiThueId);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Null(ctx!.NguoiDungKhachId);
            Assert.NotNull(ctx.EmailKhach);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_Recipient_BlankEmailAndNoAccount_AreNull()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("B");
            var room = await seeder.RoomAsync(branch, "B");
            var tenant = await seeder.TenantAsync("B");
            tenant.Email = "   ";
            await db.SaveChangesAsync();
            await seeder.ContractAsync(room, tenant);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Null(ctx!.EmailKhach);
            Assert.Null(ctx.NguoiDungKhachId);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetRoomContext_Recipient_MultipleContracts_NotResolved()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("M");
            var room = await seeder.RoomAsync(branch, "M");
            var t1 = await seeder.TenantAsync("M1");
            var t2 = await seeder.TenantAsync("M2");
            await seeder.ContractAsync(room, t1, Utc(2026, 1, 1), Utc(2026, 10, 14));
            await seeder.ContractAsync(room, t2, Utc(2026, 10, 15), null);
            await UserAsync(db, "m1", Role.KhachThue, nguoiThueId: t1.NguoiThueId);
            var store = new AutoInvoiceStore(db);

            var ctx = await store.GetRoomContextAsync(room.PhongTroId, Thang, Nam, 3);

            Assert.Equal(2, ctx!.HopDongHieuLuc.Count);
            Assert.Null(ctx.EmailKhach);
            Assert.Null(ctx.NguoiDungKhachId);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task FindFallbackActor_PicksEarliestAssignment_TiesByLowestId_SkippingInvalid()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("A");
            var otherBranch = await seeder.BranchAsync("O");
            var store = new AutoInvoiceStore(db);
            var admin = await UserAsync(db, "adm", Role.Admin);

            Assert.Null(await store.FindFallbackActorAsync(branch.ChiNhanhId));

            var t0 = Utc(2026, 1, 1);
            async Task<NhanVienChiNhanh> Assign(NguoiDung user, ChiNhanh b, DateTime at, bool active = true, DateTime? revoked = null)
            {
                var a = new NhanVienChiNhanh
                {
                    NguoiDungId = user.NguoiDungId,
                    ChiNhanhId = b.ChiNhanhId,
                    NguoiPhanCongId = admin.NguoiDungId,
                    NgayPhanCong = at,
                    IsActive = active,
                    NgayThuHoi = revoked
                };
                db.NhanVienChiNhanhs.Add(a);
                await db.SaveChangesAsync();
                return a;
            }

            var revokedStaff = await UserAsync(db, "rev", Role.NhanVien);
            var inactiveAssign = await UserAsync(db, "ina", Role.NhanVien);
            var lockedStaff = await UserAsync(db, "lck", Role.NhanVien, active: false);
            var deletedStaff = await UserAsync(db, "dlt", Role.NhanVien, deleted: true);
            var adminStaff = await UserAsync(db, "adm2", Role.Admin);
            var otherBranchStaff = await UserAsync(db, "oth", Role.NhanVien);
            await Assign(revokedStaff, branch, t0.AddDays(-10), true, t0);
            await Assign(inactiveAssign, branch, t0.AddDays(-9), active: false);
            await Assign(lockedStaff, branch, t0.AddDays(-8));
            await Assign(deletedStaff, branch, t0.AddDays(-7));
            await Assign(adminStaff, branch, t0.AddDays(-6));
            await Assign(otherBranchStaff, otherBranch, t0.AddDays(-5));
            Assert.Null(await store.FindFallbackActorAsync(branch.ChiNhanhId));

            var later = await UserAsync(db, "lat", Role.NhanVien);
            var firstOfTie = await UserAsync(db, "ti1", Role.NhanVien);
            var secondOfTie = await UserAsync(db, "ti2", Role.NhanVien);
            await Assign(later, branch, t0.AddDays(5));
            await Assign(firstOfTie, branch, t0);
            await Assign(secondOfTie, branch, t0);

            Assert.Equal(firstOfTie.NguoiDungId, await store.FindFallbackActorAsync(branch.ChiNhanhId));

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task DiscardPendingChanges_DetachesTrackedEntities()
        {
            await using var db = _fixture.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(db);
            var branch = await seeder.BranchAsync("X");
            var store = new AutoInvoiceStore(db);

            var tracked = new ChiNhanh { TenChiNhanh = "x", MaChiNhanh = "X" + Guid.NewGuid().ToString("N")[..8], DiaChi = "x", MoTa = "x", SoDienThoai = "1" };
            db.ChiNhanhs.Add(tracked);
            branch.TenChiNhanh = "Đã sửa nhưng chưa lưu";
            Assert.NotEmpty(db.ChangeTracker.Entries());

            store.DiscardPendingChanges();

            Assert.Empty(db.ChangeTracker.Entries());
            await db.SaveChangesAsync();
            Assert.False(await db.ChiNhanhs.AnyAsync(c => c.MaChiNhanh == tracked.MaChiNhanh));
            Assert.NotEqual("Đã sửa nhưng chưa lưu", (await db.ChiNhanhs.AsNoTracking().SingleAsync(c => c.ChiNhanhId == branch.ChiNhanhId)).TenChiNhanh);

            await tx.RollbackAsync();
        }
    }
}
