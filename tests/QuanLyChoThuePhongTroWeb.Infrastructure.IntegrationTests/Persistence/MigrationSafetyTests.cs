using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class MigrationSafetyTests
    {
        private readonly PostgreSqlFixture _fixture;

        public MigrationSafetyTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task AllMigrations_ShouldBeAppliedWithoutPendingChanges()
        {
            await using var context = _fixture.CreateDbContext();

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            Assert.Empty(pendingMigrations);

            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();
            Assert.NotEmpty(appliedMigrations);

            // Verify key historical migrations exist in the database
            Assert.Contains(appliedMigrations, m => m.Contains("InitialCreate"));
            Assert.Contains(appliedMigrations, m => m.Contains("AddHopDongDieuKhoan"));
            Assert.Contains(appliedMigrations, m => m.Contains("ChangeChiTietHoaDonSoLuongToDouble"));
        }
    }
}
