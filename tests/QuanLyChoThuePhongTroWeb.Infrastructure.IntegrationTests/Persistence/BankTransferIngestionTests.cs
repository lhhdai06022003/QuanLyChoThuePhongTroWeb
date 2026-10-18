using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Ghi sổ hóa đơn từ giao dịch SePay trên PostgreSQL thật: khóa dòng hoa_don tuần tự hóa các lần ghi,
    // chống trùng tính cả dòng đã xóa mềm, unique index MaGiaoDich là lớp chặn cuối (spec thanh toán trực tuyến §5, §11.2).
    [Collection("PostgreSqlCollection")]
    public class BankTransferIngestionTests
    {
        private static readonly DateTime TransferTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly PostgreSqlFixture _fixture;

        public BankTransferIngestionTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private class NoOpNotifier : IThongBaoNotifier
        {
            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private static InvoiceTransferHandler CreateHandler(ApplicationDbContext ctx)
        {
            var transaction = new InvoicePaymentTransaction(new InvoicePaymentStore(ctx), new EfUnitOfWork(ctx), new NoOpNotifier(), NullLogger<InvoicePaymentTransaction>.Instance);
            return new InvoiceTransferHandler(transaction);
        }

        private static string NewReference() => "FT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static IncomingBankTransfer Transfer(string reference, int hoaDonId, decimal amount = 1_500_000m, long id = 92704) => new(
            id, "MBBank", TransferTime, "0123456789", null, null, $"TTPTHD{hoaDonId}", "in", amount, reference, $"TTPTHD{hoaDonId}");

        private async Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, int hoaDonId)
        {
            await using var ctx = _fixture.CreateDbContext();
            return await CreateHandler(ctx).HandleAsync(transfer, $"TTPTHD{hoaDonId}");
        }

        private sealed record Seeded(int InvoiceId, int ActorId, int NguoiThueId, int ChiNhanhId, int TenantUserId);

        // Chi nhánh, phòng, người thuê (có tài khoản), hợp đồng, một nhân viên phân công chi nhánh và một hóa đơn DaGui.
        private async Task<Seeded> SeedSentInvoiceAsync(string prefix, decimal tongTien = 1_500_000m)
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{prefix}_{suffix}", MaChiNhanh = TestCodes.MaChiNhanh("C"), DiaChi = "123 Duong Test", SoDienThoai = "0912345678", MoTa = "test" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var room = new PhongTro { ChiNhanhId = branch.ChiNhanhId, SoPhong = $"P_{suffix}", GiaThue = 2000000m, DienTich = 25, MoTa = "test" };
            var tenant = new NguoiThue { HoVaTen = $"Khach {suffix}", SoDienThoai = "0987654321", Email = $"khach_{suffix}@test.com", CCCD = $"01234567{suffix[..4]}" };
            var staff = new NguoiDung { TenDangNhap = $"staff_{prefix}_{suffix}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            context.PhongTros.Add(room);
            context.NguoiThues.Add(tenant);
            context.NguoiDungs.Add(staff);
            await context.SaveChangesAsync();

            var tenantUser = new NguoiDung { TenDangNhap = $"tenant_{prefix}_{suffix}", MatKhauHash = "hash", Role = Role.KhachThue, IsActive = true, NguoiThueId = tenant.NguoiThueId };
            context.NguoiDungs.Add(tenantUser);
            context.NhanVienChiNhanhs.Add(new NhanVienChiNhanh { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staff.NguoiDungId });

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

            var invoice = new HoaDon
            {
                MaHoaDon = $"HD-SP-{prefix}-{suffix}",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow,
                HanThanhToan = DateTime.UtcNow.AddDays(7)
            };
            context.HoaDons.Add(invoice);
            await context.SaveChangesAsync();

            return new Seeded(invoice.HoaDonId, staff.NguoiDungId, tenant.NguoiThueId, branch.ChiNhanhId, tenantUser.NguoiDungId);
        }

        private async Task<YeuCauThanhToanHoaDon> SeedRequestAsync(Seeded seeded, decimal soTien, DateTime createdUtc, bool withProof = false)
        {
            await using var context = _fixture.CreateDbContext();
            var code = $"TT{seeded.InvoiceId}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var request = YeuCauThanhToanHoaDon.Tao(seeded.InvoiceId, code, soTien, createdUtc.AddHours(24), seeded.TenantUserId, createdUtc);
            if (withProof)
            {
                request.NopMinhChung("https://example.test/p.jpg", "payment-proofs/p", null, createdUtc, seeded.TenantUserId, createdUtc);
            }

            context.YeuCauThanhToanHoaDons.Add(request);
            await context.SaveChangesAsync();
            return request;
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
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) >= expectedWaitingCount)
                {
                    return;
                }

                await Task.Delay(25);
            }

            throw new TimeoutException($"Hết {timeout.TotalSeconds}s mà số tiến trình bị chặn trên pg_locks chưa đạt {expectedWaitingCount}.");
        }

        // Một kết nối giữ khóa các hóa đơn để hai luồng phải chờ, rồi thả cho cả hai chạy.
        private async Task<T[]> RunBlockedAsync<T>(int[] invoiceIds, Func<InvoiceTransferHandler, Task<T>> first, Func<InvoiceTransferHandler, Task<T>> second)
        {
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            foreach (var invoiceId in invoiceIds)
            {
                await holderCtx.HoaDons.FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {invoiceId} FOR UPDATE").ToListAsync();
            }

            Task<T> Start(Func<InvoiceTransferHandler, Task<T>> action) => Task.Run(async () =>
            {
                await using var ctx = _fixture.CreateDbContext();
                await ctx.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                return await action(CreateHandler(ctx));
            });

            var t1 = Start(first);
            var t2 = Start(second);
            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(5));
            await holderTx.CommitAsync();
            return await Task.WhenAll(t1, t2);
        }

        // ---------- Ghi sổ ----------

        [Fact]
        public async Task FullPayment_PersistsLedgerAndHistory_AndCancelsWaitingRequest()
        {
            var seeded = await SeedSentInvoiceAsync("day_du");
            var request = await SeedRequestAsync(seeded, 1_500_000m, DateTime.UtcNow.AddMinutes(-10));
            var reference = NewReference();

            var outcome = await HandleAsync(Transfer(reference, seeded.InvoiceId), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(request.MaYeuCau, outcome.MaLuotDaHuy);
            Assert.True(outcome.DaThanhToanDu);

            await using var verifyCtx = _fixture.CreateDbContext();
            var ledger = await verifyCtx.LichSuThanhToans.AsNoTracking().SingleAsync(l => l.HoaDonId == seeded.InvoiceId);
            Assert.Equal($"SEPAY-{reference}", ledger.MaGiaoDich);
            Assert.Equal(1_500_000m, ledger.SoTienThanhToan);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, ledger.PhuongThucThanhToan);
            Assert.Null(ledger.NguoiXacNhanId);
            Assert.Null(ledger.MinhChungThanhToanHoaDonId);
            Assert.Equal(TransferTime, ledger.NgayThanhToan);
            Assert.NotNull(ledger.NgayXacNhan);
            Assert.False(ledger.IsDeleted);
            Assert.StartsWith($"[SEPAY] MBBank · REF {reference} · ND: TTPTHD{seeded.InvoiceId}", ledger.GhiChu);
            Assert.Equal(ledger.LichSuThanhToanId, outcome.LichSuThanhToanId);

            var invoice = await verifyCtx.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seeded.InvoiceId);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, invoice.TrangThaiHoaDon);
            var history = await verifyCtx.LichSuTrangThaiHoaDons.AsNoTracking()
                .SingleAsync(l => l.HoaDonId == seeded.InvoiceId && l.TrangThaiThanhToanMoi == TrangThaiHoaDon.DaThanhToan);
            Assert.Equal($"[TU_DONG] SePay ghi nhận 1.500.000đ, REF {reference}", history.LyDo);
            Assert.Null(history.NguoiThucHienId);

            var persistedRequest = await verifyCtx.YeuCauThanhToanHoaDons.AsNoTracking().SingleAsync(y => y.YeuCauThanhToanHoaDonId == request.YeuCauThanhToanHoaDonId);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, persistedRequest.TrangThai);
            var requestHistory = await verifyCtx.LichSuTrangThaiYeuCauThanhToanHoaDons.AsNoTracking()
                .Where(l => l.YeuCauThanhToanHoaDonId == request.YeuCauThanhToanHoaDonId)
                .OrderByDescending(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
                .FirstAsync();
            Assert.Equal("Đã nhận chuyển khoản tự động", requestHistory.LyDo);
            Assert.Null(requestHistory.NguoiThucHienId);
        }

        [Fact]
        public async Task PartialPayment_SetsPartialStatus()
        {
            var seeded = await SeedSentInvoiceAsync("mot_phan");

            var outcome = await HandleAsync(Transfer(NewReference(), seeded.InvoiceId, amount: 500_000m), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(1_000_000m, outcome.ConLai);
            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, (await verifyCtx.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);
            Assert.Equal(500_000m, (await verifyCtx.LichSuThanhToans.AsNoTracking().SingleAsync(l => l.HoaDonId == seeded.InvoiceId)).SoTienThanhToan);
        }

        [Fact]
        public async Task Overpayment_PersistsOnlyTheDebt_AndTagsTheSurplus()
        {
            var seeded = await SeedSentInvoiceAsync("du_tien");

            var outcome = await HandleAsync(Transfer(NewReference(), seeded.InvoiceId, amount: 2_000_000m), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(500_000m, outcome.TienThua);
            await using var verifyCtx = _fixture.CreateDbContext();
            var ledger = await verifyCtx.LichSuThanhToans.AsNoTracking().SingleAsync(l => l.HoaDonId == seeded.InvoiceId);
            Assert.Equal(1_500_000m, ledger.SoTienThanhToan);
            Assert.StartsWith("[TIEN-THUA:500000] [SEPAY]", ledger.GhiChu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await verifyCtx.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);
        }

        // Nhân viên của một chi nhánh khác: không được nhận thông báo của hóa đơn này.
        private async Task<int> SeedOtherBranchStaffAsync()
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var branch = new ChiNhanh { TenChiNhanh = $"CN_khac_{suffix}", MaChiNhanh = TestCodes.MaChiNhanh("K"), DiaChi = "456 Duong Test", SoDienThoai = "0912345678", MoTa = "test" };
            context.ChiNhanhs.Add(branch);
            await context.SaveChangesAsync();

            var staff = new NguoiDung { TenDangNhap = $"staff_khac_{suffix}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            context.NguoiDungs.Add(staff);
            await context.SaveChangesAsync();

            context.NhanVienChiNhanhs.Add(new NhanVienChiNhanh { NguoiDungId = staff.NguoiDungId, ChiNhanhId = branch.ChiNhanhId, IsActive = true, NguoiPhanCongId = staff.NguoiDungId });
            await context.SaveChangesAsync();
            return staff.NguoiDungId;
        }

        [Fact]
        public async Task Recorded_CreatesInAppNotifications_ForBranchStaffAndTenant_ButNotForStaffOfAnotherBranch()
        {
            var seeded = await SeedSentInvoiceAsync("thong_bao");
            var otherBranchStaffId = await SeedOtherBranchStaffAsync();
            var reference = NewReference();

            var outcome = await HandleAsync(Transfer(reference, seeded.InvoiceId), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.Equal(seeded.TenantUserId, outcome.TenantUserId);

            await using var verifyCtx = _fixture.CreateDbContext();
            var staffNote = Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.ActorId).ToListAsync());
            var tenantNote = Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.TenantUserId).ToListAsync());
            Assert.Empty(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == otherBranchStaffId).ToListAsync());

            Assert.StartsWith("Đã nhận chuyển khoản 1.500.000đ — P_", staffNote.TieuDe);
            Assert.Equal(staffNote.TieuDe, tenantNote.TieuDe);
            Assert.Equal("HoaDon", staffNote.LinhVuc);
            Assert.False(staffNote.IsRead);
            Assert.Equal($"/QuanLyNhaTro/QuanLyHoaDon?hoaDonId={seeded.InvoiceId}", staffNote.LinkDieuHuong);
            Assert.Contains($"REF {reference}", staffNote.NoiDung);
            Assert.Contains("đã thu 1.500.000/1.500.000đ, còn lại 0đ", staffNote.NoiDung);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={seeded.InvoiceId}", tenantNote.LinkDieuHuong);
            Assert.EndsWith("đã ghi nhận 1.500.000đ, còn lại 0đ.", tenantNote.NoiDung);
        }

        [Fact]
        public async Task SameTransferSentTwice_CreatesTheNotificationsOnce()
        {
            var seeded = await SeedSentInvoiceAsync("tb_lap");
            var transfer = Transfer(NewReference(), seeded.InvoiceId);

            var first = await HandleAsync(transfer, seeded.InvoiceId);
            var second = await HandleAsync(transfer, seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, first.Kind);
            Assert.Equal(TransferOutcomeKind.Duplicate, second.Kind);
            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.ActorId).ToListAsync());
            Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.TenantUserId).ToListAsync());
        }

        // Lần lưu thứ hai (thông báo, ngay trước commit) ném lỗi: transaction thật phải rollback cả dòng sổ lẫn thông báo.
        private sealed class FailOnSecondSaveUnitOfWork : IUnitOfWork
        {
            private readonly EfUnitOfWork _inner;
            private int _saves;

            public FailOnSecondSaveUnitOfWork(ApplicationDbContext context) => _inner = new EfUnitOfWork(context);

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
                => _inner.BeginTransactionAsync(cancellationToken);

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                if (++_saves == 2)
                {
                    throw new InvalidOperationException("SECRET-DB-DETAIL");
                }

                return _inner.SaveChangesAsync(cancellationToken);
            }
        }

        [Fact]
        public async Task WhenSavingTheNotificationsFails_NeitherTheLedgerRowNorAnyNotificationRemains()
        {
            var seeded = await SeedSentInvoiceAsync("tb_rollback");
            var reference = NewReference();
            TransferOutcome outcome;
            await using (var ctx = _fixture.CreateDbContext())
            {
                var transaction = new InvoicePaymentTransaction(
                    new InvoicePaymentStore(ctx), new FailOnSecondSaveUnitOfWork(ctx), new NoOpNotifier(), NullLogger<InvoicePaymentTransaction>.Instance);
                outcome = await new InvoiceTransferHandler(transaction).HandleAsync(Transfer(reference, seeded.InvoiceId), $"TTPTHD{seeded.InvoiceId}");
            }

            Assert.Equal(TransferOutcomeKind.Failed, outcome.Kind);
            Assert.DoesNotContain("SECRET", outcome.LyDo);
            Assert.Null(outcome.TenantUserId);

            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Empty(await verifyCtx.LichSuThanhToans.AsNoTracking().Where(l => l.HoaDonId == seeded.InvoiceId).ToListAsync());
            Assert.Empty(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.ActorId || t.NguoiDungId == seeded.TenantUserId).ToListAsync());
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await verifyCtx.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);

            // SePay gửi lại sau lỗi: lần này ghi được, thông báo tạo đúng một lần.
            var retried = await HandleAsync(Transfer(reference, seeded.InvoiceId), seeded.InvoiceId);
            Assert.Equal(TransferOutcomeKind.Recorded, retried.Kind);
            await using var afterCtx = _fixture.CreateDbContext();
            Assert.Single(await afterCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.ActorId).ToListAsync());
            Assert.Single(await afterCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.TenantUserId).ToListAsync());
        }

        [Fact]
        public async Task Overpayment_AdminSeesTheSurplusSentence_AndBranchStaffAndTenantDoNot()
        {
            var seeded = await SeedSentInvoiceAsync("tb_du_tien");
            var adminId = await SeedAdminAsync();

            var outcome = await HandleAsync(Transfer(NewReference(), seeded.InvoiceId, amount: 2_000_000m), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            await using var verifyCtx = _fixture.CreateDbContext();
            var admin = Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == adminId).ToListAsync());
            var staff = Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.ActorId).ToListAsync());
            var tenant = Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.TenantUserId).ToListAsync());
            Assert.EndsWith("Khách chuyển dư 500.000đ, cần xử lý ngoài hệ thống.", admin.NoiDung);
            Assert.DoesNotContain("Khách chuyển dư", staff.NoiDung);
            Assert.DoesNotContain("Khách chuyển dư", tenant.NoiDung);
            Assert.StartsWith("Đã nhận chuyển khoản 2.000.000đ", admin.TieuDe);
        }

        [Fact]
        public async Task AnInactiveAdmin_AndAStaffOfAnotherBranch_GetNoNotification()
        {
            var seeded = await SeedSentInvoiceAsync("tb_khong_nhan");
            var inactiveAdminId = await SeedAdminAsync(isActive: false);
            var otherBranchStaffId = await SeedOtherBranchStaffAsync();
            var revokedStaffId = await SeedRevokedStaffAsync(seeded.ChiNhanhId);

            await HandleAsync(Transfer(NewReference(), seeded.InvoiceId), seeded.InvoiceId);

            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Empty(await verifyCtx.ThongBaos.AsNoTracking()
                .Where(t => t.NguoiDungId == inactiveAdminId || t.NguoiDungId == otherBranchStaffId || t.NguoiDungId == revokedStaffId).ToListAsync());
            Assert.Single(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.ActorId).ToListAsync());
        }

        // Nhân viên từng được phân công đúng chi nhánh này nhưng đã bị thu hồi.
        private async Task<int> SeedRevokedStaffAsync(int chiNhanhId)
        {
            await using var context = _fixture.CreateDbContext();
            var staff = new NguoiDung { TenDangNhap = $"staff_thu_hoi_{Guid.NewGuid().ToString("N")[..8]}", MatKhauHash = "hash", Role = Role.NhanVien, IsActive = true };
            context.NguoiDungs.Add(staff);
            await context.SaveChangesAsync();
            context.NhanVienChiNhanhs.Add(new NhanVienChiNhanh
            {
                NguoiDungId = staff.NguoiDungId, ChiNhanhId = chiNhanhId, IsActive = false, NguoiPhanCongId = staff.NguoiDungId, NgayThuHoi = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();
            return staff.NguoiDungId;
        }

        private async Task<int> SeedAdminAsync(bool isActive = true)
        {
            await using var context = _fixture.CreateDbContext();
            var admin = new NguoiDung { TenDangNhap = $"admin_{Guid.NewGuid().ToString("N")[..8]}", MatKhauHash = "hash", Role = Role.Admin, IsActive = isActive };
            context.NguoiDungs.Add(admin);
            await context.SaveChangesAsync();
            return admin.NguoiDungId;
        }

        [Fact]
        public async Task NotPayableAndAlreadyPaid_CreateNoNotifications()
        {
            var seeded = await SeedSentInvoiceAsync("tb_khong_ghi");
            Assert.Equal(TransferOutcomeKind.Recorded, (await HandleAsync(Transfer(NewReference(), seeded.InvoiceId), seeded.InvoiceId)).Kind);
            await using (var ctx = _fixture.CreateDbContext())
            {
                ctx.ThongBaos.RemoveRange(ctx.ThongBaos.Where(t => t.NguoiDungId == seeded.ActorId || t.NguoiDungId == seeded.TenantUserId));
                await ctx.SaveChangesAsync();
            }

            var alreadyPaid = await HandleAsync(Transfer(NewReference(), seeded.InvoiceId), seeded.InvoiceId);
            var missing = await HandleAsync(Transfer(NewReference(), 999_999_999), 999_999_999);

            Assert.Equal(TransferOutcomeKind.AlreadyPaid, alreadyPaid.Kind);
            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, missing.Kind);
            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Empty(await verifyCtx.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seeded.ActorId || t.NguoiDungId == seeded.TenantUserId).ToListAsync());
        }

        [Fact]
        public async Task RequestUnderReview_IsKept_ButTheTransferIsStillRecorded()
        {
            var seeded = await SeedSentInvoiceAsync("doi_chieu");
            var request = await SeedRequestAsync(seeded, 1_500_000m, DateTime.UtcNow.AddMinutes(-10), withProof: true);

            var outcome = await HandleAsync(Transfer(NewReference(), seeded.InvoiceId), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            Assert.True(outcome.ConMinhChungChoDoiChieu);
            Assert.Null(outcome.MaLuotDaHuy);
            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu,
                (await verifyCtx.YeuCauThanhToanHoaDons.AsNoTracking().SingleAsync(y => y.YeuCauThanhToanHoaDonId == request.YeuCauThanhToanHoaDonId)).TrangThai);
        }

        [Fact]
        public async Task CancelledInvoice_IsNotPayable_AndNothingIsWritten()
        {
            var seeded = await SeedSentInvoiceAsync("da_huy");
            await using (var ctx = _fixture.CreateDbContext())
            {
                var invoice = await ctx.HoaDons.SingleAsync(h => h.HoaDonId == seeded.InvoiceId);
                invoice.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaHuy;
                invoice.IsDeleted = true;
                await ctx.SaveChangesAsync();
            }

            var outcome = await HandleAsync(Transfer(NewReference(), seeded.InvoiceId), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal("Hóa đơn đã hủy", outcome.LyDo);
            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.False(await verifyCtx.LichSuThanhToans.AnyAsync(l => l.HoaDonId == seeded.InvoiceId));
        }

        [Fact]
        public async Task MissingInvoice_IsNotPayable()
        {
            const int missingId = 999_999_999;

            var outcome = await HandleAsync(Transfer(NewReference(), missingId), missingId);

            Assert.Equal(TransferOutcomeKind.InvoiceNotPayable, outcome.Kind);
            Assert.Equal("Không tìm thấy hóa đơn", outcome.LyDo);
        }

        // ---------- Chống trùng ----------

        // E05: hai lần ghi song song cùng một giao dịch chỉ ra một dòng sổ.
        [Fact]
        public async Task SameTransfer_Concurrent_IsRecordedOnce()
        {
            var seeded = await SeedSentInvoiceAsync("song_song");
            var transfer = Transfer(NewReference(), seeded.InvoiceId);
            var code = $"TTPTHD{seeded.InvoiceId}";

            var results = await RunBlockedAsync(new[] { seeded.InvoiceId },
                h => h.HandleAsync(transfer, code),
                h => h.HandleAsync(transfer, code));

            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Recorded));
            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Duplicate));

            await using var verifyCtx = _fixture.CreateDbContext();
            var ledger = Assert.Single(await verifyCtx.LichSuThanhToans.Where(l => l.HoaDonId == seeded.InvoiceId).ToListAsync());
            Assert.Equal(transfer.DedupKey, ledger.MaGiaoDich);
            Assert.StartsWith("SEPAY-", ledger.MaGiaoDich);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await verifyCtx.HoaDons.SingleAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);
            Assert.Equal(1, await verifyCtx.LichSuTrangThaiHoaDons.CountAsync(l => l.HoaDonId == seeded.InvoiceId && l.TrangThaiThanhToanMoi == TrangThaiHoaDon.DaThanhToan));
        }

        // Kiểm trùng chỉ trong hóa đơn của giao dịch; cùng khóa trên hóa đơn khác bị unique index chặn và đổi thành Duplicate.
        [Fact]
        public async Task SameTransferKeyOnTwoInvoices_Concurrent_UniqueIndexIsTheLastLineOfDefense()
        {
            var a = await SeedSentInvoiceAsync("khoa_a");
            var b = await SeedSentInvoiceAsync("khoa_b");
            var reference = NewReference();
            var forA = Transfer(reference, a.InvoiceId);
            var forB = Transfer(reference, b.InvoiceId);

            var results = await RunBlockedAsync(new[] { a.InvoiceId, b.InvoiceId },
                h => h.HandleAsync(forA, $"TTPTHD{a.InvoiceId}"),
                h => h.HandleAsync(forB, $"TTPTHD{b.InvoiceId}"));

            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Recorded));
            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Duplicate));

            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyCtx.LichSuThanhToans.CountAsync(l => l.MaGiaoDich == forA.DedupKey));
        }

        [Fact]
        public async Task Store_DuplicateQuery_CountsVoidedRows_OnlyOnTheSameInvoice()
        {
            var a = await SeedSentInvoiceAsync("tr_a");
            var b = await SeedSentInvoiceAsync("tr_b");
            var key = $"SEPAY-X{Guid.NewGuid():N}"[..24];
            await using (var ctx = _fixture.CreateDbContext())
            {
                ctx.LichSuThanhToans.Add(new LichSuThanhToan
                {
                    HoaDonId = a.InvoiceId,
                    MaGiaoDich = key,
                    SoTienThanhToan = 1m,
                    PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
                    IsDeleted = true
                });
                await ctx.SaveChangesAsync();
            }

            await using var queryCtx = _fixture.CreateDbContext();
            var store = new InvoicePaymentStore(queryCtx);

            Assert.True(await store.ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(a.InvoiceId, key.ToLowerInvariant()));
            Assert.True(await store.ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(a.InvoiceId, $"  {key}  "));
            Assert.False(await store.ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(b.InvoiceId, key));
            // Truy vấn có sẵn không đếm dòng đã xóa mềm.
            Assert.False(await store.ExistsMaGiaoDichAsync(key));
        }

        [Fact]
        public async Task VoidedLedgerRow_StillBlocksTheSameTransfer()
        {
            var seeded = await SeedSentInvoiceAsync("da_huy_ghi");
            var transfer = Transfer(NewReference(), seeded.InvoiceId);
            await using (var ctx = _fixture.CreateDbContext())
            {
                ctx.LichSuThanhToans.Add(new LichSuThanhToan
                {
                    HoaDonId = seeded.InvoiceId,
                    MaGiaoDich = transfer.DedupKey,
                    SoTienThanhToan = 1_500_000m,
                    PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
                    IsDeleted = true
                });
                await ctx.SaveChangesAsync();
            }

            var outcome = await HandleAsync(transfer, seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyCtx.LichSuThanhToans.CountAsync(l => l.HoaDonId == seeded.InvoiceId));
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await verifyCtx.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);
        }
    }
}
