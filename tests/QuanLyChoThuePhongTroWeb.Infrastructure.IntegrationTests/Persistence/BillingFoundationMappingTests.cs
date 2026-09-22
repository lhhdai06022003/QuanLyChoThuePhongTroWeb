using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class BillingFoundationMappingTests
    {
        private readonly PostgreSqlFixture _fixture;
        private readonly ApplicationDbContext _context;

        public BillingFoundationMappingTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
            _context = fixture.CreateDbContext();
        }

        [Fact]
        public void Model_ShouldContainExactly37BusinessEntities()
        {
            var entityTypes = _context.Model.GetEntityTypes()
                .Where(e => !e.IsOwned())
                .ToList();

            Assert.Equal(37, entityTypes.Count);
            Assert.DoesNotContain(entityTypes, e => e.GetTableName() == "ket_qua_nhan_dang_chi_so");
            Assert.DoesNotContain(entityTypes, e => e.GetTableName() == "lich_su_dieu_chinh_chi_so");
        }

        [Theory]
        [InlineData(typeof(AnhChiSoDongHo))]
        [InlineData(typeof(LichSuTrangThaiHoaDon))]
        [InlineData(typeof(YeuCauThanhToanHoaDon))]
        [InlineData(typeof(MinhChungThanhToanHoaDon))]
        [InlineData(typeof(LichSuTrangThaiYeuCauThanhToanHoaDon))]
        public void Model_ShouldContainAllFiveNewEntities(Type entityType)
        {
            var entity = _context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);
        }

        [Theory]
        [InlineData(typeof(AnhChiSoDongHo), nameof(AnhChiSoDongHo.GiaTriAIGoiY), 18, 3)]
        [InlineData(typeof(AnhChiSoDongHo), nameof(AnhChiSoDongHo.GiaTriXacNhan), 18, 3)]
        [InlineData(typeof(HoaDon), nameof(HoaDon.SoTienThanhToanToiThieu), 18, 2)]
        [InlineData(typeof(YeuCauThanhToanHoaDon), nameof(YeuCauThanhToanHoaDon.SoTien), 18, 2)]
        [InlineData(typeof(MinhChungThanhToanHoaDon), nameof(MinhChungThanhToanHoaDon.SoTienKhaiBao), 18, 2)]
        public void Property_ShouldHaveExpectedPrecisionAndScale(Type entityType, string propertyName, int expectedPrecision, int expectedScale)
        {
            var entity = _context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);

            var property = entity.FindProperty(propertyName);
            Assert.NotNull(property);

            Assert.Equal(expectedPrecision, property.GetPrecision());
            Assert.Equal(expectedScale, property.GetScale());
        }

        [Fact]
        public void AnhChiSoDongHo_ShouldAllowOnlyOneOfficialImagePerMeterType()
        {
            var entity = _context.Model.FindEntityType(typeof(AnhChiSoDongHo));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().SingleOrDefault(i =>
                i.Properties.Select(p => p.Name).SequenceEqual(new[]
                {
                    nameof(AnhChiSoDongHo.DichVuDienNuocCuaPhongId),
                    nameof(AnhChiSoDongHo.LoaiDongHo)
                }));

            Assert.NotNull(index);
            Assert.True(index.IsUnique);
            Assert.Contains(nameof(AnhChiSoDongHo.DuocChonLamChiSoChinhThuc), index.GetFilter());
        }

        [Fact]
        public void DichVuDienNuocCuaPhong_ShouldHaveFilteredUniqueIndex()
        {
            var entity = _context.Model.FindEntityType(typeof(DichVuDienNuocCuaPhong));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(DichVuDienNuocCuaPhong.PhongTroId)) &&
                i.Properties.Any(p => p.Name == nameof(DichVuDienNuocCuaPhong.Thang)) &&
                i.Properties.Any(p => p.Name == nameof(DichVuDienNuocCuaPhong.Nam)));

            Assert.NotNull(index);
            Assert.True(index.IsUnique);
            Assert.NotNull(index.GetFilter());
        }

        [Fact]
        public async Task AppliedDatabase_ShouldContainExactly37BusinessTables()
        {
            var connectionString = Environment.GetEnvironmentVariable("QLCTPT_TEST_CONNECTION_STRING");
            Assert.False(string.IsNullOrWhiteSpace(connectionString));

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name <> '__EFMigrationsHistory';
                """;
            var tableCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            Assert.Equal(37, tableCount);

            await using var removedTablesCommand = connection.CreateCommand();
            removedTablesCommand.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name IN ('ket_qua_nhan_dang_chi_so', 'lich_su_dieu_chinh_chi_so');
                """;
            var removedTableCount = Convert.ToInt32(await removedTablesCommand.ExecuteScalarAsync());
            Assert.Equal(0, removedTableCount);
        }

        [Fact]
        public void LichSuThanhToan_MinhChungThanhToanHoaDonId_ShouldHaveFilteredUniqueIndex()
        {
            var entity = _context.Model.FindEntityType(typeof(LichSuThanhToan));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(LichSuThanhToan.MinhChungThanhToanHoaDonId)));

            Assert.NotNull(index);
            Assert.True(index.IsUnique);
        }
    }
}
