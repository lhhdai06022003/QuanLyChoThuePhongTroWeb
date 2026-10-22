using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests;

[Collection(WebTestCollection.Name)]
public sealed class PublicRoomDiscoveryTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task DiscoveryOrdersBeforePagingAndProtectsUnavailableRooms()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        int? branchId = null;
        try
        {
            var roomIds = new List<int>();
            int hiddenId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var branch = new ChiNhanh
                {
                    MaChiNhanh = "D" + suffix[..7], TenChiNhanh = "Discovery " + suffix,
                    DiaChi = "Test", MoTa = "Test", SoDienThoai = "0900000000"
                };
                db.ChiNhanhs.Add(branch);
                await db.SaveChangesAsync();
                branchId = branch.ChiNhanhId;
                for (var index = 0; index < 12; index++)
                {
                    var room = new PhongTro
                    {
                        ChiNhanhId = branchId.Value, SoPhong = "D" + index.ToString("00"),
                        GiaThue = (12 - index) * 1000000m, DienTich = 20 + index,
                        SoNguoiToiDa = 2, DuocDangTin = index != 11,
                        TrangThai = index == 10 ? TrangThaiPhong.DaThue : TrangThaiPhong.Trong,
                        MoTa = "Test"
                    };
                    db.PhongTros.Add(room);
                    await db.SaveChangesAsync();
                    roomIds.Add(room.PhongTroId);
                }
                hiddenId = roomIds[^1];
            }
            using var client = factory.CreateClient();
            var html = await client.GetStringAsync($"/phong?khuVuc={suffix}&soNguoi=2&sapXep=gia-tang&page=1");
            var cards = Regex.Matches(html, "data-room-card=\"([0-9]+)\"")
                .Select(match => int.Parse(match.Groups[1].Value)).ToArray();
            Assert.Equal(roomIds.Take(10).Reverse().Take(9), cards);
            Assert.Contains("sapXep=gia-tang", WebUtility.HtmlDecode(html));
            Assert.Contains("soNguoi=2", WebUtility.HtmlDecode(html));

            var ids = string.Join(',', new[] { hiddenId, roomIds[10], roomIds[0], roomIds[0], -1 });
            using var response = await client.GetAsync("/phong/du-lieu?ids=" + ids);
            response.EnsureSuccessStatusCode();
            Assert.True(response.Headers.CacheControl?.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var publicRooms = json.RootElement.GetProperty("rooms").EnumerateArray().ToArray();
            Assert.Single(publicRooms);
            Assert.Equal(roomIds[0], publicRooms[0].GetProperty("id").GetInt32());
            Assert.False(publicRooms[0].TryGetProperty("soDienThoaiChiNhanh", out _));
            Assert.Contains(hiddenId, json.RootElement.GetProperty("unavailableIds").EnumerateArray().Select(x => x.GetInt32()));
            Assert.Contains(roomIds[10], json.RootElement.GetProperty("unavailableIds").EnumerateArray().Select(x => x.GetInt32()));
            var compare = await client.GetStringAsync($"/phong/so-sanh?ids={roomIds[0]},{hiddenId}");
            Assert.Contains("D00", compare);
            Assert.DoesNotContain("Phòng D11", compare);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var room = await db.PhongTros.FindAsync(roomIds[0]);
                room!.DuocDangTin = false;
                await db.SaveChangesAsync();
            }
            using var refreshed = JsonDocument.Parse(await client.GetStringAsync($"/phong/du-lieu?ids={roomIds[0]}"));
            Assert.Empty(refreshed.RootElement.GetProperty("rooms").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/phong/da-luu")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/phong/cam-nang")).StatusCode);
        }
        finally
        {
            if (branchId.HasValue)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.PhongTros.Where(room => room.ChiNhanhId == branchId.Value).ExecuteDeleteAsync();
                await db.ChiNhanhs.Where(branch => branch.ChiNhanhId == branchId.Value).ExecuteDeleteAsync();
            }
        }
    }
}
