using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class AutoInvoiceSchemaTests
    {
        private readonly PostgreSqlFixture _fixture;

        public AutoInvoiceSchemaTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private static ChiNhanh NewBranch(string suffix, int soGio) => new()
        {
            TenChiNhanh = $"AI {suffix}",
            MaChiNhanh = $"A{suffix}",
            DiaChi = "Dia chi",
            SoDienThoai = "0900000001",
            MoTa = "Mo ta",
            SoGioPhanHoiHoaDon = soGio
        };

        [Fact]
        public async Task ChiNhanh_InsertedWithoutColumn_GetsDefault24()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO chi_nhanh ("MaChiNhanh", "TenChiNhanh", "DiaChi", "MoTa", "SoDienThoai", "NgayTao", "IsDeleted")
                VALUES ({"R" + suffix}, {"Raw " + suffix}, 'x', 'x', '0900000001', NOW(), false)
                """);

            var branch = await context.ChiNhanhs.AsNoTracking().SingleAsync(c => c.MaChiNhanh == "R" + suffix);
            Assert.Equal(24, branch.SoGioPhanHoiHoaDon);

            await tx.RollbackAsync();
        }

        // Chèn bằng SQL thô vì EF coi 0 là "chưa gán" và để cột nhận giá trị mặc định 24.
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(169)]
        public async Task ChiNhanh_OutOfRangeHours_AreRejectedByCheckConstraint(int soGio)
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO chi_nhanh ("MaChiNhanh", "TenChiNhanh", "DiaChi", "MoTa", "SoDienThoai", "NgayTao", "IsDeleted", "SoGioPhanHoiHoaDon")
                VALUES ({"R" + suffix}, {"Raw " + suffix}, 'x', 'x', '0900000001', NOW(), false, {soGio})
                """));

            Assert.Contains("CK_ChiNhanh_SoGioPhanHoiHoaDon", ex.ToString());
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(169)]
        public async Task ChiNhanh_OutOfRangeHours_ViaEntity_AreRejected(int soGio)
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            context.ChiNhanhs.Add(NewBranch(suffix, soGio));

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.Contains("CK_ChiNhanh_SoGioPhanHoiHoaDon", ex.InnerException?.Message ?? ex.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(24)]
        [InlineData(168)]
        public async Task ChiNhanh_BoundaryHours_AreSaved(int soGio)
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var branch = NewBranch(suffix, soGio);
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            context.ChangeTracker.Clear();
            var loaded = await context.ChiNhanhs.AsNoTracking().SingleAsync(c => c.ChiNhanhId == branch.ChiNhanhId);
            Assert.Equal(soGio, loaded.SoGioPhanHoiHoaDon);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HoaDon_HanPhanHoi_RoundTripsAsUtc()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(context);
            var branch = await seeder.BranchAsync("S");
            var room = await seeder.RoomAsync(branch, "S");
            var tenant = await seeder.TenantAsync("S");
            var contract = await seeder.ContractAsync(room, tenant);
            var han = new DateTime(2026, 10, 11, 3, 15, 0, DateTimeKind.Utc);

            var invoice = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon.ChuaThanhToan, 1_000_000m, null);
            invoice.HanPhanHoi = han;
            await context.SaveChangesAsync();

            context.ChangeTracker.Clear();
            var loaded = await context.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == invoice.HoaDonId);
            Assert.Equal(han, loaded.HanPhanHoi);
            Assert.Equal(DateTimeKind.Utc, loaded.HanPhanHoi!.Value.Kind);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task HoaDon_HanPhanHoi_IsNullableAndDefaultsToNull()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();
            var seeder = new DebtTestSeeder(context);
            var branch = await seeder.BranchAsync("N");
            var room = await seeder.RoomAsync(branch, "N");
            var tenant = await seeder.TenantAsync("N");
            var contract = await seeder.ContractAsync(room, tenant);

            var invoice = await seeder.InvoiceAsync(contract, TrangThaiPhatHanhHoaDon.Nhap, TrangThaiHoaDon.ChuaThanhToan, 1_000_000m, null);

            context.ChangeTracker.Clear();
            var loaded = await context.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == invoice.HoaDonId);
            Assert.Null(loaded.HanPhanHoi);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task Index_HanPhanHoi_ExistsWithFilter()
        {
            await using var context = _fixture.CreateDbContext();
            var connection = context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT indexdef
                    FROM pg_indexes
                    WHERE tablename = 'hoa_don' AND indexname = 'IX_hoa_don_TrangThaiPhatHanh_HanPhanHoi';
                    """;
                var def = (string?)await command.ExecuteScalarAsync();

                Assert.NotNull(def);
                Assert.Contains("TrangThaiPhatHanh", def);
                Assert.Contains("HanPhanHoi", def);
                Assert.Contains("\"HanPhanHoi\" IS NOT NULL", def);
                Assert.Contains("\"IsDeleted\" = false", def);
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }

        [Fact]
        public async Task CheckConstraint_ExistsInDatabase()
        {
            await using var context = _fixture.CreateDbContext();
            var connection = context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync();
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT pg_get_constraintdef(c.oid)
                    FROM pg_constraint c
                    WHERE c.conname = 'CK_ChiNhanh_SoGioPhanHoiHoaDon';
                    """;
                var def = (string?)await command.ExecuteScalarAsync();

                Assert.NotNull(def);
                Assert.Contains("SoGioPhanHoiHoaDon", def);
                Assert.Contains("168", def);
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }
}
