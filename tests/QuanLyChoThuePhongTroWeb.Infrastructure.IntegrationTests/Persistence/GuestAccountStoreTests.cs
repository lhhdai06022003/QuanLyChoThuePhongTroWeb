using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.GuestAccounts;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

[Collection("PostgreSqlCollection")]
public sealed class GuestAccountStoreTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task TryCreate_PersistsOneProfile_AndRejectsDuplicateUsername()
    {
        var username = "guest_" + Guid.NewGuid().ToString("N")[..12];
        int? userId = null;
        try
        {
            await using (var context = fixture.CreateDbContext())
            {
                var store = new GuestAccountStore(context);
                userId = await store.TryCreateAsync(CreateUser(username), CreateProfile());
            }

            Assert.NotNull(userId);
            await using (var context = fixture.CreateDbContext())
            {
                var duplicate = await new GuestAccountStore(context)
                    .TryCreateAsync(CreateUser(username.ToUpperInvariant()), CreateProfile());
                Assert.Null(duplicate);
            }

            await using (var context = fixture.CreateDbContext())
            {
                Assert.Equal(1, await context.NguoiDungs.CountAsync(u =>
                    u.TenDangNhap.ToUpper() == username.ToUpperInvariant()));
                Assert.Equal(1, await context.KhachVangLais.CountAsync(g =>
                    g.NguoiDungId == userId));
            }
        }
        finally
        {
            if (userId.HasValue)
            {
                await using var cleanup = fixture.CreateDbContext();
                await cleanup.KhachVangLais.Where(g => g.NguoiDungId == userId)
                    .ExecuteDeleteAsync();
                await cleanup.NguoiDungs.Where(u => u.NguoiDungId == userId)
                    .ExecuteDeleteAsync();
            }
        }
    }

    private static NguoiDung CreateUser(string username) => new()
    {
        TenDangNhap = username,
        MatKhauHash = "test-hash",
        Role = Role.KhachVangLai,
        IsActive = true
    };

    private static KhachVangLai CreateProfile() => new()
    {
        HoTen = "Khách thử",
        SoDienThoai = "0912345678",
        Email = "guest@example.com",
        EmailNormalized = "GUEST@EXAMPLE.COM"
    };
}
