using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationPayments;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

[Collection("PostgreSqlCollection")]
public sealed class SelfServiceReservationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task CheckoutImmediatelyUsesPublicPrice_StaysVisibleUntilFullyPaid_ThenHidesAndStarts48Hours()
    {
        await using var seed = await Seed.Create(fixture);
        var now = DateTime.UtcNow;
        int holdId;
        await using (var db = fixture.CreateDbContext())
        {
            var store = new ReservationStore(db);
            var created = await store.CreateForPaymentAsync(seed.GuestA, seed.RoomId, null, now);
            Assert.Equal(ReservationError.None, created.Error);
            holdId = created.Id!.Value;
            var hold = await db.YeuCauGiuChos.Include(h => h.YeuCauThanhToanGiuChos).SingleAsync(h => h.YeuCauGiuChoId == holdId);
            Assert.Equal(TrangThaiYeuCauGiuCho.ChoThanhToan, hold.TrangThai);
            Assert.Equal(450000, hold.SoTienGiuCho);
            Assert.Null(hold.NguoiDuyetId);
            Assert.Null(hold.NgayDuyet);
            Assert.Equal(now.AddMinutes(30), hold.HanThanhToan);
            var payment = Assert.Single(hold.YeuCauThanhToanGiuChos);
            Assert.Equal("TTPTGC" + holdId, payment.MaYeuCau);
            Assert.Equal(450000, payment.SoTien);
            Assert.Equal(seed.GuestA, payment.NguoiTaoId);
            Assert.NotNull(await new PublicRoomStore(db, TimeProvider.System).GetAvailableAsync(seed.RoomId));
        }
        await using (var db = fixture.CreateDbContext())
        {
            // Quản lý đổi giá sau khi khách bắt đầu thanh toán không đổi số tiền đã chốt.
            await db.PhongTros.Where(p => p.PhongTroId == seed.RoomId).ExecuteUpdateAsync(s => s.SetProperty(p => p.TienCocPhong, 999000m));
            var repeated = await new ReservationStore(db).CreateForPaymentAsync(seed.GuestA, seed.RoomId, null, now.AddMinutes(1));
            Assert.Equal(holdId, repeated.Id);
            Assert.Equal(450000, (await db.YeuCauGiuChos.SingleAsync(h => h.YeuCauGiuChoId == holdId)).SoTienGiuCho);
            var blocked = await new ReservationStore(db).CreateForPaymentAsync(seed.GuestB, seed.RoomId, null, now.AddMinutes(1));
            Assert.Null(blocked.Id);
        }
        await using (var db = fixture.CreateDbContext())
        {
            var paymentStore = new ReservationPaymentStore(db);
            var partial = await paymentStore.RecordSystemTransferAsync("TTPTGC" + holdId, "SEPAY-part-" + holdId,
                100000, now.AddMinutes(2), "test", now.AddMinutes(2));
            Assert.Equal(SystemReservationRecordStatus.Recorded, partial.Status);
            Assert.False(partial.VuaCoHieuLuc);
            Assert.NotNull(await new PublicRoomStore(db, TimeProvider.System).GetAvailableAsync(seed.RoomId));
            var received = now.AddMinutes(4);
            var full = await paymentStore.RecordSystemTransferAsync("TTPTGC" + holdId, "SEPAY-full-" + holdId,
                350000, received, "test", received);
            Assert.True(full.VuaCoHieuLuc);
            Assert.Equal(received.AddHours(48), full.HanKyHopDongUtc);
            Assert.Null(await new PublicRoomStore(db, TimeProvider.System).GetAvailableAsync(seed.RoomId));
            var duplicate = await paymentStore.RecordSystemTransferAsync("TTPTGC" + holdId, "SEPAY-full-" + holdId,
                350000, received, "test", received);
            Assert.Equal(SystemReservationRecordStatus.Duplicate, duplicate.Status);
            Assert.Equal(2, await db.GiaoDichGiuChos.CountAsync(g => g.YeuCauThanhToanGiuCho.YeuCauGiuChoId == holdId));
        }
    }

    [Fact]
    public async Task PartialPaymentExpiresAfter30Minutes_AndLateMoneyCannotTakeRoomFromNextCustomer()
    {
        await using var seed = await Seed.Create(fixture);
        var now = DateTime.UtcNow;
        int firstId;
        await using (var db = fixture.CreateDbContext())
        {
            firstId = (await new ReservationStore(db).CreateForPaymentAsync(seed.GuestA, seed.RoomId, null, now)).Id!.Value;
            await new ReservationPaymentStore(db).RecordSystemTransferAsync("TTPTGC" + firstId, "SEPAY-first-" + firstId,
                100000, now.AddMinutes(1), "test", now.AddMinutes(1));
        }
        await using (var db = fixture.CreateDbContext())
        {
            var store = new ReservationStore(db);
            await store.ExpireUnpaidAsync(now.AddMinutes(31));
            Assert.Equal(TrangThaiYeuCauGiuCho.HetHan,
                (await db.YeuCauGiuChos.SingleAsync(h => h.YeuCauGiuChoId == firstId)).TrangThai);
            var next = await store.CreateForPaymentAsync(seed.GuestB, seed.RoomId, null, now.AddMinutes(31));
            Assert.NotNull(next.Id);
            var late = await new ReservationPaymentStore(db).RecordSystemTransferAsync("TTPTGC" + firstId, "SEPAY-late-" + firstId,
                350000, now.AddMinutes(32), "test", now.AddMinutes(32));
            Assert.True(late.CanHoanTien);
            Assert.False(late.VuaCoHieuLuc);
            Assert.NotNull(await new PublicRoomStore(db, TimeProvider.System).GetAvailableAsync(seed.RoomId));
        }
    }

    [Fact]
    public async Task TwoCustomersAtOnceReceiveOnlyOneCheckout()
    {
        await using var seed = await Seed.Create(fixture);
        var now = DateTime.UtcNow;
        async Task<ReservationCreateResult> Checkout(int actor)
        {
            await using var db = fixture.CreateDbContext();
            return await new ReservationStore(db).CreateForPaymentAsync(actor, seed.RoomId, null, now);
        }
        var results = await Task.WhenAll(Checkout(seed.GuestA), Checkout(seed.GuestB));
        Assert.Single(results.Where(r => r.Id.HasValue));
        await using var check = fixture.CreateDbContext();
        Assert.Equal(1, await check.YeuCauThanhToanGiuChos.CountAsync(p => p.YeuCauGiuCho.PhongTroId == seed.RoomId));
    }

    [Fact]
    public async Task RefundOnlyLateMoneyNeverCountsTowardOnTimeDeposit()
    {
        await using var seed = await Seed.Create(fixture);
        var now = DateTime.UtcNow;
        await using var db = fixture.CreateDbContext();
        var id = (await new ReservationStore(db).CreateForPaymentAsync(seed.GuestA, seed.RoomId, null, now)).Id!.Value;
        var payments = new ReservationPaymentStore(db);
        var late = await payments.RecordSystemTransferAsync("TTPTGC" + id, "SEPAY-late-first-" + id,
            500000, now.AddMinutes(31), "test", now.AddMinutes(31));
        Assert.True(late.CanHoanTien);
        var onTime = await payments.RecordSystemTransferAsync("TTPTGC" + id, "SEPAY-small-" + id,
            100000, now.AddMinutes(1), "test", now.AddMinutes(32));
        Assert.False(onTime.VuaCoHieuLuc);
        Assert.NotNull(await new PublicRoomStore(db, TimeProvider.System).GetAvailableAsync(seed.RoomId));
    }

    [Fact]
    public async Task OutOfOrderCallbacksUseActualCompletionTimeForContractDeadline()
    {
        await using var seed = await Seed.Create(fixture);
        var now = DateTime.UtcNow;
        await using var db = fixture.CreateDbContext();
        var id = (await new ReservationStore(db).CreateForPaymentAsync(seed.GuestA, seed.RoomId, null, now)).Id!.Value;
        var payments = new ReservationPaymentStore(db);
        await payments.RecordSystemTransferAsync("TTPTGC" + id, "SEPAY-later-" + id,
            400000, now.AddMinutes(10), "test", now.AddMinutes(10));
        var complete = await payments.RecordSystemTransferAsync("TTPTGC" + id, "SEPAY-earlier-" + id,
            50000, now.AddMinutes(2), "test", now.AddMinutes(11));
        Assert.True(complete.VuaCoHieuLuc);
        Assert.InRange(Math.Abs((complete.HanKyHopDongUtc!.Value - now.AddMinutes(10).AddHours(48)).TotalMilliseconds), 0, 1);
    }

    [Fact]
    public async Task PublishedRoomWithZeroDeposit_IsHiddenAndCannotCheckout()
    {
        await using var seed = await Seed.Create(fixture);
        await using var db = fixture.CreateDbContext();
        await db.PhongTros.Where(p => p.PhongTroId == seed.RoomId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.TienCocPhong, 0m));

        var result = await new ReservationStore(db).CreateForPaymentAsync(seed.GuestA, seed.RoomId, null, DateTime.UtcNow);

        Assert.Equal(ReservationError.RoomUnavailable, result.Error);
        Assert.False(await db.YeuCauGiuChos.AnyAsync(h => h.PhongTroId == seed.RoomId));
        var publicRooms = new PublicRoomStore(db, TimeProvider.System);
        Assert.Null(await publicRooms.GetAvailableAsync(seed.RoomId));
        Assert.DoesNotContain(await publicRooms.ListAvailableAsync(), room => room.PhongTroId == seed.RoomId);
    }

    [Fact]
    public async Task PublicRoom_ExposesRoomDeposit()
    {
        await using var seed = await Seed.Create(fixture);
        await using var db = fixture.CreateDbContext();

        var room = await new PublicRoomStore(db, TimeProvider.System).GetAvailableAsync(seed.RoomId);

        Assert.Equal(450000m, room!.DepositAmount);
    }

    private sealed class Seed(PostgreSqlFixture fixture, int branchId, int roomId, int guestA, int guestB) : IAsyncDisposable
    {
        public int RoomId => roomId;
        public int GuestA => guestA;
        public int GuestB => guestB;
        public static async Task<Seed> Create(PostgreSqlFixture fixture)
        {
            await using var db = fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var branch = new ChiNhanh { MaChiNhanh = "S" + suffix, TenChiNhanh = "Self " + suffix,
                DiaChi = "Test", MoTa = "Test", SoDienThoai = "0900000000" };
            var room = new PhongTro { ChiNhanh = branch, SoPhong = suffix, GiaThue = 1500000,
                DienTich = 25, MoTa = "Test", DuocDangTin = true, TienCocPhong = 450000, TrangThai = TrangThaiPhong.Trong };
            var users = Enumerable.Range(0, 2).Select(i => new NguoiDung { TenDangNhap = "self_" + suffix + i,
                MatKhauHash = "test", Role = Role.KhachVangLai }).ToArray();
            db.Add(room);
            foreach (var user in users) db.KhachVangLais.Add(new KhachVangLai {
                NguoiDung = user, HoTen = "Self test", SoDienThoai = "0912345678", Email = "self_" + suffix + "@example.com" });
            await db.SaveChangesAsync();
            return new(fixture, branch.ChiNhanhId, room.PhongTroId, users[0].NguoiDungId, users[1].NguoiDungId);
        }
        public async ValueTask DisposeAsync()
        {
            await using var db = fixture.CreateDbContext();
            var holds = db.YeuCauGiuChos.Where(h => h.PhongTroId == roomId).Select(h => h.YeuCauGiuChoId);
            var payments = db.YeuCauThanhToanGiuChos.Where(p => holds.Contains(p.YeuCauGiuChoId)).Select(p => p.YeuCauThanhToanGiuChoId);
            await db.GiaoDichGiuChos.Where(g => payments.Contains(g.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.LichSuTrangThaiYeuCauThanhToanGiuChos.Where(h => payments.Contains(h.YeuCauThanhToanGiuChoId)).ExecuteDeleteAsync();
            await db.YeuCauThanhToanGiuChos.Where(p => holds.Contains(p.YeuCauGiuChoId)).ExecuteDeleteAsync();
            await db.LichSuTrangThaiYeuCauGiuChos.Where(h => holds.Contains(h.YeuCauGiuChoId)).ExecuteDeleteAsync();
            await db.YeuCauGiuChos.Where(h => h.PhongTroId == roomId).ExecuteDeleteAsync();
            await db.PhongTros.Where(p => p.PhongTroId == roomId).ExecuteDeleteAsync();
            await db.KhachVangLais.Where(g => g.NguoiDungId == guestA || g.NguoiDungId == guestB).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(u => u.NguoiDungId == guestA || u.NguoiDungId == guestB).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId).ExecuteDeleteAsync();
        }
    }
}
