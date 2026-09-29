using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

public class PublicRoomQueryTests
{
    [Fact]
    public void FilterPublicRooms_ExcludesHeldOccupiedUnpublishedAndDeletedRooms()
    {
        var branch = new ChiNhanh { ChiNhanhId = 1, TenChiNhanh = "A", IsDeleted = false };
        var rooms = new[]
        {
            Room(1, branch),
            Room(2, branch, published: false),
            Room(3, branch, status: TrangThaiPhong.DaThue),
            Room(4, branch, status: TrangThaiPhong.BaoTri),
            Room(5, branch, deleted: true),
            Room(6, branch)
        }.AsQueryable();
        var holds = new[]
        {
            new YeuCauGiuCho { PhongTroId = 6, TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan }
        }.AsQueryable();

        var result = PhongCongKhaiStore.FilterPublicRooms(rooms, holds).Select(room => room.PhongTroId).ToList();

        Assert.Equal(new[] { 1 }, result);
    }

    private static PhongTro Room(int id, ChiNhanh branch, bool published = true,
        TrangThaiPhong status = TrangThaiPhong.Trong, bool deleted = false) => new()
    {
        PhongTroId = id,
        ChiNhanhId = branch.ChiNhanhId,
        ChiNhanh = branch,
        DuocDangTin = published,
        MaCongKhai = $"P{id}",
        TrangThai = status,
        IsDeleted = deleted
    };
}
