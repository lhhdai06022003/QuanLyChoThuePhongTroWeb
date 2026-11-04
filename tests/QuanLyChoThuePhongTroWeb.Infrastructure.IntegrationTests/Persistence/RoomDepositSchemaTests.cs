using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.PublicRooms;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

// phong_tro."TienCocPhong": mặc định 0, DB chặn số âm; điều kiện > 0 khi đăng công khai do code kiểm tra.
[Collection("PostgreSqlCollection")]
public sealed class RoomDepositSchemaTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task RoomInsertedWithoutDeposit_GetsZero()
    {
        await using var context = fixture.CreateDbContext();
        await using var tx = await context.Database.BeginTransactionAsync();
        var room = NewRoom();
        context.PhongTros.Add(room);
        await context.SaveChangesAsync();

        var stored = await context.Database.SqlQuery<decimal>(
            $"""SELECT "TienCocPhong" AS "Value" FROM phong_tro WHERE "PhongTroId" = {room.PhongTroId}""").SingleAsync();

        Assert.Equal(0m, stored);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task NegativeDeposit_IsRejectedByCheckConstraint()
    {
        await using var context = fixture.CreateDbContext();
        var room = NewRoom();
        room.TienCocPhong = -1;
        context.PhongTros.Add(room);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_PhongTro_TienCocPhong", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task SaveListing_StoresDeposit_AndRefusesPublishingWithZero()
    {
        // RoomListingStore.SaveAsync tự mở transaction nên test tự dọn dữ liệu thay vì rollback.
        await using var context = fixture.CreateDbContext();
        var room = NewRoom();
        var staff = new NguoiDung { TenDangNhap = "coc_" + room.SoPhong, MatKhauHash = "test", Role = Role.NhanVien };
        context.PhongTros.Add(room);
        context.NguoiDungs.Add(staff);
        await context.SaveChangesAsync();
        try
        {
            async Task<bool> Save(bool publish, decimal? deposit, string title = "Tin")
            {
                await using var db = fixture.CreateDbContext();
                return await new RoomListingStore(db, TimeProvider.System).SaveAsync(staff.NguoiDungId, room.ChiNhanhId,
                    new SaveRoomListing(room.PhongTroId, title, "", publish, [], deposit), [], CancellationToken.None);
            }

            Assert.False(await Save(true, 0));
            Assert.True(await Save(false, 0));
            Assert.True(await Save(true, 650000));
            // Không gửi tiền cọc thì giữ nguyên số đã lưu.
            Assert.True(await Save(true, null, "Tin mới"));

            await using var check = fixture.CreateDbContext();
            var listing = await new RoomListingStore(check, TimeProvider.System).GetAsync(room.PhongTroId, CancellationToken.None);
            Assert.Equal(650000m, listing!.DepositAmount);
            Assert.True(listing.Published);
            Assert.Equal("Tin mới", listing.Title);
        }
        finally
        {
            await using var cleanup = fixture.CreateDbContext();
            await cleanup.PhongTros.Where(p => p.PhongTroId == room.PhongTroId).ExecuteDeleteAsync();
            await cleanup.NguoiDungs.Where(u => u.NguoiDungId == staff.NguoiDungId).ExecuteDeleteAsync();
            await cleanup.ChiNhanhs.Where(b => b.ChiNhanhId == room.ChiNhanhId).ExecuteDeleteAsync();
        }
    }

    private static PhongTro NewRoom()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new PhongTro
        {
            ChiNhanh = new ChiNhanh { MaChiNhanh = TestCodes.MaChiNhanh("D"), TenChiNhanh = "Coc " + suffix,
                DiaChi = "A", MoTa = "A", SoDienThoai = "0900000001" },
            SoPhong = "D" + suffix, GiaThue = 1000000m, DienTich = 20, MoTa = "Test",
            TrangThai = TrangThaiPhong.Trong
        };
    }
}
