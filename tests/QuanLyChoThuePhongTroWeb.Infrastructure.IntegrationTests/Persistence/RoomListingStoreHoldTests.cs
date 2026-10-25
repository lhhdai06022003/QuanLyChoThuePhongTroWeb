using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

// Màn Tin phòng phải biết phòng đang bị giữ chỗ để không báo "Đang hiển thị" trong khi
// trang công khai đã ẩn phòng; hai bên dùng chung một điều kiện giữ chỗ còn hiệu lực.
[Collection("PostgreSqlCollection")]
public sealed class RoomListingStoreHoldTests(PostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(TrangThaiYeuCauGiuCho.DangGiuCho, 1, true)]
    [InlineData(TrangThaiYeuCauGiuCho.ChoXacNhanTien, -1, true)]
    [InlineData(TrangThaiYeuCauGiuCho.ChoThanhToan, 1, true)]
    [InlineData(TrangThaiYeuCauGiuCho.ChoThanhToan, -1, false)]
    [InlineData(TrangThaiYeuCauGiuCho.DaHuy, 1, false)]
    public async Task ListAsync_FlagsActiveHold_SameAsPublicListing(
        TrangThaiYeuCauGiuCho status, int paymentDueInDays, bool expectedHold)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int? branchId = null, roomId = null, guestUserId = null, guestId = null, holdId = null;
        try
        {
            await using (var db = fixture.CreateDbContext())
            {
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "L" + suffix[..7], TenChiNhanh = "Tin " + suffix,
                    DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001"
                };
                var guestUser = new NguoiDung
                {
                    TenDangNhap = "guest_lst_" + suffix,
                    MatKhauHash = "test", Role = Role.KhachVangLai
                };
                var guest = new KhachVangLai
                {
                    NguoiDung = guestUser, HoTen = "Khách test",
                    SoDienThoai = "0912345678",
                    Email = "listing" + suffix + "@example.com"
                };
                db.ChiNhanhs.Add(branch);
                db.NguoiDungs.Add(guestUser);
                db.KhachVangLais.Add(guest);
                await db.SaveChangesAsync();
                var room = new PhongTro
                {
                    ChiNhanhId = branch.ChiNhanhId, SoPhong = "L" + suffix,
                    GiaThue = 1000000m, DienTich = 20, MoTa = "Test",
                    TrangThai = TrangThaiPhong.Trong, DuocDangTin = true
                };
                db.PhongTros.Add(room);
                await db.SaveChangesAsync();
                var hold = new YeuCauGiuCho
                {
                    KhachVangLaiId = guest.KhachVangLaiId,
                    PhongTroId = room.PhongTroId,
                    SoTienGiuCho = 200000m,
                    HanThanhToan = DateTime.UtcNow.AddDays(paymentDueInDays),
                    HanKyHopDong = DateTime.UtcNow.AddDays(3),
                    TrangThai = status
                };
                db.YeuCauGiuChos.Add(hold);
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId;
                roomId = room.PhongTroId;
                guestUserId = guestUser.NguoiDungId;
                guestId = guest.KhachVangLaiId;
                holdId = hold.YeuCauGiuChoId;
            }

            await using (var db = fixture.CreateDbContext())
            {
                var listing = Assert.Single(await new RoomListingStore(db, TimeProvider.System)
                    .ListAsync(new[] { branchId.Value }, CancellationToken.None));
                Assert.Equal(expectedHold, listing.HasActiveHold);

                var publicRoom = await new PublicRoomStore(db, TimeProvider.System)
                    .GetAvailableAsync(roomId.Value);
                Assert.Equal(expectedHold, publicRoom is null);
            }
        }
        finally
        {
            await using var db = fixture.CreateDbContext();
            if (holdId.HasValue)
                await db.YeuCauGiuChos.Where(h => h.YeuCauGiuChoId == holdId.Value)
                    .ExecuteDeleteAsync();
            if (roomId.HasValue)
                await db.PhongTros.Where(r => r.PhongTroId == roomId.Value)
                    .ExecuteDeleteAsync();
            if (guestId.HasValue)
                await db.KhachVangLais.Where(g => g.KhachVangLaiId == guestId.Value)
                    .ExecuteDeleteAsync();
            if (guestUserId.HasValue)
                await db.NguoiDungs.Where(u => u.NguoiDungId == guestUserId.Value)
                    .ExecuteDeleteAsync();
            if (branchId.HasValue)
                await db.ChiNhanhs.Where(b => b.ChiNhanhId == branchId.Value)
                    .ExecuteDeleteAsync();
        }
    }
}
