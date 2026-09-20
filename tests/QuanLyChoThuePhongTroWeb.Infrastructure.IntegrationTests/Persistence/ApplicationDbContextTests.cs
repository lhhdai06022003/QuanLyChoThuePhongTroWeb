using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class ApplicationDbContextTests
    {
        private readonly PostgreSqlFixture _fixture;

        public ApplicationDbContextTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Database_ShouldConnectAndUseNpgsqlProvider()
        {
            await using var context = _fixture.CreateDbContext();

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            var canConnect = await context.Database.CanConnectAsync();
            Assert.True(canConnect, "ApplicationDbContext must be able to connect to PostgreSQL test database.");
        }

        [Fact]
        public async Task DbSets_ShouldBeQueryableWithoutModelMappingErrors()
        {
            await using var context = _fixture.CreateDbContext();

            // Query empty tables without throwing EF mapping exceptions
            _ = await context.ChiNhanhs.AsNoTracking().Take(1).ToListAsync();
            _ = await context.PhongTros.AsNoTracking().Take(1).ToListAsync();
            _ = await context.HopDongs.AsNoTracking().Take(1).ToListAsync();
            _ = await context.HoaDons.AsNoTracking().Take(1).ToListAsync();
            _ = await context.NguoiThues.AsNoTracking().Take(1).ToListAsync();
            _ = await context.NguoiDungs.AsNoTracking().Take(1).ToListAsync();
            _ = await context.LichSuThanhToans.AsNoTracking().Take(1).ToListAsync();
            _ = await context.DichVus.AsNoTracking().Take(1).ToListAsync();
            _ = await context.DieuKhoanMaus.AsNoTracking().Take(1).ToListAsync();
        }
    }
}
