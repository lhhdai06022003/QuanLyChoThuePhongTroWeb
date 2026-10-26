using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
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
    // Ghi sổ hóa đơn từ IPN VNPay trên PostgreSQL thật (spec VNPay §6, §6.1): chống trùng theo khóa VNPAY-{vnp_TransactionNo},
    // dòng xóa mềm vẫn chặn gọi lại, truy vấn dòng hệ thống ghi sau mốc và nhãn VNPay ở chi tiết hóa đơn.
    [Collection("PostgreSqlCollection")]
    public class VnPayLedgerTests
    {
        private static readonly DateTime PayTime = new(2026, 10, 3, 2, 30, 0, DateTimeKind.Utc);

        private readonly PostgreSqlFixture _fixture;

        public VnPayLedgerTests(PostgreSqlFixture fixture)
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

        private static string NewTransactionNo() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        private static IncomingBankTransfer Transfer(string transactionNo, int hoaDonId, decimal amount = 1_500_000m) => new(
            0, "NCB", PayTime, null, null, null, $"Thanh toan hoa don {hoaDonId}", "in", amount, transactionNo, $"HD{hoaDonId}_638654321098765432")
        {
            Kenh = OnlinePaymentChannel.VnPay,
            CardType = "ATM"
        };

        private async Task<TransferOutcome> HandleAsync(IncomingBankTransfer transfer, int hoaDonId)
        {
            await using var ctx = _fixture.CreateDbContext();
            return await CreateHandler(ctx).HandleAsync(transfer, InvoiceTransferHandler.TransferCode(hoaDonId));
        }

        private sealed record Seeded(int InvoiceId, int StaffId);

        // Chi nhánh, phòng, người thuê, hợp đồng, một nhân viên và một hóa đơn DaGui.
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
                MaHoaDon = $"HD-VP-{prefix}-{suffix}",
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

            return new Seeded(invoice.HoaDonId, staff.NguoiDungId);
        }

        private async Task AddLedgerAsync(
            int hoaDonId, string maGiaoDich, DateTime? ngayXacNhan, bool isDeleted = false, int? nguoiXacNhanId = null)
        {
            await using var ctx = _fixture.CreateDbContext();
            ctx.LichSuThanhToans.Add(new LichSuThanhToan
            {
                HoaDonId = hoaDonId,
                MaGiaoDich = maGiaoDich,
                SoTienThanhToan = 1m,
                PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
                NgayThanhToan = PayTime,
                NgayXacNhan = ngayXacNhan,
                NguoiXacNhanId = nguoiXacNhanId,
                IsDeleted = isDeleted
            });
            await ctx.SaveChangesAsync();
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
        public async Task IpnTransfer_PersistsTheVnPayLedgerRow_AndPaysTheInvoice()
        {
            var seeded = await SeedSentInvoiceAsync("ghi_so");
            var transactionNo = NewTransactionNo();

            var outcome = await HandleAsync(Transfer(transactionNo, seeded.InvoiceId), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Recorded, outcome.Kind);
            await using var verifyCtx = _fixture.CreateDbContext();
            var ledger = await verifyCtx.LichSuThanhToans.AsNoTracking().SingleAsync(l => l.HoaDonId == seeded.InvoiceId);
            Assert.Equal($"VNPAY-{transactionNo}", ledger.MaGiaoDich);
            Assert.Equal(1_500_000m, ledger.SoTienThanhToan);
            Assert.Equal(PhuongThucThanhToan.ChuyenKhoan, ledger.PhuongThucThanhToan);
            Assert.Null(ledger.NguoiXacNhanId);
            Assert.Equal(PayTime, ledger.NgayThanhToan);
            Assert.StartsWith($"[VNPAY] NCB · ATM · TXN {transactionNo} · REF HD{seeded.InvoiceId}_", ledger.GhiChu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await verifyCtx.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);
            var history = await verifyCtx.LichSuTrangThaiHoaDons.AsNoTracking()
                .SingleAsync(l => l.HoaDonId == seeded.InvoiceId && l.TrangThaiThanhToanMoi == TrangThaiHoaDon.DaThanhToan);
            Assert.Equal($"[TU_DONG] VNPay ghi nhận 1.500.000đ, TXN {transactionNo}", history.LyDo);
        }

        // ---------- Chống trùng ----------

        [Fact]
        public async Task SameTransactionNumber_Concurrent_IsRecordedOnce()
        {
            var seeded = await SeedSentInvoiceAsync("song_song");
            var transfer = Transfer(NewTransactionNo(), seeded.InvoiceId);
            var code = InvoiceTransferHandler.TransferCode(seeded.InvoiceId);

            var results = await RunBlockedAsync(new[] { seeded.InvoiceId },
                h => h.HandleAsync(transfer, code),
                h => h.HandleAsync(transfer, code));

            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Recorded));
            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Duplicate));

            await using var verifyCtx = _fixture.CreateDbContext();
            var ledger = Assert.Single(await verifyCtx.LichSuThanhToans.Where(l => l.HoaDonId == seeded.InvoiceId).ToListAsync());
            Assert.Equal(transfer.DedupKey, ledger.MaGiaoDich);
            Assert.StartsWith("VNPAY-", ledger.MaGiaoDich);
        }

        [Fact]
        public async Task SameTransactionNumberOnTwoInvoices_Concurrent_UniqueIndexIsTheLastLineOfDefense()
        {
            var a = await SeedSentInvoiceAsync("khoa_a");
            var b = await SeedSentInvoiceAsync("khoa_b");
            var transactionNo = NewTransactionNo();
            var forA = Transfer(transactionNo, a.InvoiceId);
            var forB = Transfer(transactionNo, b.InvoiceId);

            var results = await RunBlockedAsync(new[] { a.InvoiceId, b.InvoiceId },
                h => h.HandleAsync(forA, InvoiceTransferHandler.TransferCode(a.InvoiceId)),
                h => h.HandleAsync(forB, InvoiceTransferHandler.TransferCode(b.InvoiceId)));

            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Recorded));
            Assert.Equal(1, results.Count(r => r.Kind == TransferOutcomeKind.Duplicate));

            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyCtx.LichSuThanhToans.CountAsync(l => l.MaGiaoDich == forA.DedupKey));
        }

        [Fact]
        public async Task VoidedVnPayRow_StillBlocksTheSameTransaction_ButNotTheUndeletedQuery()
        {
            var seeded = await SeedSentInvoiceAsync("da_huy");
            var transactionNo = NewTransactionNo();
            var key = InvoicePaymentPolicy.VnPayLedgerKey(transactionNo);
            await AddLedgerAsync(seeded.InvoiceId, key, PayTime, isDeleted: true);

            await using (var queryCtx = _fixture.CreateDbContext())
            {
                var store = new InvoicePaymentStore(queryCtx);
                Assert.True(await store.ExistsMaGiaoDichForInvoiceIncludingDeletedAsync(seeded.InvoiceId, key));
                Assert.False(await store.ExistsMaGiaoDichAsync(key));
            }

            var outcome = await HandleAsync(Transfer(transactionNo, seeded.InvoiceId), seeded.InvoiceId);

            Assert.Equal(TransferOutcomeKind.Duplicate, outcome.Kind);
            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyCtx.LichSuThanhToans.CountAsync(l => l.HoaDonId == seeded.InvoiceId));
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, (await verifyCtx.HoaDons.AsNoTracking().SingleAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);
        }

        // ---------- Dòng hệ thống ghi sau mốc ----------

        [Fact]
        public async Task FindSystemEntryRecordedAfter_ReturnsAVnPayRow_ButNotVoidedOrPersonConfirmedOnes()
        {
            var seeded = await SeedSentInvoiceAsync("sau_moc");
            var since = PayTime;
            var voided = $"VNPAY-HUY{NewTransactionNo()}";
            var byPerson = $"VNPAY-NGUOI{NewTransactionNo()}";
            var system = $"VNPAY-HT{NewTransactionNo()}";
            await AddLedgerAsync(seeded.InvoiceId, voided, since.AddHours(1), isDeleted: true);
            await AddLedgerAsync(seeded.InvoiceId, byPerson, since.AddHours(1), nguoiXacNhanId: seeded.StaffId);

            await using (var ctx = _fixture.CreateDbContext())
            {
                Assert.Null(await new InvoicePaymentStore(ctx).FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since));
            }

            await AddLedgerAsync(seeded.InvoiceId, system, since.AddHours(2));

            await using var verifyCtx = _fixture.CreateDbContext();
            var store = new InvoicePaymentStore(verifyCtx);
            Assert.Equal(system, await store.FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since));
            Assert.Null(await store.FindSystemSePayEntryRecordedAfterAsync(seeded.InvoiceId, since.AddHours(3)));
        }

        // ---------- Chi tiết hóa đơn ----------

        [Fact]
        public async Task InvoiceDetail_MarksVnPayRows_WithTheTransactionNumber_AndKeepsSePayRowsApart()
        {
            var seeded = await SeedSentInvoiceAsync("chi_tiet", tongTien: 3_000_000m);
            var transactionNo = NewTransactionNo();
            var sePayRef = "FT" + NewTransactionNo();
            await AddLedgerAsync(seeded.InvoiceId, $"VNPAY-{transactionNo}", PayTime);
            await AddLedgerAsync(seeded.InvoiceId, $"SEPAY-{sePayRef}", PayTime);
            await AddLedgerAsync(seeded.InvoiceId, $"TM-{NewTransactionNo()}", PayTime);

            await using var ctx = _fixture.CreateDbContext();
            var detail = await new HoaDonStore(ctx).GetHoaDonDetailByIdAsync(seeded.InvoiceId);

            Assert.NotNull(detail);
            var vnPay = Assert.Single(detail!.LichSuThanhToans, l => l.MaGiaoDich == $"VNPAY-{transactionNo}");
            Assert.True(vnPay.LaVnPay);
            Assert.False(vnPay.LaSePay);
            Assert.Equal(transactionNo, vnPay.MaThamChieu);

            var sePay = Assert.Single(detail.LichSuThanhToans, l => l.MaGiaoDich == $"SEPAY-{sePayRef}");
            Assert.False(sePay.LaVnPay);
            Assert.True(sePay.LaSePay);
            Assert.Equal(sePayRef, sePay.MaThamChieu);

            var manual = Assert.Single(detail.LichSuThanhToans, l => l.MaGiaoDich.StartsWith("TM-"));
            Assert.False(manual.LaVnPay);
            Assert.False(manual.LaSePay);
            Assert.Null(manual.MaThamChieu);
        }
    }
}
