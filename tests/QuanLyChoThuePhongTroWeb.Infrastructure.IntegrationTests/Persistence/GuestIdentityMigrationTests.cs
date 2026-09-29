using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence;

public class GuestIdentityMigrationTests
{
    [Fact]
    public void Migration_IsDiscoverableWithoutConnectingToDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=qlctpt_never_connect;Username=unused;Password=unused")
            .Options;
        using var context = new ApplicationDbContext(options);

        Assert.Contains("20260925075200_AddGuestIdentityForReservations", context.Database.GetMigrations());
    }
}
