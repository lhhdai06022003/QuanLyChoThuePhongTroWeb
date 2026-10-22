using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class InvoiceDraftConcurrencyTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoiceDraftConcurrencyTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task WaitForBlockedLocksAsync(int expectedWaitingCount, TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
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

        private async Task<(ChiNhanh branch, PhongTro room, NguoiThue tenant, HopDong contract, NguoiDung admin)> CreateTestHierarchyAsync(string prefix = "test")
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];

            var branch = new ChiNhanh
            {
                TenChiNhanh = $"CN_{prefix}_{suffix}",
                MaChiNhanh = $"C{suffix}",
                DiaChi = "123 Duong Test",
                SoDienThoai = "0912345678",
                MoTa = "Chi nhanh test"
            };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro
            {
                ChiNhanhId = branch.ChiNhanhId,
                SoPhong = $"P_{suffix}",
                GiaThue = 2000000m,
                DienTich = 25,
                MoTa = "Phong test"
            };
            context.PhongTros.Add(room);
            await context.SaveChangesAsync();

            var tenant = new NguoiThue
            {
                HoVaTen = $"Khach {suffix}",
                SoDienThoai = "0987654321",
                Email = $"khach_{suffix}@test.com",
                CCCD = $"01234567{suffix[..4]}"
            };
            context.NguoiThues.Add(tenant);
            await context.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-3),
                ThoiDiemKetThuc = DateTime.UtcNow.AddMonths(9),
                TienCocPhong = 2000000m,
                TienThuePhong = 2000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            context.HopDongs.Add(contract);

            var admin = new NguoiDung
            {
                TenDangNhap = $"adm_{suffix}",
                MatKhauHash = "hash",
                Role = Role.Admin,
                IsActive = true
            };
            context.NguoiDungs.Add(admin);

            await context.SaveChangesAsync();

            return (branch, room, tenant, contract, admin);
        }

        [Fact]
        public async Task LockContracts_SecondTransactionWaits_UntilFirstCommits()
        {
            var (_, _, _, contract1, _) = await CreateTestHierarchyAsync("lock1");
            var (_, _, _, contract2, _) = await CreateTestHierarchyAsync("lock2");
            var ids = new[] { contract1.HopDongId, contract2.HopDongId }.OrderBy(x => x).ToList();

            await using var ctx1 = _fixture.CreateDbContext();
            await using var tx1 = await ctx1.Database.BeginTransactionAsync();
            var store1 = new InvoiceIssuanceStore(ctx1);

            // Tx 1 khóa 2 hợp đồng
            var lockedIds1 = await store1.LockContractsAsync(ids);
            Assert.Equal(2, lockedIds1.Count);

            var tx2Acquired = false;
            var tx2Task = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                await ctx2.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                await using var tx2 = await ctx2.Database.BeginTransactionAsync();
                var store2 = new InvoiceIssuanceStore(ctx2);
                var lockedIds2 = await store2.LockContractsAsync(ids);
                tx2Acquired = (lockedIds2.Count == 2);
                await tx2.CommitAsync();
            });

            // Chờ tx2 rơi vào trạng thái chờ khóa trên pg_locks
            await WaitForBlockedLocksAsync(expectedWaitingCount: 1, timeout: TimeSpan.FromSeconds(5));
            Assert.False(tx2Acquired);

            // Tx1 commit -> tx2 được giải phóng và tiếp tục
            await tx1.CommitAsync();
            await tx2Task;

            Assert.True(tx2Acquired);
        }

        [Fact]
        public async Task LockContracts_ReturnsOnlyExistingIds()
        {
            var (_, _, _, contract, _) = await CreateTestHierarchyAsync("exist");

            await using var ctx = _fixture.CreateDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var store = new InvoiceIssuanceStore(ctx);

            var result = await store.LockContractsAsync(new[] { contract.HopDongId, 999999, -5 });
            Assert.Single(result);
            Assert.Equal(contract.HopDongId, result[0]);

            await tx.RollbackAsync();
        }

        private async Task<DichVuDienNuocCuaPhong> CreateMeterPeriodAsync(int phongTroId, int thang, int nam, decimal chiSoDienMoi)
        {
            await using var ctx = _fixture.CreateDbContext();
            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = phongTroId,
                Thang = thang,
                Nam = nam,
                TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                ChiSoDienCu = 100,
                ChiSoDienMoi = chiSoDienMoi,
                DonGiaDien = 3500m,
                ChiSoNuocCu = 50,
                ChiSoNuocMoi = 60,
                DonGiaNuoc = 15000m
            };
            ctx.DichVuDienNuocCuaPhongs.Add(period);
            await ctx.SaveChangesAsync();
            return period;
        }

        [Fact]
        public async Task LockMeterPeriodsForRooms_ConcurrentEdit_DraftCreationReadsCommittedReading()
        {
            var (_, room, _, _, _) = await CreateTestHierarchyAsync("lockperiod");
            var period = await CreateMeterPeriodAsync(room.PhongTroId, 9, 2026, chiSoDienMoi: 150);

            // T1 (sửa số) giữ khóa kỳ và đổi số mới nhưng chưa commit.
            await using var ctx1 = _fixture.CreateDbContext();
            await using var tx1 = await ctx1.Database.BeginTransactionAsync();
            var editStore = new MeterImageStore(ctx1);
            var locked = await editStore.GetPeriodByIdForUpdateAsync(period.DichVuDienNuocCuaPhongId);
            Assert.NotNull(locked);
            locked!.ChiSoDienMoi = 180;
            await ctx1.SaveChangesAsync();

            decimal? readByDraft = null;
            var draftTask = Task.Run(async () =>
            {
                await using var ctx2 = _fixture.CreateDbContext();
                await ctx2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                await using var tx2 = await ctx2.Database.BeginTransactionAsync();
                var issuanceStore = new InvoiceIssuanceStore(ctx2);
                await issuanceStore.LockMeterPeriodsForRoomsAsync(new[] { room.PhongTroId }, 9, 2026);
                // Đúng thứ tự trong CreateDraftsAsync: khóa kỳ rồi mới đọc số.
                readByDraft = await ctx2.DichVuDienNuocCuaPhongs
                    .AsNoTracking()
                    .Where(x => x.DichVuDienNuocCuaPhongId == period.DichVuDienNuocCuaPhongId)
                    .Select(x => (decimal?)x.ChiSoDienMoi)
                    .SingleAsync();
                await tx2.CommitAsync();
            });

            // Tạo nháp phải bị chặn cho tới khi T1 commit, không được đọc số cũ.
            await WaitForBlockedLocksAsync(expectedWaitingCount: 1, timeout: TimeSpan.FromSeconds(5));
            Assert.Null(readByDraft);

            await tx1.CommitAsync();
            await draftTask;

            Assert.Equal(180m, readByDraft);
        }

        [Fact]
        public async Task LockMeterPeriodsForRooms_LocksOnlyRequestedRoomAndMonth()
        {
            var (_, room, _, _, _) = await CreateTestHierarchyAsync("lockperiod_scope");
            var (_, otherRoom, _, _, _) = await CreateTestHierarchyAsync("lockperiod_scope2");
            await CreateMeterPeriodAsync(room.PhongTroId, 9, 2026, chiSoDienMoi: 150);
            var otherMonth = await CreateMeterPeriodAsync(room.PhongTroId, 10, 2026, chiSoDienMoi: 150);
            var otherRoomPeriod = await CreateMeterPeriodAsync(otherRoom.PhongTroId, 9, 2026, chiSoDienMoi: 150);

            await using var ctx1 = _fixture.CreateDbContext();
            await using var tx1 = await ctx1.Database.BeginTransactionAsync();
            await new InvoiceIssuanceStore(ctx1).LockMeterPeriodsForRoomsAsync(new[] { room.PhongTroId }, 9, 2026);

            // Kỳ khác tháng và kỳ của phòng khác không bị khóa: khóa ngắn hạn lấy được ngay.
            await using var ctx2 = _fixture.CreateDbContext();
            await ctx2.Database.ExecuteSqlRawAsync("SET lock_timeout = '2000ms';");
            await using var tx2 = await ctx2.Database.BeginTransactionAsync();
            var store2 = new MeterImageStore(ctx2);
            Assert.NotNull(await store2.GetPeriodByIdForUpdateAsync(otherMonth.DichVuDienNuocCuaPhongId));
            Assert.NotNull(await store2.GetPeriodByIdForUpdateAsync(otherRoomPeriod.DichVuDienNuocCuaPhongId));

            await tx2.RollbackAsync();
            await tx1.RollbackAsync();
        }

        [Fact]
        public async Task LockMeterPeriodsForRooms_WithNoRooms_DoesNothing()
        {
            await using var ctx = _fixture.CreateDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync();

            await new InvoiceIssuanceStore(ctx).LockMeterPeriodsForRoomsAsync(Array.Empty<int>(), 9, 2026);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task CountCancelledInvoices_CountsOnlyDaHuyDeleted_ForSameContractAndPeriod()
        {
            var (_, room, _, contract, _) = await CreateTestHierarchyAsync("cnt_cancel");

            await using var ctx = _fixture.CreateDbContext();

            // 1. Bản DaHuy (!IsDeleted = false, tức IsDeleted = true) tháng 9/2026
            var inv1 = new HoaDon
            {
                MaHoaDon = "HD-TEST-1",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };
            // 2. Bản DaHuy thứ hai cùng kỳ
            var inv2 = new HoaDon
            {
                MaHoaDon = "HD-TEST-1-R1",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };
            // 3. Bản DaChot (chưa hủy, IsDeleted = false)
            var inv3 = new HoaDon
            {
                MaHoaDon = "HD-TEST-1-R2",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            // 4. Bản DaHuy tháng 8/2026 (khác tháng)
            var inv4 = new HoaDon
            {
                MaHoaDon = "HD-TEST-PREV",
                HopDongId = contract.HopDongId,
                Thang = 8,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };

            ctx.HoaDons.AddRange(inv1, inv2, inv3, inv4);
            await ctx.SaveChangesAsync();

            var store = new InvoiceIssuanceStore(ctx);
            var count = await store.CountCancelledInvoicesAsync(contract.HopDongId, 9, 2026);
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task HasOtherActiveInvoice_IgnoresCancelledAndSelf()
        {
            var (_, _, _, contract, _) = await CreateTestHierarchyAsync("active_inv");

            await using var ctx = _fixture.CreateDbContext();

            var activeInv = new HoaDon
            {
                MaHoaDon = "HD-ACT-1",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot,
                IsDeleted = false
            };
            var cancelledInv = new HoaDon
            {
                MaHoaDon = "HD-CAN-1",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };
            ctx.HoaDons.AddRange(activeInv, cancelledInv);
            await ctx.SaveChangesAsync();

            var store = new InvoiceIssuanceStore(ctx);

            // Exclude chính activeInv -> không còn hóa đơn active nào khác -> false
            var hasOther1 = await store.HasOtherActiveInvoiceAsync(contract.HopDongId, 9, 2026, activeInv.HoaDonId);
            Assert.False(hasOther1);

            // Exclude id khác -> activeInv vẫn còn -> true
            var hasOther2 = await store.HasOtherActiveInvoiceAsync(contract.HopDongId, 9, 2026, 999999);
            Assert.True(hasOther2);
        }

        [Fact]
        public async Task GetInvoiceForUpdate_ReturnsNull_ForCancelledInvoice()
        {
            var (_, _, _, contract, _) = await CreateTestHierarchyAsync("for_update");

            await using var ctx = _fixture.CreateDbContext();
            var cancelledInv = new HoaDon
            {
                MaHoaDon = "HD-DEL-1",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1000000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy,
                IsDeleted = true
            };
            ctx.HoaDons.Add(cancelledInv);
            await ctx.SaveChangesAsync();

            await using var tx = await ctx.Database.BeginTransactionAsync();
            var store = new InvoiceIssuanceStore(ctx);

            var result = await store.GetInvoiceForUpdateAsync(cancelledInv.HoaDonId);
            Assert.Null(result);

            await tx.RollbackAsync();
        }

        [Fact]
        public async Task GetBillableIncidentsInPeriod_UsesVietnamMonthBoundaries()
        {
            var (_, room, tenant, _, _) = await CreateTestHierarchyAsync("billable_sc");

            await using var ctx = _fixture.CreateDbContext();

            // Khung tháng 9/2026 theo giờ VN (UTC+7), nửa mở [startUtc, nextMonthStartUtc):
            // startUtc = 31/08/2026 17:00:00 UTC (01/09 00:00 VN)
            // nextMonthStartUtc = 30/09/2026 17:00:00 UTC (01/10 00:00 VN)
            var startOfMonthVn = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var startUtc = DateTime.SpecifyKind(startOfMonthVn.AddHours(-7), DateTimeKind.Utc);
            // Nửa mở [startUtc, nextMonthStartUtc): nextMonthStartUtc = 01/10 00:00 giờ VN = 30/09 17:00 UTC.
            var nextMonthStartUtc = DateTime.SpecifyKind(startOfMonthVn.AddMonths(1).AddHours(-7), DateTimeKind.Utc);

            // 1. Thuộc tháng 9: 30/09 23:30 VN = 16:30 UTC
            var sc1 = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Sự cố tháng 9 hợp lệ",
                MoTa = "Mô tả",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 200000m,
                NgayXuLy = new DateTime(2026, 9, 30, 16, 30, 0, DateTimeKind.Utc),
                IsDeleted = false
            };

            // 2. Thuộc tháng 10: 01/10 00:10 VN = 30/09 17:10 UTC
            var sc2 = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Sự cố tháng 10",
                MoTa = "Mô tả",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 300000m,
                NgayXuLy = new DateTime(2026, 9, 30, 17, 10, 0, DateTimeKind.Utc),
                IsDeleted = false
            };

            // 3. Chưa hoàn thành (DangXuLy) dù trong tháng 9
            var sc3 = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Chưa hoàn thành",
                MoTa = "Mô tả",
                TrangThai = TrangThaiSuCo.DangXuLy,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 150000m,
                NgayXuLy = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            };

            // 4. CongVaoHoaDon = false
            var sc4 = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Không cộng vào hóa đơn",
                MoTa = "Mô tả",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = false,
                ChiPhiSuaChua = 150000m,
                NgayXuLy = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            };

            // 5. ChiPhiSuaChua = 0
            var sc5 = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Chi phí 0",
                MoTa = "Mô tả",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 0m,
                NgayXuLy = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            };

            // 6. Đã xóa (IsDeleted = true)
            var sc6 = new YeuCauSuCo
            {
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                TieuDe = "Đã xóa",
                MoTa = "Mô tả",
                TrangThai = TrangThaiSuCo.DaHoanThanh,
                CongVaoHoaDon = true,
                ChiPhiSuaChua = 500000m,
                NgayXuLy = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = true
            };

            ctx.YeuCauSuCos.AddRange(sc1, sc2, sc3, sc4, sc5, sc6);
            await ctx.SaveChangesAsync();

            var store = new InvoiceIssuanceStore(ctx);
            var results = await store.GetBillableIncidentsInPeriodAsync(new[] { room.PhongTroId }, startUtc, nextMonthStartUtc);

            Assert.Single(results);
            Assert.Equal("Sự cố tháng 9 hợp lệ", results[0].TieuDe);
            Assert.Equal(200000m, results[0].ChiPhiSuaChua);
        }

        [Fact]
        public async Task CreateDrafts_TwoConcurrentRequestsSameContract_OneCreatedOneSkipped_NoUniqueViolation()
        {
            var (branch, room, tenant, contract, admin) = await CreateTestHierarchyAsync("conc_draft");

            // Tạo kỳ chỉ số DaDuyet tháng 9/2026
            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 150,
                    DonGiaDien = 3500m,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 60,
                    DonGiaNuoc = 15000m
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);

                // Thêm nhân viên có phân công chi nhánh
                var user = new NguoiDung
                {
                    TenDangNhap = $"staff_{Guid.NewGuid():N}"[..15],
                    MatKhauHash = "hash",
                    Role = Role.NhanVien
                };
                seedCtx.NguoiDungs.Add(user);
                await seedCtx.SaveChangesAsync();

                var staff = new NhanVienChiNhanh
                {
                    NguoiDungId = user.NguoiDungId,
                    ChiNhanhId = branch.ChiNhanhId,
                    NguoiPhanCongId = admin.NguoiDungId,
                    IsActive = true
                };
                seedCtx.NhanVienChiNhanhs.Add(staff);
                await seedCtx.SaveChangesAsync();
            }

            // Tạo kết nối giữ khóa trên hợp đồng
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.HopDongs
                .FromSqlInterpolated($"SELECT * FROM hop_dong WHERE \"HopDongId\" = {contract.HopDongId} FOR UPDATE")
                .ToListAsync();

            var req = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 9,
                Nam = 2026,
                PhongTroIds = new[] { room.PhongTroId }
            };

            var t1 = Task.Run(async () =>
            {
                await using var c1 = _fixture.CreateDbContext();
                await c1.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service1 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c1),
                    new HoaDonStore(c1),
                    new EfUnitOfWork(c1),
                    new EmployeeAccessService(new EmployeeBranchStore(c1)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                return await service1.CreateDraftsAsync(req, actorId: admin.NguoiDungId); // Admin
            });

            var t2 = Task.Run(async () =>
            {
                await using var c2 = _fixture.CreateDbContext();
                await c2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service2 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c2),
                    new HoaDonStore(c2),
                    new EfUnitOfWork(c2),
                    new EmployeeAccessService(new EmployeeBranchStore(c2)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                return await service2.CreateDraftsAsync(req, actorId: admin.NguoiDungId); // Admin
            });

            // Chờ 2 transaction bị chặn trên pg_locks
            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(5));

            // Thả khóa
            await holderTx.CommitAsync();

            var r1 = await t1;
            var r2 = await t2;

            Assert.True(r1.Success);
            Assert.True(r2.Success);

            // Một bên tạo thành công (CreatedCount = 1), bên kia bị skip (CreatedCount = 0)
            var totalCreated = r1.Data!.CreatedCount + r2.Data!.CreatedCount;
            Assert.Equal(1, totalCreated);

            // Kiểm tra DB chỉ có đúng 1 hóa đơn
            await using var verifyCtx = _fixture.CreateDbContext();
            var invoices = await verifyCtx.HoaDons
                .Where(h => h.HopDongId == contract.HopDongId && h.Thang == 9 && h.Nam == 2026 && !h.IsDeleted)
                .ToListAsync();
            Assert.Single(invoices);
        }

        [Fact]
        public async Task CreateDrafts_ReplacementCodes_AreUniquePerContractPeriod()
        {
            var (branch, room, _, contract, admin) = await CreateTestHierarchyAsync("rep_codes");

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                seedCtx.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 150,
                    DonGiaDien = 3500m,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 60,
                    DonGiaNuoc = 15000m
                });
                await seedCtx.SaveChangesAsync();
            }

            var req = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 9,
                Nam = 2026,
                PhongTroIds = new[] { room.PhongTroId }
            };

            // 1. Tạo bản nháp gốc
            await using (var c1 = _fixture.CreateDbContext())
            {
                var s1 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c1),
                    new HoaDonStore(c1),
                    new EfUnitOfWork(c1),
                    new EmployeeAccessService(new EmployeeBranchStore(c1)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                var res = await s1.CreateDraftsAsync(req, actorId: admin.NguoiDungId);
                Assert.True(res.Success);
                Assert.Equal(1, res.Data!.CreatedCount);
            }

            // Hủy bản gốc (giả lập hủy: IsDeleted = true, DaHuy)
            await using (var cDel1 = _fixture.CreateDbContext())
            {
                var inv = await cDel1.HoaDons.FirstAsync(h => h.HopDongId == contract.HopDongId && h.Thang == 9 && h.Nam == 2026 && !h.IsDeleted);
                inv.IsDeleted = true;
                inv.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                await cDel1.SaveChangesAsync();
            }

            // 2. Tạo bản thay thế -R1
            await using (var c2 = _fixture.CreateDbContext())
            {
                var s2 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c2),
                    new HoaDonStore(c2),
                    new EfUnitOfWork(c2),
                    new EmployeeAccessService(new EmployeeBranchStore(c2)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                var res = await s2.CreateDraftsAsync(req, actorId: admin.NguoiDungId);
                Assert.True(res.Success);
                Assert.Equal(1, res.Data!.CreatedCount);
                Assert.EndsWith("-R1", res.Data.Items[0].MaHoaDon);
            }

            // Hủy bản thứ hai
            await using (var cDel2 = _fixture.CreateDbContext())
            {
                var inv = await cDel2.HoaDons.FirstAsync(h => h.HopDongId == contract.HopDongId && h.Thang == 9 && h.Nam == 2026 && !h.IsDeleted);
                inv.IsDeleted = true;
                inv.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                await cDel2.SaveChangesAsync();
            }

            // 3. Tạo bản thay thế -R2
            await using (var c3 = _fixture.CreateDbContext())
            {
                var s3 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c3),
                    new HoaDonStore(c3),
                    new EfUnitOfWork(c3),
                    new EmployeeAccessService(new EmployeeBranchStore(c3)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                var res = await s3.CreateDraftsAsync(req, actorId: admin.NguoiDungId);
                Assert.True(res.Success);
                Assert.Equal(1, res.Data!.CreatedCount);
                Assert.EndsWith("-R2", res.Data.Items[0].MaHoaDon);
            }

            // Kiểm tra DB có cả 3 bản ghi với 3 mã khác nhau
            await using (var verifyCtx = _fixture.CreateDbContext())
            {
                var allInvoices = await verifyCtx.HoaDons
                    .Where(h => h.HopDongId == contract.HopDongId && h.Thang == 9 && h.Nam == 2026)
                    .OrderBy(h => h.HoaDonId)
                    .ToListAsync();
                Assert.Equal(3, allInvoices.Count);
                Assert.DoesNotContain("-R", allInvoices[0].MaHoaDon);
                Assert.EndsWith("-R1", allInvoices[1].MaHoaDon);
                Assert.EndsWith("-R2", allInvoices[2].MaHoaDon);
            }
        }

        [Fact]
        public async Task UpdateStatus_ConcurrentWithSubmit_NeverLeavesChoDuyetWithStaleIncidentTotal()
        {
            var (branch, room, tenant, contract, admin) = await CreateTestHierarchyAsync("sync_sub");

            int invoiceId;
            int incidentId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var sc = new YeuCauSuCo
                {
                    PhongTroId = room.PhongTroId,
                    NguoiThueId = tenant.NguoiThueId,
                    TieuDe = "Sửa khóa cửa",
                    MoTa = "Khóa hỏng",
                    TrangThai = TrangThaiSuCo.DaHoanThanh,
                    CongVaoHoaDon = true,
                    ChiPhiSuaChua = 100000m,
                    NgayGui = DateTime.UtcNow.AddDays(-5),
                    NgayXuLy = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                };
                seedCtx.YeuCauSuCos.Add(sc);
                await seedCtx.SaveChangesAsync();
                incidentId = sc.Id;

                var inv = new HoaDon
                {
                    HopDongId = contract.HopDongId,
                    MaHoaDon = $"HD-TEST-{Guid.NewGuid():N}"[..20],
                    Thang = 9,
                    Nam = 2026,
                    TongTien = 2100000m,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                    NgayTao = DateTime.UtcNow,
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                    {
                        new ChiTietHoaDon { TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1, DichVuId = 1 },
                        new ChiTietHoaDon { TenDichVu = "Sự cố: Sửa khóa cửa", TongTien = 100000m, DonGia = 100000m, SoLuong = 1, DichVuId = null }
                    }
                };
                seedCtx.HoaDons.Add(inv);
                await seedCtx.SaveChangesAsync();
                invoiceId = inv.HoaDonId;
            }

            // Kết nối giữ khóa trên hóa đơn
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {invoiceId} FOR UPDATE")
                .ToListAsync();

            // Task 1: Sửa chi phí sự cố lên 250k
            var t1 = Task.Run(async () =>
            {
                await using var c1 = _fixture.CreateDbContext();
                await c1.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service1 = new YeuCauSuCoService(
                    new YeuCauSuCoStore(c1),
                    new EfUnitOfWork(c1),
                    new EmployeeAccessService(new EmployeeBranchStore(c1)),
                    new InvoiceIssuanceStore(c1),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<YeuCauSuCoService>.Instance);

                return await service1.UpdateStatusAsync(
                    id: incidentId,
                    trangThai: AppTrangThaiSuCo.DaHoanThanh,
                    chiPhi: 250000m,
                    congVaoHoaDon: true,
                    lyDoTuChoi: null,
                    ghiChuAdmin: "Tăng chi phí",
                    actorId: admin.NguoiDungId);
            });

            // Task 2: Gửi duyệt hóa đơn
            var t2 = Task.Run(async () =>
            {
                await using var c2 = _fixture.CreateDbContext();
                await c2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service2 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c2),
                    new HoaDonStore(c2),
                    new EfUnitOfWork(c2),
                    new EmployeeAccessService(new EmployeeBranchStore(c2)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);

                return await service2.SubmitAsync(invoiceId, actorId: admin.NguoiDungId);
            });

            await WaitForBlockedLocksAsync(expectedWaitingCount: 1, timeout: TimeSpan.FromSeconds(5));

            await holderTx.CommitAsync();

            var r1 = await t1;
            var r2 = await t2;

            // Kiểm tra trạng thái trong DB:
            // Không bao giờ để lại hóa đơn ChoDuyet có tổng tiền lệch so với chi tiết
            await using var verifyCtx = _fixture.CreateDbContext();
            var finalInv = await verifyCtx.HoaDons
                .Include(h => h.ChiTietHoaDonDichVus)
                .FirstAsync(h => h.HoaDonId == invoiceId);

            var activeDetails = finalInv.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
            var activeTotal = activeDetails.Sum(x => x.TongTien);
            Assert.Equal(activeTotal, finalInv.TongTien);

            if (r1.Success && r2.Success)
            {
                // Update chạy trước -> Submit chạy sau -> Đã chốt chi phí 250k
                Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, finalInv.TrangThaiPhatHanh);
                Assert.Equal(2250000m, finalInv.TongTien);
            }
            else if (!r1.Success && r2.Success)
            {
                // Submit chạy trước -> Update bị từ chối vì hóa đơn đã qua Nhap
                Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, finalInv.TrangThaiPhatHanh);
                Assert.Equal(2100000m, finalInv.TongTien);
                Assert.Contains("đã gửi duyệt", r1.Message);
            }
        }

        [Fact]
        public async Task UpdateStatus_ConcurrentWithCreateDrafts_DraftAlwaysHasLatestIncidentCost()
        {
            var (branch, room, tenant, contract, admin) = await CreateTestHierarchyAsync("sync_draft");

            int incidentId;
            await using (var seedCtx = _fixture.CreateDbContext())
            {
                seedCtx.DichVuDienNuocCuaPhongs.Add(new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet,
                    ChiSoDienCu = 100,
                    ChiSoDienMoi = 150,
                    DonGiaDien = 3500m,
                    ChiSoNuocCu = 50,
                    ChiSoNuocMoi = 60,
                    DonGiaNuoc = 15000m
                });

                var sc = new YeuCauSuCo
                {
                    PhongTroId = room.PhongTroId,
                    NguoiThueId = tenant.NguoiThueId,
                    TieuDe = "Sửa ống nước",
                    MoTa = "Rò rỉ",
                    TrangThai = TrangThaiSuCo.DaHoanThanh,
                    CongVaoHoaDon = true,
                    ChiPhiSuaChua = 100000m,
                    NgayGui = DateTime.UtcNow.AddDays(-3),
                    NgayXuLy = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                };
                seedCtx.YeuCauSuCos.Add(sc);
                await seedCtx.SaveChangesAsync();
                incidentId = sc.Id;
            }

            // Kết nối giữ khóa trên hợp đồng
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.HopDongs
                .FromSqlInterpolated($"SELECT * FROM hop_dong WHERE \"HopDongId\" = {contract.HopDongId} FOR UPDATE")
                .ToListAsync();

            var req = new CreateInvoiceDraftsRequest
            {
                ChiNhanhId = branch.ChiNhanhId,
                Thang = 9,
                Nam = 2026,
                PhongTroIds = new[] { room.PhongTroId }
            };

            // Task 1: Tạo hóa đơn nháp
            var t1 = Task.Run(async () =>
            {
                await using var c1 = _fixture.CreateDbContext();
                await c1.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service1 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c1),
                    new HoaDonStore(c1),
                    new EfUnitOfWork(c1),
                    new EmployeeAccessService(new EmployeeBranchStore(c1)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);

                return await service1.CreateDraftsAsync(req, actorId: admin.NguoiDungId);
            });

            // Task 2: Sửa chi phí sự cố lên 300k
            var t2 = Task.Run(async () =>
            {
                await using var c2 = _fixture.CreateDbContext();
                await c2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service2 = new YeuCauSuCoService(
                    new YeuCauSuCoStore(c2),
                    new EfUnitOfWork(c2),
                    new EmployeeAccessService(new EmployeeBranchStore(c2)),
                    new InvoiceIssuanceStore(c2),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<YeuCauSuCoService>.Instance);

                return await service2.UpdateStatusAsync(
                    id: incidentId,
                    trangThai: AppTrangThaiSuCo.DaHoanThanh,
                    chiPhi: 300000m,
                    congVaoHoaDon: true,
                    lyDoTuChoi: null,
                    ghiChuAdmin: "Tăng chi phí",
                    actorId: admin.NguoiDungId);
            });

            await WaitForBlockedLocksAsync(expectedWaitingCount: 1, timeout: TimeSpan.FromSeconds(5));

            await holderTx.CommitAsync();

            var r1 = await t1;
            var r2 = await t2;

            Assert.True(r1.Success);
            Assert.True(r2.Success);

            // Kiểm tra DB: hóa đơn nháp cuối cùng phải có chi phí sự cố là 300.000đ
            await using var verifyCtx = _fixture.CreateDbContext();
            var inv = await verifyCtx.HoaDons
                .Include(h => h.ChiTietHoaDonDichVus)
                .FirstAsync(h => h.HopDongId == contract.HopDongId && h.Thang == 9 && h.Nam == 2026 && !h.IsDeleted);

            var suCoLine = inv.ChiTietHoaDonDichVus.First(x => !x.IsDeleted && x.TenDichVu.StartsWith(QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.InvoiceLineNames.SuCoPrefix));
            Assert.Equal(300000m, suCoLine.TongTien);
        }

        [Fact]
        public async Task Finalize_TwoConcurrentAdmins_OnlyOneSucceeds_SingleHistoryRow()
        {
            var (branch, room, tenant, contract, admin1) = await CreateTestHierarchyAsync("fin_concur");

            int admin2Id;
            int invoiceId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var admin2 = new NguoiDung
                {
                    TenDangNhap = $"adm2_{Guid.NewGuid():N}"[..15],
                    MatKhauHash = "h",
                    Role = Role.Admin,
                    IsActive = true
                };
                seedCtx.NguoiDungs.Add(admin2);

                var inv = new HoaDon
                {
                    HopDongId = contract.HopDongId,
                    MaHoaDon = $"HD-FIN-{Guid.NewGuid():N}"[..20],
                    Thang = 9,
                    Nam = 2026,
                    TongTien = 2000000m,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet,
                    NgayTao = DateTime.UtcNow,
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                    {
                        new ChiTietHoaDon { TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1, DichVuId = 1 }
                    }
                };
                seedCtx.HoaDons.Add(inv);

                await seedCtx.SaveChangesAsync();
                admin2Id = admin2.NguoiDungId;
                invoiceId = inv.HoaDonId;
            }

            // Kết nối giữ khóa trên hóa đơn
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {invoiceId} FOR UPDATE")
                .ToListAsync();

            var t1 = Task.Run(async () =>
            {
                await using var c1 = _fixture.CreateDbContext();
                await c1.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service1 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c1),
                    new HoaDonStore(c1),
                    new EfUnitOfWork(c1),
                    new EmployeeAccessService(new EmployeeBranchStore(c1)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                return await service1.FinalizeAsync(invoiceId, actorId: admin1.NguoiDungId);
            });

            var t2 = Task.Run(async () =>
            {
                await using var c2 = _fixture.CreateDbContext();
                await c2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service2 = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c2),
                    new HoaDonStore(c2),
                    new EfUnitOfWork(c2),
                    new EmployeeAccessService(new EmployeeBranchStore(c2)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                return await service2.FinalizeAsync(invoiceId, actorId: admin2Id);
            });

            await WaitForBlockedLocksAsync(expectedWaitingCount: 1, timeout: TimeSpan.FromSeconds(5));

            await holderTx.CommitAsync();

            var r1 = await t1;
            var r2 = await t2;

            // Đúng một Admin thành công, một Admin bị từ chối
            Assert.True(r1.Success ^ r2.Success);
            var failedResult = !r1.Success ? r1 : r2;
            Assert.Contains("trạng thái chờ duyệt", failedResult.Message);

            // Kiểm tra DB: hóa đơn chuyển DaChot và chỉ có 1 hàng lịch sử DaChot
            await using var verifyCtx = _fixture.CreateDbContext();
            var finalInv = await verifyCtx.HoaDons
                .Include(h => h.LichSuTrangThaiHoaDons)
                .FirstAsync(h => h.HoaDonId == invoiceId);

            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, finalInv.TrangThaiPhatHanh);
            var chotHistories = finalInv.LichSuTrangThaiHoaDons
                .Where(h => h.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaChot)
                .ToList();
            Assert.Single(chotHistories);
        }

        [Fact]
        public async Task Submit_ConcurrentWithMeterCorrection_NeverLeavesChoDuyetWithStaleTotal()
        {
            var (branch, room, tenant, contract, admin) = await CreateTestHierarchyAsync("sub_meter_corr");

            int periodId;
            int imgId;
            int invoiceId;

            await using (var seedCtx = _fixture.CreateDbContext())
            {
                var period = new DichVuDienNuocCuaPhong
                {
                    PhongTroId = room.PhongTroId,
                    Thang = 9,
                    Nam = 2026,
                    ChiSoDienCu = 100m,
                    ChiSoDienMoi = 150m,
                    DonGiaDien = 3500m,
                    TrangThaiGhiNhan = TrangThaiGhiNhan.DaDuyet
                };
                seedCtx.DichVuDienNuocCuaPhongs.Add(period);
                await seedCtx.SaveChangesAsync();
                periodId = period.DichVuDienNuocCuaPhongId;

                var img = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = periodId,
                    LoaiDongHo = LoaiDongHo.Dien,
                    Url = "https://example.com/meter.jpg",
                    PublicId = "meter_pub_id",
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.DaXacNhan,
                    DuocChonLamChiSoChinhThuc = true,
                    GiaTriXacNhan = 150m,
                    NguoiXacNhanId = admin.NguoiDungId,
                    NgayXacNhan = DateTime.UtcNow
                };
                seedCtx.AnhChiSoDongHos.Add(img);

                var inv = new HoaDon
                {
                    HopDongId = contract.HopDongId,
                    MaHoaDon = $"HD-SUB-{Guid.NewGuid():N}"[..20],
                    Thang = 9,
                    Nam = 2026,
                    DichVuDienNuocCuaPhongId = periodId,
                    TongTien = 2175000m,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.Nhap,
                    NgayTao = DateTime.UtcNow,
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon>
                    {
                        new ChiTietHoaDon { TenDichVu = "Tiền phòng", TongTien = 2000000m, DonGia = 2000000m, SoLuong = 1 },
                        new ChiTietHoaDon { TenDichVu = "Điện: 50 x 3500", TongTien = 175000m, DonGia = 3500m, SoLuong = 50, DichVuId = 1 }
                    }
                };
                seedCtx.HoaDons.Add(inv);

                await seedCtx.SaveChangesAsync();
                imgId = img.AnhChiSoDongHoId;
                invoiceId = inv.HoaDonId;
            }

            // Kết nối giữ khóa trên kỳ chỉ số
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.DichVuDienNuocCuaPhongs
                .FromSqlInterpolated($"SELECT * FROM dich_vu_dien_nuoc_cua_phong WHERE \"DichVuDienNuocCuaPhongId\" = {periodId} FOR UPDATE")
                .ToListAsync();

            // Task 1: Sửa chỉ số điện từ 150 lên 180 (dùng 80 số x 3500 = 280.000)
            var t1 = Task.Run(async () =>
            {
                await using var c1 = _fixture.CreateDbContext();
                await c1.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var workflow = MeterImageStoreTests.CreateWorkflowService(c1);
                return await workflow.CorrectConfirmedImageAsync(
                    new CorrectConfirmedMeterImageRequest(imgId, 180m, "Sửa lại số điện"),
                    admin.NguoiDungId);
            });

            // Task 2: Gửi duyệt hóa đơn
            var t2 = Task.Run(async () =>
            {
                await using var c2 = _fixture.CreateDbContext();
                await c2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var issuance = new InvoiceIssuanceService(
                    new InvoiceIssuanceStore(c2),
                    new HoaDonStore(c2),
                    new EfUnitOfWork(c2),
                    new EmployeeAccessService(new EmployeeBranchStore(c2)),
                    new QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services.HoaDonCalculatorService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<InvoiceIssuanceService>.Instance);
                return await issuance.SubmitAsync(invoiceId, actorId: admin.NguoiDungId);
            });

            await WaitForBlockedLocksAsync(expectedWaitingCount: 1, timeout: TimeSpan.FromSeconds(5));

            await holderTx.CommitAsync();

            var r1 = await t1;
            var r2 = await t2;

            // Kiểm tra DB:
            // Không bao giờ có deadlock (cả hai hoàn thành)
            // Không bao giờ để lại hóa đơn ChoDuyet có tổng tiền lệch so với chi tiết
            await using var verifyCtx = _fixture.CreateDbContext();
            var finalInv = await verifyCtx.HoaDons
                .Include(h => h.ChiTietHoaDonDichVus)
                .FirstAsync(h => h.HoaDonId == invoiceId);

            var activeDetails = finalInv.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).ToList();
            var activeTotal = activeDetails.Sum(x => x.TongTien);
            Assert.Equal(activeTotal, finalInv.TongTien);

            if (r1.Success && r2.Success)
            {
                // Sửa số chạy trước -> cập nhật nháp lên 2.280.000đ -> Gửi duyệt thấy tổng tiền mới
                Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, finalInv.TrangThaiPhatHanh);
                Assert.Equal(2280000m, finalInv.TongTien);
            }
            else if (!r1.Success && r2.Success)
            {
                // Gửi duyệt chạy trước -> Sửa số bị từ chối vì hóa đơn không còn ở Nhap
                Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, finalInv.TrangThaiPhatHanh);
                Assert.Equal(2175000m, finalInv.TongTien);
                Assert.Contains("nháp", r1.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
