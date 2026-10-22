using System;
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
    [Collection("PostgreSqlCollection")]
    public class PublicReservationFoundationMappingTests
    {
        private readonly PostgreSqlFixture _fixture;
        private readonly ApplicationDbContext _context;

        public PublicReservationFoundationMappingTests(PostgreSqlFixture fixture)
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
        }

        [Theory]
        [InlineData(typeof(AnhPhongTro))]
        [InlineData(typeof(NhanVienChiNhanh))]
        [InlineData(typeof(KhachVangLai))]
        [InlineData(typeof(KhungGioXemPhong))]
        [InlineData(typeof(YeuCauXemPhong))]
        [InlineData(typeof(LichSuTrangThaiYeuCauXemPhong))]
        [InlineData(typeof(YeuCauGiuCho))]
        [InlineData(typeof(LichSuTrangThaiYeuCauGiuCho))]
        [InlineData(typeof(YeuCauThanhToanGiuCho))]
        [InlineData(typeof(MinhChungThanhToanGiuCho))]
        [InlineData(typeof(GiaoDichGiuCho))]
        [InlineData(typeof(LichSuTrangThaiYeuCauThanhToanGiuCho))]
        [InlineData(typeof(ApDungTienGiuChoVaoTienCoc))]
        [InlineData(typeof(QuyetDinhHoanTienGiuCho))]
        [InlineData(typeof(GiaoDichHoanTienGiuCho))]
        public void Model_ShouldContainAllFifteenNewEntities(Type entityType)
        {
            var entity = _context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);
        }

        [Fact]
        public void PhongTro_HasApprovedPublicationConfiguration()
        {
            var entity = _context.Model.FindEntityType(typeof(PhongTro));
            Assert.NotNull(entity);

            var duocDangTinProp = entity.FindProperty(nameof(PhongTro.DuocDangTin));
            Assert.NotNull(duocDangTinProp);
            Assert.Equal(false, duocDangTinProp.GetDefaultValue());

            Assert.Null(entity.FindProperty("NgayDangTin"));
            Assert.Null(entity.FindProperty("NgayNgungDang"));

            var maCongKhaiIndex = entity.GetIndexes()
                .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(PhongTro.MaCongKhai)));
            Assert.NotNull(maCongKhaiIndex);
            Assert.True(maCongKhaiIndex.IsUnique);
            Assert.Contains("MaCongKhai", maCongKhaiIndex.GetFilter());
        }

        [Fact]
        public void AnhPhongTro_HasUniqueFilteredCoverImageIndex()
        {
            var entity = _context.Model.FindEntityType(typeof(AnhPhongTro));
            Assert.NotNull(entity);

            var index = entity.GetIndexes()
                .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(AnhPhongTro.PhongTroId));
            Assert.NotNull(index);
            Assert.True(index.IsUnique);
            Assert.Contains("LaAnhDaiDien", index.GetFilter());
            Assert.Contains("IsActive", index.GetFilter());
        }

        [Fact]
        public void NhanVienChiNhanh_HasUniqueAssignmentIndex_WithoutFilter()
        {
            var entity = _context.Model.FindEntityType(typeof(NhanVienChiNhanh));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(NhanVienChiNhanh.NguoiDungId)) &&
                i.Properties.Any(p => p.Name == nameof(NhanVienChiNhanh.ChiNhanhId)));

            Assert.NotNull(index);
            Assert.True(index.IsUnique);
            Assert.Null(index.GetFilter());
        }

        [Fact]
        public async Task KhungGioXemPhong_HasTimeAndCapacityCheckConstraints()
        {
            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT conname
                FROM pg_constraint c
                JOIN pg_namespace n ON n.oid = c.connamespace
                WHERE contype = 'c' AND n.nspname = 'public'
                  AND conname IN ('CK_KhungGioXemPhong_ThoiGian', 'CK_KhungGioXemPhong_SoLuongToiDa');
                """;

            var found = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                found.Add(reader.GetString(0));
            }

            Assert.Contains("CK_KhungGioXemPhong_ThoiGian", found);
            Assert.Contains("CK_KhungGioXemPhong_SoLuongToiDa", found);
        }

        [Fact]
        public async Task YeuCauGiuCho_HasNullableDepositAndCheckConstraint()
        {
            var entity = _context.Model.FindEntityType(typeof(YeuCauGiuCho));
            Assert.NotNull(entity);

            var prop = entity.FindProperty(nameof(YeuCauGiuCho.SoTienGiuCho));
            Assert.NotNull(prop);
            Assert.True(prop.IsNullable);

            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT conname
                FROM pg_constraint c
                JOIN pg_namespace n ON n.oid = c.connamespace
                WHERE contype = 'c' AND n.nspname = 'public'
                  AND conname IN ('CK_YeuCauGiuCho_SoTienGiuCho', 'CK_YeuCauGiuCho_SoTienTheoTrangThai');
                """;

            var found = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                found.Add(reader.GetString(0));
            }

            Assert.Contains("CK_YeuCauGiuCho_SoTienGiuCho", found);
            Assert.Contains("CK_YeuCauGiuCho_SoTienTheoTrangThai", found);
        }

        [Theory]
        [InlineData(typeof(LichSuTrangThaiYeuCauXemPhong))]
        [InlineData(typeof(LichSuTrangThaiYeuCauGiuCho))]
        [InlineData(typeof(YeuCauThanhToanGiuCho))]
        [InlineData(typeof(MinhChungThanhToanGiuCho))]
        [InlineData(typeof(LichSuTrangThaiYeuCauThanhToanGiuCho))]
        [InlineData(typeof(GiaoDichGiuCho))]
        [InlineData(typeof(ApDungTienGiuChoVaoTienCoc))]
        [InlineData(typeof(QuyetDinhHoanTienGiuCho))]
        [InlineData(typeof(GiaoDichHoanTienGiuCho))]
        public void AuditAndFinancialRelations_HaveRestrictDeleteBehavior(Type entityType)
        {
            var entity = _context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);

            var foreignKeys = entity.GetForeignKeys().ToList();
            Assert.NotEmpty(foreignKeys);

            foreach (var fk in foreignKeys)
            {
                Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
            }
        }

        [Fact]
        public void KhachVangLai_HasUniqueNguoiDungIndex()
        {
            var entity = _context.Model.FindEntityType(typeof(KhachVangLai));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(KhachVangLai.NguoiDungId)));

            Assert.NotNull(index);
            Assert.True(index.IsUnique);
        }

        [Fact]
        public void YeuCauGiuCho_HasUniqueActiveReservationPerRoomIndex()
        {
            var entity = _context.Model.FindEntityType(typeof(YeuCauGiuCho));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Count == 1 && i.Properties[0].Name == nameof(YeuCauGiuCho.PhongTroId));

            Assert.NotNull(index);
            Assert.True(index.IsUnique);
            Assert.Contains("TrangThai", index.GetFilter());
        }

        [Fact]
        public void GiaoDichGiuCho_HasUniqueTransactionCode_And_UniqueEvidence()
        {
            var entity = _context.Model.FindEntityType(typeof(GiaoDichGiuCho));
            Assert.NotNull(entity);

            var codeIndex = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(GiaoDichGiuCho.MaGiaoDich)));
            Assert.NotNull(codeIndex);
            Assert.True(codeIndex.IsUnique);

            var evidenceIndex = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(GiaoDichGiuCho.MinhChungThanhToanGiuChoId)));
            Assert.NotNull(evidenceIndex);
            Assert.True(evidenceIndex.IsUnique);
        }

        [Fact]
        public void ApDungTienGiuChoVaoTienCoc_HasUniqueReservationIndex()
        {
            var entity = _context.Model.FindEntityType(typeof(ApDungTienGiuChoVaoTienCoc));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(ApDungTienGiuChoVaoTienCoc.YeuCauGiuChoId)));
            Assert.NotNull(index);
            Assert.True(index.IsUnique);
        }

        [Fact]
        public void QuyetDinhHoanTienGiuCho_HasUniqueReservationIndex()
        {
            var entity = _context.Model.FindEntityType(typeof(QuyetDinhHoanTienGiuCho));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(QuyetDinhHoanTienGiuCho.YeuCauGiuChoId)));
            Assert.NotNull(index);
            Assert.True(index.IsUnique);
        }

        [Fact]
        public void GiaoDichHoanTienGiuCho_HasUniqueTransactionCode()
        {
            var entity = _context.Model.FindEntityType(typeof(GiaoDichHoanTienGiuCho));
            Assert.NotNull(entity);

            var index = entity.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(GiaoDichHoanTienGiuCho.MaGiaoDichHoan)));
            Assert.NotNull(index);
            Assert.True(index.IsUnique);
        }

        [Fact]
        public async Task AppliedDatabase_ShouldContainExactly37BusinessTables_And_ExpectedConstraints()
        {
            var connectionString = _fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString));

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Table count
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

            // 2. All 15 new tables exist
            var expected15Tables = new[]
            {
                "anh_phong_tro",
                "nhan_vien_chi_nhanh",
                "khach_vang_lai",
                "khung_gio_xem_phong",
                "yeu_cau_xem_phong",
                "lich_su_trang_thai_yeu_cau_xem_phong",
                "yeu_cau_giu_cho",
                "lich_su_trang_thai_yeu_cau_giu_cho",
                "yeu_cau_thanh_toan_giu_cho",
                "minh_chung_thanh_toan_giu_cho",
                "giao_dich_giu_cho",
                "lich_su_trang_thai_yeu_cau_thanh_toan_giu_cho",
                "ap_dung_tien_giu_cho_vao_tien_coc",
                "quyet_dinh_hoan_tien_giu_cho",
                "giao_dich_hoan_tien_giu_cho"
            };

            await using var tablesCommand = connection.CreateCommand();
            tablesCommand.CommandText = """
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE';
                """;
            var existingTables = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await tablesCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            foreach (var table in expected15Tables)
            {
                Assert.Contains(table, existingTables);
            }

            // 3. Check constraints in pg_constraint
            await using var checkConstraintsCmd = connection.CreateCommand();
            checkConstraintsCmd.CommandText = """
                SELECT conname, pg_get_constraintdef(c.oid)
                FROM pg_constraint c
                JOIN pg_namespace n ON n.oid = c.connamespace
                WHERE contype = 'c' AND n.nspname = 'public';
                """;
            var existingChecks = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await checkConstraintsCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingChecks[reader.GetString(0)] = reader.GetString(1);
                }
            }

            Assert.True(existingChecks.TryGetValue("CK_KhungGioXemPhong_ThoiGian", out var defTime));
            Assert.Contains("ThoiGianBatDau", defTime);
            Assert.Contains("ThoiGianKetThuc", defTime);

            Assert.True(existingChecks.TryGetValue("CK_KhungGioXemPhong_SoLuongToiDa", out var defCap));
            Assert.Contains("SoLuongToiDa", defCap);
            Assert.Contains("> 0", defCap);

            Assert.True(existingChecks.TryGetValue("CK_YeuCauGiuCho_SoTienGiuCho", out var defDeposit));
            Assert.Contains("SoTienGiuCho", defDeposit);
            Assert.Contains("IS NULL", defDeposit);
            Assert.Contains(">", defDeposit);
            Assert.Contains("0", defDeposit);

            Assert.True(existingChecks.TryGetValue("CK_YeuCauGiuCho_SoTienTheoTrangThai", out var defStatus));
            Assert.Contains("TrangThai", defStatus);
            Assert.Contains("SoTienGiuCho", defStatus);
            Assert.Contains(">", defStatus);
            Assert.Contains("0", defStatus);

            // 4. Index on nhan_vien_chi_nhanh (NguoiDungId, ChiNhanhId) is unique and unfiltered
            await using var indexCmd = connection.CreateCommand();
            indexCmd.CommandText = """
                SELECT indexname, indexdef
                FROM pg_indexes
                WHERE tablename = 'nhan_vien_chi_nhanh'
                  AND indexdef LIKE '%NguoiDungId%' AND indexdef LIKE '%ChiNhanhId%';
                """;
            string? nvcnIndexDef = null;
            await using (var reader = await indexCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    nvcnIndexDef = reader.GetString(1);
                }
            }
            Assert.NotNull(nvcnIndexDef);
            Assert.Contains("UNIQUE INDEX", nvcnIndexDef);
            Assert.DoesNotContain("WHERE", nvcnIndexDef); // Unfiltered!

            // 5. PhongTro columns: DuocDangTin exists, NgayDangTin and NgayNgungDang do not
            await using var colCommand = connection.CreateCommand();
            colCommand.CommandText = """
                SELECT column_name, column_default
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'phong_tro';
                """;
            var columns = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await colCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columns[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
                }
            }

            Assert.True(columns.ContainsKey("DuocDangTin"));
            Assert.Contains("false", columns["DuocDangTin"]?.ToLowerInvariant() ?? "");
            Assert.False(columns.ContainsKey("NgayDangTin"));
            Assert.False(columns.ContainsKey("NgayNgungDang"));
        }
    }
}
