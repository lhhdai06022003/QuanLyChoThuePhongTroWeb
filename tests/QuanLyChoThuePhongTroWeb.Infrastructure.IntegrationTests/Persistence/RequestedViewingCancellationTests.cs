using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Viewings;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

[Collection("PostgreSqlCollection")]
public sealed class RequestedViewingCancellationTests(PostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(TrangThaiPhong.DaThue, TrangThaiYeuCauXemPhong.ChoNhanVienXuLy, true)]
    [InlineData(TrangThaiPhong.DaThue, TrangThaiYeuCauXemPhong.DaXacNhanLich, true)]
    [InlineData(TrangThaiPhong.Trong, TrangThaiYeuCauXemPhong.ChoNhanVienXuLy, false)]
    [InlineData(TrangThaiPhong.DaThue, TrangThaiYeuCauXemPhong.DaHuy, false)]
    [InlineData(TrangThaiPhong.DaThue, TrangThaiYeuCauXemPhong.DaXemPhong, false)]
    public async Task CancelOnlyActiveViewingOfRentedRoom(TrangThaiPhong roomStatus,
        TrangThaiYeuCauXemPhong status, bool succeeds) =>
        await Exercise(roomStatus, status, ViewingAdminAction.CancelBecauseRented, succeeds);

    [Fact]
    public async Task CannotConfirmViewingAfterRoomIsRented() =>
        await Exercise(TrangThaiPhong.DaThue, TrangThaiYeuCauXemPhong.ChoNhanVienXuLy,
            ViewingAdminAction.Confirm, false);

    private async Task Exercise(TrangThaiPhong roomStatus, TrangThaiYeuCauXemPhong status,
        ViewingAdminAction action, bool succeeds)
    {
        await using var db = fixture.CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var branch = new ChiNhanh { MaChiNhanh = "V" + suffix, TenChiNhanh = "Viewing test",
            DiaChi = "Test", MoTa = "Test", SoDienThoai = "0900000001" };
        var staff = new NguoiDung { TenDangNhap = "viewing_staff_" + suffix,
            MatKhauHash = "test", Role = Role.Admin };
        var room = new PhongTro { ChiNhanh = branch, SoPhong = "V" + suffix,
            GiaThue = 1000000m, DienTich = 20, MoTa = "Test", DuocDangTin = true,
            TrangThai = roomStatus };
        var now = DateTime.UtcNow;
        var request = new YeuCauXemPhong { PhongTro = room, HoTen = "Khách kiểm thử",
            SoDienThoai = "0912345678", Email = "viewing@example.com",
            ThoiGianMongMuon = now.AddDays(1), TrangThai = status,
            NguonTao = NguonTaoYeuCauXemPhong.TrangCongKhai, NgayTao = now };
        db.NguoiDungs.Add(staff);
        db.YeuCauXemPhongs.Add(request);
        await db.SaveChangesAsync();
        try
        {
            var store = new ViewingAdminRequestStore(db);
            var result = await store.UpdateAsync(staff.NguoiDungId, request.YeuCauXemPhongId,
                action, now.AddDays(1), "Phòng đã có người thuê.", now);
            Assert.Equal(succeeds ? ViewingAdminError.None : ViewingAdminError.InvalidState, result);
            db.ChangeTracker.Clear();
            var saved = await db.YeuCauXemPhongs.SingleAsync(x => x.YeuCauXemPhongId == request.YeuCauXemPhongId);
            Assert.Equal(succeeds ? TrangThaiYeuCauXemPhong.DaHuy : status, saved.TrangThai);
            var history = await db.LichSuTrangThaiYeuCauXemPhongs
                .Where(x => x.YeuCauXemPhongId == request.YeuCauXemPhongId).ToListAsync();
            Assert.Equal(succeeds ? 1 : 0, history.Count);
            if (succeeds)
            {
                Assert.Equal(staff.NguoiDungId, history.Single().NguoiThucHienId);
                Assert.Equal("Phòng đã có người thuê.", history.Single().LyDo);
                Assert.Equal(ViewingAdminError.InvalidState, await store.UpdateAsync(
                    staff.NguoiDungId, saved.YeuCauXemPhongId, action, null,
                    "Phòng đã có người thuê.", now));
            }
        }
        finally
        {
            await db.LichSuTrangThaiYeuCauXemPhongs.Where(x =>
                x.YeuCauXemPhongId == request.YeuCauXemPhongId).ExecuteDeleteAsync();
            await db.YeuCauXemPhongs.Where(x => x.YeuCauXemPhongId == request.YeuCauXemPhongId).ExecuteDeleteAsync();
            await db.PhongTros.Where(x => x.PhongTroId == room.PhongTroId).ExecuteDeleteAsync();
            await db.ChiNhanhs.Where(x => x.ChiNhanhId == branch.ChiNhanhId).ExecuteDeleteAsync();
            await db.NguoiDungs.Where(x => x.NguoiDungId == staff.NguoiDungId).ExecuteDeleteAsync();
        }
    }
}
