using System;
using System.Linq;
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
    public class InvoicePublicationStoreTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoicePublicationStoreTests(PostgreSqlFixture fixture)
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

        private async Task<(ChiNhanh branch, PhongTro room, NguoiThue tenant, HopDong contract)> CreateTestHierarchyAsync(string prefix)
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
            await context.SaveChangesAsync();

            return (branch, room, tenant, contract);
        }

        private static HoaDon MakeInvoice(int hopDongId, string ma, TrangThaiPhatHanhHoaDon status = TrangThaiPhatHanhHoaDon.DaChot, bool isDeleted = false)
        {
            return new HoaDon
            {
                MaHoaDon = ma,
                HopDongId = hopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1_000_000m,
                TrangThaiPhatHanh = status,
                IsDeleted = isDeleted
            };
        }

        [Fact]
        public async Task GetSnapshotAsync_ReturnsBranchAndStatus_IncludingCancelledAndDeleted()
        {
            var (branch, _, _, contract) = await CreateTestHierarchyAsync("snap");

            await using var ctx = _fixture.CreateDbContext();
            var invActive = MakeInvoice(contract.HopDongId, "HD-SNAP-ACTIVE");
            var invCancelled = MakeInvoice(contract.HopDongId, "HD-SNAP-CANCELLED", TrangThaiPhatHanhHoaDon.DaHuy, isDeleted: true);
            ctx.HoaDons.AddRange(invActive, invCancelled);
            await ctx.SaveChangesAsync();

            var store = new InvoicePublicationStore(ctx);

            var snapActive = await store.GetSnapshotAsync(invActive.HoaDonId);
            Assert.NotNull(snapActive);
            Assert.Equal(branch.ChiNhanhId, snapActive!.ChiNhanhId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, snapActive.TrangThaiPhatHanh);
            Assert.False(snapActive.IsDeleted);

            var snapCancelled = await store.GetSnapshotAsync(invCancelled.HoaDonId);
            Assert.NotNull(snapCancelled);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, snapCancelled!.TrangThaiPhatHanh);
            Assert.True(snapCancelled.IsDeleted);

            var snapMissing = await store.GetSnapshotAsync(999999);
            Assert.Null(snapMissing);
        }

        [Fact]
        public async Task GetRecipientAsync_ActiveAccount_ReturnsNguoiDungId()
        {
            var (_, _, tenant, contract) = await CreateTestHierarchyAsync("recip_active");

            await using var ctx = _fixture.CreateDbContext();
            var user = new NguoiDung
            {
                TenDangNhap = $"tnt_{Guid.NewGuid():N}"[..15],
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                NguoiThueId = tenant.NguoiThueId,
                IsActive = true
            };
            ctx.NguoiDungs.Add(user);
            await ctx.SaveChangesAsync();

            var store = new InvoicePublicationStore(ctx);
            var recipient = await store.GetRecipientAsync(contract.HopDongId);

            Assert.NotNull(recipient);
            Assert.Equal(tenant.NguoiThueId, recipient!.NguoiThueId);
            Assert.Equal(user.NguoiDungId, recipient.NguoiDungId);
            Assert.Equal(tenant.Email, recipient.Email);
        }

        [Fact]
        public async Task GetRecipientAsync_LockedAccount_ReturnsNullNguoiDungId()
        {
            // Mỗi NguoiThue chỉ được gắn tối đa một NguoiDung (unique NguoiThueId), nên tách riêng ca khóa và ca xóa.
            var (_, _, tenant, contract) = await CreateTestHierarchyAsync("recip_locked");

            await using var ctx = _fixture.CreateDbContext();
            var lockedUser = new NguoiDung
            {
                TenDangNhap = $"lck_{Guid.NewGuid():N}"[..15],
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                NguoiThueId = tenant.NguoiThueId,
                IsActive = false
            };
            ctx.NguoiDungs.Add(lockedUser);
            await ctx.SaveChangesAsync();

            var store = new InvoicePublicationStore(ctx);
            var recipient = await store.GetRecipientAsync(contract.HopDongId);

            Assert.NotNull(recipient);
            Assert.Null(recipient!.NguoiDungId);
        }

        [Fact]
        public async Task GetRecipientAsync_DeletedAccount_ReturnsNullNguoiDungId()
        {
            var (_, _, tenant, contract) = await CreateTestHierarchyAsync("recip_deleted");

            await using var ctx = _fixture.CreateDbContext();
            var deletedUser = new NguoiDung
            {
                TenDangNhap = $"del_{Guid.NewGuid():N}"[..15],
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                NguoiThueId = tenant.NguoiThueId,
                IsActive = true,
                IsDeleted = true
            };
            ctx.NguoiDungs.Add(deletedUser);
            await ctx.SaveChangesAsync();

            var store = new InvoicePublicationStore(ctx);
            var recipient = await store.GetRecipientAsync(contract.HopDongId);

            Assert.NotNull(recipient);
            Assert.Null(recipient!.NguoiDungId);
        }

        [Fact]
        public async Task GetRecipientAsync_BlankEmail_ReturnsNullEmail()
        {
            await using var seedCtx = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var branch = new ChiNhanh { TenChiNhanh = $"CN_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "Dc", SoDienThoai = "0900000000", MoTa = "MT" };
            seedCtx.ChiNhanhs.Add(branch);
            await seedCtx.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 1000000m, DienTich = 20, MoTa = "MT" };
            seedCtx.PhongTros.Add(room);
            await seedCtx.SaveChangesAsync();

            // Email là cột bắt buộc trong NguoiThue, dùng chuỗi trắng để mô phỏng "chưa có email".
            var tenant = new NguoiThue { HoVaTen = $"Khach_{suffix}", SoDienThoai = "0900000001", Email = "   ", CCCD = $"07900000{suffix[..2]}" };
            seedCtx.NguoiThues.Add(tenant);
            await seedCtx.SaveChangesAsync();

            var contract = new HopDong
            {
                MaHopDong = $"HD_{suffix}",
                PhongTroId = room.PhongTroId,
                NguoiThueId = tenant.NguoiThueId,
                ThoiDiemBatDau = DateTime.UtcNow.AddMonths(-1),
                TienCocPhong = 1000000m,
                TienThuePhong = 1000000m,
                TrangThaiHopDong = TrangThaiHopDong.DangHoatDong
            };
            seedCtx.HopDongs.Add(contract);
            await seedCtx.SaveChangesAsync();

            var store = new InvoicePublicationStore(seedCtx);
            var recipient = await store.GetRecipientAsync(contract.HopDongId);

            Assert.NotNull(recipient);
            Assert.Null(recipient!.Email);
        }

        [Fact]
        public async Task GetRecipientAsync_OtherContractMemberWithAccount_NotReturned()
        {
            var (_, _, tenant, contract) = await CreateTestHierarchyAsync("recip_other_member");

            await using var ctx = _fixture.CreateDbContext();

            // Thành viên khác của hợp đồng (không phải người đứng tên)
            var otherMember = new NguoiThue
            {
                HoVaTen = "Thanh vien khac",
                SoDienThoai = "0911111111",
                Email = "other_member@test.com",
                CCCD = $"07911111{Guid.NewGuid().ToString("N")[..2]}"
            };
            ctx.NguoiThues.Add(otherMember);
            await ctx.SaveChangesAsync();

            ctx.ChiTietThanhVienHopDongs.Add(new ChiTietThanhVienHopDong
            {
                HopDongId = contract.HopDongId,
                NguoiThueId = otherMember.NguoiThueId,
                NgayVao = DateTime.UtcNow
            });

            var otherMemberAccount = new NguoiDung
            {
                TenDangNhap = $"om_{Guid.NewGuid():N}"[..15],
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                NguoiThueId = otherMember.NguoiThueId,
                IsActive = true
            };
            ctx.NguoiDungs.Add(otherMemberAccount);
            await ctx.SaveChangesAsync();

            var store = new InvoicePublicationStore(ctx);
            var recipient = await store.GetRecipientAsync(contract.HopDongId);

            Assert.NotNull(recipient);
            Assert.Equal(tenant.NguoiThueId, recipient!.NguoiThueId);
            Assert.Null(recipient.NguoiDungId);
        }

        [Fact]
        public async Task LockInvoiceAsync_UsesForUpdate_SecondCallerWaitsUntilFirstCommits()
        {
            var (_, _, _, contract) = await CreateTestHierarchyAsync("lock_for_update");

            await using var seedCtx = _fixture.CreateDbContext();
            var invoice = MakeInvoice(contract.HopDongId, "HD-LOCK-1");
            seedCtx.HoaDons.Add(invoice);
            await seedCtx.SaveChangesAsync();
            var invoiceId = invoice.HoaDonId;

            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            var holderStore = new InvoicePublicationStore(holderCtx);
            var held = await holderStore.LockInvoiceAsync(invoiceId);
            Assert.NotNull(held);

            var waiterAcquired = false;
            var waiterTask = Task.Run(async () =>
            {
                await using var waiterCtx = _fixture.CreateDbContext();
                await waiterCtx.Database.ExecuteSqlRawAsync("SET lock_timeout = '5000ms';");
                await using var waiterTx = await waiterCtx.Database.BeginTransactionAsync();
                var waiterStore = new InvoicePublicationStore(waiterCtx);
                var waited = await waiterStore.LockInvoiceAsync(invoiceId);
                waiterAcquired = waited != null;
                await waiterTx.CommitAsync();
            });

            await WaitForBlockedLocksAsync(expectedWaitingCount: 1, timeout: TimeSpan.FromSeconds(5));
            Assert.False(waiterAcquired);

            await holderTx.CommitAsync();
            await waiterTask;

            Assert.True(waiterAcquired);
        }

        [Fact]
        public async Task GetHoaDonDetailByIdAsync_ReturnsHanThanhToanVnFormat()
        {
            var (_, _, _, contract) = await CreateTestHierarchyAsync("han_format");

            await using var ctx = _fixture.CreateDbContext();
            var invoiceWithHan = MakeInvoice(contract.HopDongId, "HD-HAN-1", TrangThaiPhatHanhHoaDon.DaGui);
            invoiceWithHan.HanThanhToan = new DateTime(2026, 10, 5, 16, 59, 59, DateTimeKind.Utc);

            var invoiceWithoutHan = MakeInvoice(contract.HopDongId, "HD-HAN-2");
            invoiceWithoutHan.Thang = 10; // Tránh trùng khóa duy nhất (HopDongId, Thang, Nam) với invoiceWithHan

            // L4 (kế hoạch): hạn gần nửa đêm giờ VN vẫn phải được quy đổi đúng sang ngày VN kế tiếp.
            var invoiceWithHanNearMidnightVn = MakeInvoice(contract.HopDongId, "HD-HAN-3", TrangThaiPhatHanhHoaDon.DaGui);
            invoiceWithHanNearMidnightVn.Thang = 11; // Tránh trùng khóa duy nhất (HopDongId, Thang, Nam)
            invoiceWithHanNearMidnightVn.HanThanhToan = new DateTime(2026, 10, 5, 17, 30, 0, DateTimeKind.Utc);

            ctx.HoaDons.AddRange(invoiceWithHan, invoiceWithoutHan, invoiceWithHanNearMidnightVn);
            await ctx.SaveChangesAsync();

            var store = new HoaDonStore(ctx);

            var detailWithHan = await store.GetHoaDonDetailByIdAsync(invoiceWithHan.HoaDonId);
            Assert.NotNull(detailWithHan);
            Assert.Equal("05/10/2026", detailWithHan!.HanThanhToan);

            var detailWithoutHan = await store.GetHoaDonDetailByIdAsync(invoiceWithoutHan.HoaDonId);
            Assert.NotNull(detailWithoutHan);
            Assert.Equal(string.Empty, detailWithoutHan!.HanThanhToan);

            var detailNearMidnight = await store.GetHoaDonDetailByIdAsync(invoiceWithHanNearMidnightVn.HoaDonId);
            Assert.NotNull(detailNearMidnight);
            Assert.Equal("06/10/2026", detailNearMidnight!.HanThanhToan);
        }
    }
}
