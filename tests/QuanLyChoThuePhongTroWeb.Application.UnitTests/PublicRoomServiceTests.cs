using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public class PublicRoomServiceTests
{
    [Fact]
    public async Task SearchAsync_OnlyReturnsPublishedVacantRoomsWithoutActiveHold()
    {
        var store = new FakeStore(
            Room(1, "P101"),
            Room(2, "P102", published: false),
            Room(3, "P103", roomDeleted: true),
            Room(4, "P104", branchDeleted: true),
            Room(5, "P105", vacant: false),
            Room(6, "P106", hasActiveHold: true));

        var result = await new PhongCongKhaiService(store).SearchAsync(new PublicRoomQuery());

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("P101", Assert.Single(result.Items).MaCongKhai);
    }

    [Fact]
    public async Task SearchAsync_AppliesFiltersAndBoundsPageSize()
    {
        var store = new FakeStore(Room(1, "P101", rent: 2500000m, branchId: 7),
            Room(2, "P102", rent: 3500000m, branchId: 7),
            Room(3, "P103", rent: 2500000m, branchId: 8));

        var result = await new PhongCongKhaiService(store).SearchAsync(
            new PublicRoomQuery(7, 2000000m, 3000000m, null, -5, 500));

        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
        Assert.Equal("P101", Assert.Single(result.Items).MaCongKhai);
    }

    [Fact]
    public async Task GetAsync_DoesNotRevealHiddenRoomByKnownCode()
    {
        var store = new FakeStore(Room(1, "PUBLIC"), Room(2, "HIDDEN", published: false));
        var service = new PhongCongKhaiService(store);

        Assert.Null(await service.GetAsync("HIDDEN"));
        Assert.Equal("PUBLIC", (await service.GetAsync("PUBLIC"))?.MaCongKhai);
    }

    private static PublicRoomRecord Room(int id, string code, bool published = true,
        bool roomDeleted = false, bool branchDeleted = false, bool vacant = true,
        bool hasActiveHold = false, decimal rent = 2500000m, int branchId = 7) => new()
    {
        PhongTroId = id,
        ChiNhanhId = branchId,
        MaCongKhai = code,
        TieuDe = $"Phòng {code}",
        SoPhong = code,
        TenChiNhanh = "Chi nhánh 1",
        DiaChi = "123 Đường A",
        SoDienThoaiLienHe = "0900000000",
        GiaThue = rent,
        DienTich = 20,
        SoNguoiToiDa = 2,
        MoTa = "Phòng sáng, thoáng",
        DuocDangTin = published,
        PhongDaXoa = roomDeleted,
        ChiNhanhDaXoa = branchDeleted,
        PhongTrong = vacant,
        CoGiuChoHoatDong = hasActiveHold
    };

    private sealed class FakeStore(params PublicRoomRecord[] rooms) : IPhongCongKhaiStore
    {
        public Task<IReadOnlyList<PublicRoomRecord>> GetCandidatesAsync() =>
            Task.FromResult<IReadOnlyList<PublicRoomRecord>>(rooms);

        public Task<PublicRoomRecord?> GetByCodeAsync(string code) =>
            Task.FromResult(rooms.FirstOrDefault(room => room.MaCongKhai == code));
    }
}
