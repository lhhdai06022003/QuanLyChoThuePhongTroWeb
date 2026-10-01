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
    public class EmployeeBranchStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public EmployeeBranchStoreTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task EmployeeBranchStore_QueriesActiveBranchAssignments_Correctly()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);

            // Create admin user for assignment tracking
            var adminUser = new NguoiDung
            {
                TenDangNhap = $"admin_{uniqueSuffix}",
                MatKhauHash = "hash123",
                Role = Role.Admin,
                IsActive = true
            };
            context.NguoiDungs.Add(adminUser);
            await context.SaveChangesAsync();

            // Create 2 branches
            var branch1 = new ChiNhanh
            {
                TenChiNhanh = $"CN 1 {uniqueSuffix}",
                MaChiNhanh = $"C1{uniqueSuffix.Substring(0, 4)}",
                DiaChi = "123 Street 1",
                SoDienThoai = "0900000001",
                MoTa = "Mo ta CN 1"
            };
            var branch2 = new ChiNhanh
            {
                TenChiNhanh = $"CN 2 {uniqueSuffix}",
                MaChiNhanh = $"C2{uniqueSuffix.Substring(0, 4)}",
                DiaChi = "456 Street 2",
                SoDienThoai = "0900000002",
                MoTa = "Mo ta CN 2"
            };
            context.ChiNhanhs.AddRange(branch1, branch2);
            await context.SaveChangesAsync();

            // Create employee user
            var employee = new NguoiDung
            {
                TenDangNhap = $"staff_{uniqueSuffix}",
                MatKhauHash = "hash456",
                Role = Role.NhanVien,
                IsActive = true
            };
            context.NguoiDungs.Add(employee);
            await context.SaveChangesAsync();

            // Assign employee to branch1 (active) and branch2 (revoked)
            var activeAssignment = new NhanVienChiNhanh
            {
                NguoiDungId = employee.NguoiDungId,
                ChiNhanhId = branch1.ChiNhanhId,
                NguoiPhanCongId = adminUser.NguoiDungId,
                IsActive = true,
                NgayPhanCong = DateTime.UtcNow
            };
            var revokedAssignment = new NhanVienChiNhanh
            {
                NguoiDungId = employee.NguoiDungId,
                ChiNhanhId = branch2.ChiNhanhId,
                NguoiPhanCongId = adminUser.NguoiDungId,
                IsActive = false,
                NgayPhanCong = DateTime.UtcNow.AddMonths(-1),
                NgayThuHoi = DateTime.UtcNow.AddDays(-2),
                NguoiThuHoiId = adminUser.NguoiDungId,
                LyDo = "Thu hồi phân công chi nhánh 2"
            };
            context.NhanVienChiNhanhs.AddRange(activeAssignment, revokedAssignment);
            await context.SaveChangesAsync();

            // Test store queries
            var store = new EmployeeBranchStore(context);

            var actorDto = await store.GetActorWithActiveBranchesAsync(employee.NguoiDungId);
            Assert.NotNull(actorDto);
            Assert.Equal(employee.NguoiDungId, actorDto.NguoiDungId);
            Assert.Equal(Role.NhanVien, actorDto.Role);
            Assert.True(actorDto.IsActive);
            Assert.Contains(branch1.ChiNhanhId, actorDto.ActiveBranchIds);
            Assert.DoesNotContain(branch2.ChiNhanhId, actorDto.ActiveBranchIds);

            // Test HasActiveBranchAssignmentAsync: Branch 1 is active, Branch 2 is revoked
            var hasBranch1 = await store.HasActiveBranchAssignmentAsync(employee.NguoiDungId, branch1.ChiNhanhId);
            var hasBranch2 = await store.HasActiveBranchAssignmentAsync(employee.NguoiDungId, branch2.ChiNhanhId);
            var hasBranch999 = await store.HasActiveBranchAssignmentAsync(employee.NguoiDungId, 99999);

            Assert.True(hasBranch1);
            Assert.False(hasBranch2);
            Assert.False(hasBranch999);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task EmployeeBranchStore_InactiveOrDeletedUser_ReturnsNull()
        {
            await using var context = _fixture.CreateDbContext();
            await using var tx = await context.Database.BeginTransactionAsync();

            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);

            var inactiveUser = new NguoiDung
            {
                TenDangNhap = $"inact_{uniqueSuffix}",
                MatKhauHash = "hash789",
                Role = Role.NhanVien,
                IsActive = false
            };
            var deletedUser = new NguoiDung
            {
                TenDangNhap = $"del_{uniqueSuffix}",
                MatKhauHash = "hash789",
                Role = Role.NhanVien,
                IsActive = true,
                IsDeleted = true
            };
            context.NguoiDungs.AddRange(inactiveUser, deletedUser);
            await context.SaveChangesAsync();

            var store = new EmployeeBranchStore(context);

            var inactiveActor = await store.GetActorWithActiveBranchesAsync(inactiveUser.NguoiDungId);
            var deletedActor = await store.GetActorWithActiveBranchesAsync(deletedUser.NguoiDungId);

            Assert.Null(inactiveActor);
            Assert.Null(deletedActor);

            await tx.RollbackAsync();
        }
    }
}
