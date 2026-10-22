using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class EmployeeBranchAssignmentConcurrencyTests
    {
        private readonly PostgreSqlFixture _fixture;

        public EmployeeBranchAssignmentConcurrencyTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task WaitForBlockedLocksAsync(int expectedWaitingCount, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                await using var checkCtx = _fixture.CreateDbContext();
                using var cmd = checkCtx.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid = l.pid WHERE NOT l.granted AND a.datname = current_database();";
                await checkCtx.Database.OpenConnectionAsync();
                var result = await cmd.ExecuteScalarAsync();
                var count = Convert.ToInt32(result);
                if (count >= expectedWaitingCount)
                {
                    return;
                }
                await Task.Delay(25);
            }

            throw new TimeoutException($"Hết thời gian chờ {timeout.TotalSeconds}s nhưng số tiến trình bị chặn trên pg_locks không đạt, kỳ vọng >= {expectedWaitingCount}.");
        }

        private async Task<(ChiNhanh branch, NguoiDung admin, NguoiDung staff)> SeedAsync(string prefix)
        {
            await using var ctx = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..8];

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_{prefix}_{suffix}",
                MaChiNhanh = $"C{suffix}",
                DiaChi = "Dia chi test",
                SoDienThoai = "0911111111",
                MoTa = "Chi nhanh test"
            };
            ctx.ChiNhanhs.Add(branch);

            var admin = new NguoiDung
            {
                TenDangNhap = $"adm_{prefix}_{suffix}",
                MatKhauHash = "hash",
                Role = Role.Admin,
                IsActive = true
            };
            ctx.NguoiDungs.Add(admin);

            var staff = new NguoiDung
            {
                TenDangNhap = $"stf_{prefix}_{suffix}",
                MatKhauHash = "hash",
                Role = Role.NhanVien,
                IsActive = true
            };
            ctx.NguoiDungs.Add(staff);

            await ctx.SaveChangesAsync();
            return (branch, admin, staff);
        }

        private static EmployeeBranchAssignmentService CreateService(ApplicationDbContext ctx)
        {
            return new EmployeeBranchAssignmentService(
                new EmployeeBranchStore(ctx),
                new EmployeeBranchAssignmentStore(ctx),
                new EfUnitOfWork(ctx),
                NullLogger<EmployeeBranchAssignmentService>.Instance);
        }

        [Fact]
        public async Task AssignAsync_TwoConcurrentRequests_SamePair_OnlyOneRowCreated_NoException()
        {
            var (branch, admin, staff) = await SeedAsync("assign_conc");

            // Kết nối giữ khóa FOR UPDATE trên hàng nguoi_dung của nhân viên đích để buộc
            // request thứ hai phải chờ (chứng minh LockEmployeeAsync dùng FOR UPDATE).
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.NguoiDungs
                .FromSqlInterpolated($"SELECT * FROM nguoi_dung WHERE \"NguoiDungId\" = {staff.NguoiDungId} FOR UPDATE")
                .ToListAsync();

            var req = new AssignEmployeeBranchRequest { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, LyDo = "Phân công đồng thời" };

            var t1 = Task.Run(async () =>
            {
                await using var c1 = _fixture.CreateDbContext();
                await c1.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                return await CreateService(c1).AssignAsync(req, actorId: admin.NguoiDungId);
            });

            var t2 = Task.Run(async () =>
            {
                await using var c2 = _fixture.CreateDbContext();
                await c2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                return await CreateService(c2).AssignAsync(req, actorId: admin.NguoiDungId);
            });

            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(5));

            await holderTx.CommitAsync();

            var r1 = await t1;
            var r2 = await t2;

            Assert.True(r1.Success);
            Assert.True(r2.Success);

            await using var verifyCtx = _fixture.CreateDbContext();
            var rows = await verifyCtx.NhanVienChiNhanhs
                .Where(x => x.NguoiDungId == staff.NguoiDungId && x.ChiNhanhId == branch.ChiNhanhId)
                .ToListAsync();
            Assert.Single(rows);
        }

        [Fact]
        public async Task Assign_Revoke_AssignAgain_StillOnlyOneRow()
        {
            var (branch, admin, staff) = await SeedAsync("cycle");

            await using var ctx = _fixture.CreateDbContext();
            var service = CreateService(ctx);

            var assign1 = await service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, LyDo = "Lần 1" }, admin.NguoiDungId);
            Assert.True(assign1.Success);

            var revoke = await service.RevokeAsync(new RevokeEmployeeBranchRequest { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, LyDo = "Thu hồi" }, admin.NguoiDungId);
            Assert.True(revoke.Success);

            var assign2 = await service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, LyDo = "Lần 2" }, admin.NguoiDungId);
            Assert.True(assign2.Success);

            var rows = await ctx.NhanVienChiNhanhs
                .Where(x => x.NguoiDungId == staff.NguoiDungId && x.ChiNhanhId == branch.ChiNhanhId)
                .ToListAsync();
            Assert.Single(rows);
            Assert.True(rows[0].IsActive);
            Assert.Null(rows[0].NgayThuHoi);
            Assert.Equal("Lần 2", rows[0].LyDo);
        }

        [Fact]
        public async Task GetEmployeesAsync_FilterByBranch_ReturnsOnlyEmployeesActiveAtThatBranch()
        {
            var (branch1, admin, staff1) = await SeedAsync("filter1");
            var (branch2, _, staff2) = await SeedAsync("filter2");

            await using var ctx = _fixture.CreateDbContext();
            var service = CreateService(ctx);

            var r1 = await service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = staff1.NguoiDungId, ChiNhanhId = branch1.ChiNhanhId }, admin.NguoiDungId);
            Assert.True(r1.Success);
            var r2 = await service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = staff2.NguoiDungId, ChiNhanhId = branch2.ChiNhanhId }, admin.NguoiDungId);
            Assert.True(r2.Success);

            var filtered = await service.GetEmployeesAsync(branch1.ChiNhanhId, admin.NguoiDungId);
            Assert.True(filtered.Success);
            Assert.Contains(filtered.Data!, e => e.NguoiDungId == staff1.NguoiDungId);
            Assert.DoesNotContain(filtered.Data!, e => e.NguoiDungId == staff2.NguoiDungId);
        }

        [Fact]
        public async Task GetEmployeeDetailAsync_IncludesDeletedBranch_WithExistingAssignment()
        {
            var (branch, admin, staff) = await SeedAsync("deleted_branch");

            await using var ctx = _fixture.CreateDbContext();
            var service = CreateService(ctx);

            var assignResult = await service.AssignAsync(new AssignEmployeeBranchRequest { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, LyDo = "Trước khi xóa chi nhánh" }, admin.NguoiDungId);
            Assert.True(assignResult.Success);

            // Xóa mềm chi nhánh sau khi đã phân công
            var branchEntity = await ctx.ChiNhanhs.FirstAsync(c => c.ChiNhanhId == branch.ChiNhanhId);
            branchEntity.IsDeleted = true;
            await ctx.SaveChangesAsync();

            var detail = await service.GetEmployeeAsync(staff.NguoiDungId, admin.NguoiDungId);
            Assert.True(detail.Success);
            var row = Assert.Single(detail.Data!.ChiNhanhs, r => r.ChiNhanhId == branch.ChiNhanhId);
            Assert.True(row.ChiNhanhDaXoa);
            Assert.Equal(EmployeeBranchState.DangPhuTrach, row.TrangThai);
        }

        [Fact]
        public async Task GetBillableIncidents_EndOfMonthEdgeCase_HalfOpenBoundary()
        {
            await using var ctx = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "d", SoDienThoai = "0900000000", MoTa = "m" };
            ctx.ChiNhanhs.Add(branch);
            await ctx.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P{suffix}", GiaThue = 1000000m, DienTich = 20, MoTa = "m" };
            ctx.PhongTros.Add(room);
            var tenant = new NguoiThue { HoVaTen = $"KH{suffix}", SoDienThoai = "0911111111", Email = $"kh_{suffix}@test.com", CCCD = $"0999{suffix}" };
            ctx.NguoiThues.Add(tenant);
            await ctx.SaveChangesAsync();

            // Khung tháng 9/2026 theo giờ VN: startUtc = 31/08 17:00 UTC; nextMonthStartUtc = 30/09 17:00 UTC (01/10 00:00 VN)
            var startUtc = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc);
            var nextMonthStartUtc = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);

            // 23:59:59.5 giờ VN ngày 30/09 = 16:59:59.5 UTC -> vẫn thuộc tháng 9
            var scEndOfMonth = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Sát nửa đêm cuối tháng 9",
                MoTa = "m",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 111000m,
                NgayXuLy = new DateTime(2026, 9, 30, 16, 59, 59, 500, DateTimeKind.Utc),
                IsDeleted = false
            };

            // 00:00:00 giờ VN ngày 01/10 = 17:00:00 UTC 30/09 -> thuộc tháng 10, bị loại
            var scNextMonth = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Đầu tháng 10",
                MoTa = "m",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 222000m,
                NgayXuLy = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            };

            ctx.YeuCauSuCos.AddRange(scEndOfMonth, scNextMonth);
            await ctx.SaveChangesAsync();

            IInvoiceIssuanceStore store = new InvoiceIssuanceStore(ctx);
            var results = await store.GetBillableIncidentsInPeriodAsync(new[] { room.PhongTroId }, startUtc, nextMonthStartUtc);

            Assert.Single(results);
            Assert.Equal("Sát nửa đêm cuối tháng 9", results[0].TieuDe);
        }
    }
}
