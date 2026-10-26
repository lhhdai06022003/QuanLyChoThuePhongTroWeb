using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Spec giữ chỗ §7, điểm S1 (migration AllowSystemReservationTransaction): dòng giao dịch giữ chỗ do SePay/VNPay tự ghi
    // không có người xác nhận, nên cột được để trống; khóa ngoại tới nguoi_dung (Restrict) và index vẫn giữ.
    [Collection("PostgreSqlCollection")]
    public class ReservationSystemTransactionSchemaTests
    {
        private readonly PostgreSqlFixture _fixture;
        private readonly ApplicationDbContext _context;

        public ReservationSystemTransactionSchemaTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
            _context = fixture.CreateDbContext();
        }

        [Fact]
        public void Model_NguoiXacNhan_IsOptional_AndStillRestrict()
        {
            var entity = _context.Model.FindEntityType(typeof(GiaoDichGiuCho));
            Assert.NotNull(entity);

            var prop = entity.FindProperty(nameof(GiaoDichGiuCho.NguoiXacNhanId));
            Assert.NotNull(prop);
            Assert.True(prop.IsNullable);

            var fk = entity.GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == nameof(GiaoDichGiuCho.NguoiXacNhanId)));
            Assert.False(fk.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
            Assert.Equal(typeof(NguoiDung), fk.PrincipalEntityType.ClrType);
        }

        [Fact]
        public async Task Database_NguoiXacNhanColumn_IsNullable_WithRestrictForeignKeyAndIndex()
        {
            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();

            await using (var column = connection.CreateCommand())
            {
                column.CommandText = """
                    SELECT is_nullable, column_default
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'giao_dich_giu_cho' AND column_name = 'NguoiXacNhanId';
                    """;
                await using var reader = await column.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("YES", reader.GetString(0));
                Assert.True(reader.IsDBNull(1));
            }

            await using (var foreignKey = connection.CreateCommand())
            {
                // confdeltype 'r' = ON DELETE RESTRICT.
                foreignKey.CommandText = """
                    SELECT referenced.relname, c.confdeltype::text
                    FROM pg_constraint c
                    JOIN pg_class owner ON owner.oid = c.conrelid
                    JOIN pg_class referenced ON referenced.oid = c.confrelid
                    JOIN pg_attribute a ON a.attrelid = owner.oid AND a.attnum = ANY (c.conkey)
                    WHERE c.contype = 'f' AND owner.relname = 'giao_dich_giu_cho' AND a.attname = 'NguoiXacNhanId';
                    """;
                await using var reader = await foreignKey.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal("nguoi_dung", reader.GetString(0));
                Assert.Equal("r", reader.GetString(1));
                Assert.False(await reader.ReadAsync());
            }

            await using (var index = connection.CreateCommand())
            {
                index.CommandText = """
                    SELECT COUNT(*)
                    FROM pg_indexes
                    WHERE schemaname = 'public' AND tablename = 'giao_dich_giu_cho' AND indexdef LIKE '%("NguoiXacNhanId")%';
                    """;
                Assert.Equal(1L, (long)(await index.ExecuteScalarAsync())!);
            }
        }
    }
}
