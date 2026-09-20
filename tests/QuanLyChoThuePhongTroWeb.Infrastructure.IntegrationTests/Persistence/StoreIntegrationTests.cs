using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class StoreIntegrationTests
    {
        private readonly PostgreSqlFixture _fixture;

        public StoreIntegrationTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TransactionRollback_ShouldRevertDataChanges()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var testCode = "T" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var chiNhanh = new ChiNhanh
            {
                TenChiNhanh = "Chi Nhanh Test Transaction",
                DiaChi = "123 Test Street",
                SoDienThoai = "0900000001",
                MaChiNhanh = testCode,
                MoTa = "Mo ta test transaction"
            };

            context.ChiNhanhs.Add(chiNhanh);
            await context.SaveChangesAsync();

            var inserted = await context.ChiNhanhs.FirstOrDefaultAsync(c => c.MaChiNhanh == testCode);
            Assert.NotNull(inserted);

            // Rollback transaction
            await tx.RollbackAsync();

            // Verify in a clean context that the rolled-back data is gone
            await using var verifyContext = _fixture.CreateDbContext();
            var notFound = await verifyContext.ChiNhanhs.FirstOrDefaultAsync(c => c.MaChiNhanh == testCode);
            Assert.Null(notFound);
        }

        [Fact]
        public async Task PhongTroStore_GetDanhSachPhongTroAsync_ReturnsAccurateDto()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var testCode = "P" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var chiNhanh = new ChiNhanh
            {
                TenChiNhanh = "CN PhongTro Test",
                DiaChi = "Test address",
                SoDienThoai = "0900000002",
                MaChiNhanh = testCode,
                MoTa = "Mo ta test phong tro"
            };
            context.ChiNhanhs.Add(chiNhanh);
            await context.SaveChangesAsync();

            var phongTro = new PhongTro
            {
                ChiNhanhId = chiNhanh.ChiNhanhId,
                SoPhong = "P999_TEST",
                GiaThue = 2500000,
                DienTich = 25.5,
                SoNguoiToiDa = 3,
                TrangThai = TrangThaiPhong.Trong,
                MoTa = "Test room description"
            };
            context.PhongTros.Add(phongTro);
            await context.SaveChangesAsync();

            var store = new PhongTroStore(context);
            var results = await store.GetDanhSachPhongTroAsync();

            Assert.NotEmpty(results);
            Assert.Contains(results, p => p.SoPhong == "P999_TEST" && p.GiaThue == 2500000);

            // Always rollback to keep DB clean
            await tx.RollbackAsync();
        }
    }
}
