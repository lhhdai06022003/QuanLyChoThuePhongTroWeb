using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Notifications;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Thanh toán hóa đơn trên PostgreSQL thật: khóa dòng hoa_don tuần tự hóa các luồng ghi (spec §28),
    // unique index là lớp chặn cuối, và các truy vấn của InvoicePaymentStore dịch được sang SQL.
    [Collection("PostgreSqlCollection")]
    public class InvoicePaymentConcurrencyTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoicePaymentConcurrencyTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private class AllowAllEmployeeAccessService : IEmployeeAccessService
        {
            public Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            public Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default)
                => Task.FromResult<EmployeeAccessScope?>(new EmployeeAccessScope(actorId, isAdmin: true));
        }

        private class NoOpNotifier : IThongBaoNotifier
        {
            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class FakeStorage : IMeterImageStorageService
        {
            public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
                => Task.FromResult(new MeterImageUploadResult($"https://example.test/{folder}/{file.FileName}", $"{folder}/{Guid.NewGuid():N}"));
            public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
                => Task.FromResult(new MeterImageReadResult(new byte[] { 1 }, "image/jpeg"));
            public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class FakeVietQr : IVietQRService
        {
            public string GenerateVietQRString(string bankIdOrBin, string accountNumber, decimal amount, string memo) => "QR";
            public byte[] GenerateQRCodePNGBytes(string payload) => new byte[] { 9 };
        }

        private sealed record Services(InvoiceLedgerService Ledger, InvoicePaymentRequestService Requests, InvoicePaymentConfirmationService Confirmation);

        private static Services CreateServices(ApplicationDbContext ctx)
        {
            var store = new InvoicePaymentStore(ctx);
            var options = new InvoicePaymentOptions();
            var access = new AllowAllEmployeeAccessService();
            var codes = new PaymentCodeGenerator();
            var storage = new FakeStorage();
            var transaction = new InvoicePaymentTransaction(store, new EfUnitOfWork(ctx), new NoOpNotifier(), NullLogger<InvoicePaymentTransaction>.Instance);
            return new Services(
                new InvoiceLedgerService(store, transaction, access, codes, options),
                new InvoicePaymentRequestService(store, transaction, codes, storage, new FakeVietQr(), new VietQrSettings(), options, NullLogger<InvoicePaymentRequestService>.Instance),
                new InvoicePaymentConfirmationService(store, transaction, access, storage, options, NullLogger<InvoicePaymentConfirmationService>.Instance));
        }

        private sealed record Seeded(int InvoiceId, int ActorId, int NguoiThueId, int ChiNhanhId, int TenantUserId);

        // Chi nhánh, phòng, người thuê (có tài khoản), hợp đồng, một nhân viên phân công chi nhánh và một hóa đơn DaGui.
        private async Task<Seeded> SeedSentInvoiceAsync(string prefix, decimal tongTien = 1_500_000m, bool choPhepMotPhan = false, TrangThaiHopDong trangThaiHopDong = TrangThaiHopDong.DangHoatDong)
        {
            await using var context = _fixture.CreateDbContext();
            var suffix = Guid.NewGuid().ToString("N")[..6];

            var branch = new ChiNhanh { TenChiNhanh = $"CN_{prefix}_{suffix}", MaChiNhanh = $"C{suffix}", DiaChi = "123 Duong Test", SoDienThoai = "0912345678", MoTa = "test" };
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
                TrangThaiHopDong = trangThaiHopDong
            };
            context.HopDongs.Add(contract);
            await context.SaveChangesAsync();

            var invoice = new HoaDon
            {
                MaHoaDon = $"HD-PAY-{prefix}-{suffix}",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = tongTien,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow,
                HanThanhToan = DateTime.UtcNow.AddDays(7),
                ChoPhepThanhToanMotPhan = choPhepMotPhan,
                SoTienThanhToanToiThieu = choPhepMotPhan ? 100_000m : null
            };
            context.HoaDons.Add(invoice);
            await context.SaveChangesAsync();

            return new Seeded(invoice.HoaDonId, staff.NguoiDungId, tenant.NguoiThueId, branch.ChiNhanhId, tenantUser.NguoiDungId);
        }

        private async Task<YeuCauThanhToanHoaDon> SeedRequestAsync(Seeded seeded, decimal soTien, DateTime createdUtc, string? maGiaoDichProof = null, bool withProof = false)
        {
            await using var context = _fixture.CreateDbContext();
            var code = $"TT{seeded.InvoiceId}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var request = YeuCauThanhToanHoaDon.Tao(seeded.InvoiceId, code, soTien, createdUtc.AddHours(24), seeded.TenantUserId, createdUtc);
            if (withProof)
            {
                request.NopMinhChung("https://example.test/p.jpg", "payment-proofs/p", maGiaoDichProof, createdUtc, seeded.TenantUserId, createdUtc);
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
        private async Task<T[]> RunBlockedAsync<T>(int[] invoiceIds, Func<Services, Task<T>> first, Func<Services, Task<T>> second)
        {
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            foreach (var invoiceId in invoiceIds)
            {
                await holderCtx.HoaDons.FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {invoiceId} FOR UPDATE").ToListAsync();
            }

            Task<T> Start(Func<Services, Task<T>> action) => Task.Run(async () =>
            {
                await using var ctx = _fixture.CreateDbContext();
                await ctx.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                return await action(CreateServices(ctx));
            });

            var t1 = Start(first);
            var t2 = Start(second);
            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(5));
            await holderTx.CommitAsync();
            return await Task.WhenAll(t1, t2);
        }

        private static ManualCollectionRequest Cash(int hoaDonId, decimal soTien) => new()
        {
            HoaDonId = hoaDonId,
            SoTienThucNhan = soTien,
            PhuongThuc = AppPhuongThucThanhToan.TienMat,
            NgayThanhToanUtc = DateTime.UtcNow.AddMinutes(-1)
        };

        [Fact]
        public async Task CollectAndCancel_Concurrent_OnlyOneSucceeds_NoCancelledInvoiceWithPayment()
        {
            var seeded = await SeedSentInvoiceAsync("thu_huy");

            var results = await RunBlockedAsync<ServiceResult>(new[] { seeded.InvoiceId },
                s => s.Ledger.CollectManuallyAsync(Cash(seeded.InvoiceId, 1_500_000m), seeded.ActorId).ContinueWith(t => (ServiceResult)t.Result),
                s => s.Ledger.CancelInvoiceAsync(seeded.InvoiceId, seeded.ActorId, "Hủy kiểm thử").ContinueWith(t => (ServiceResult)t.Result));

            Assert.Equal(1, results.Count(r => r.Success));

            await using var verifyCtx = _fixture.CreateDbContext();
            var persisted = await verifyCtx.HoaDons.FirstAsync(h => h.HoaDonId == seeded.InvoiceId);
            var paymentCount = await verifyCtx.LichSuThanhToans.CountAsync(l => l.HoaDonId == seeded.InvoiceId);
            if (persisted.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                Assert.Equal(0, paymentCount);
            }
            else
            {
                Assert.Equal(TrangThaiHoaDon.DaThanhToan, persisted.TrangThaiHoaDon);
                Assert.Equal(1, paymentCount);
                Assert.False(persisted.IsDeleted);
            }
        }

        [Fact]
        public async Task TwoFullCollections_Concurrent_CreateSingleLedgerEntry_AndHistory()
        {
            var seeded = await SeedSentInvoiceAsync("thu_thu");

            var results = await RunBlockedAsync(new[] { seeded.InvoiceId },
                s => s.Ledger.CollectManuallyAsync(Cash(seeded.InvoiceId, 1_500_000m), seeded.ActorId),
                s => s.Ledger.CollectManuallyAsync(Cash(seeded.InvoiceId, 1_500_000m), seeded.ActorId));

            Assert.Equal(1, results.Count(r => r.Success));
            Assert.Equal("Hóa đơn này đã được thanh toán đủ.", results.Single(r => !r.Success).Message);

            await using var verifyCtx = _fixture.CreateDbContext();
            var payment = Assert.Single(await verifyCtx.LichSuThanhToans.Where(l => l.HoaDonId == seeded.InvoiceId).ToListAsync());
            Assert.Equal(1_500_000m, payment.SoTienThanhToan);
            Assert.StartsWith("TM-", payment.MaGiaoDich);
            Assert.Equal(1, await verifyCtx.LichSuTrangThaiHoaDons.CountAsync(l => l.HoaDonId == seeded.InvoiceId && l.TrangThaiThanhToanMoi == TrangThaiHoaDon.DaThanhToan));
        }

        [Fact]
        public async Task TwoRequestCreations_Concurrent_CreateSingleRequest()
        {
            var seeded = await SeedSentInvoiceAsync("tao_luot");

            var results = await RunBlockedAsync(new[] { seeded.InvoiceId },
                s => s.Requests.CreateAsync(seeded.InvoiceId, seeded.NguoiThueId, seeded.TenantUserId, null),
                s => s.Requests.CreateAsync(seeded.InvoiceId, seeded.NguoiThueId, seeded.TenantUserId, null));

            Assert.Equal(1, results.Count(r => r.Success));

            await using var verifyCtx = _fixture.CreateDbContext();
            var request = Assert.Single(await verifyCtx.YeuCauThanhToanHoaDons.Where(y => y.HoaDonId == seeded.InvoiceId).ToListAsync());
            Assert.Equal(request.MaYeuCau, request.NoiDungChuyenKhoan);
            Assert.Equal(1_500_000m, request.SoTien);
            Assert.Equal(1, await verifyCtx.LichSuTrangThaiYeuCauThanhToanHoaDons.CountAsync(l => l.YeuCauThanhToanHoaDonId == request.YeuCauThanhToanHoaDonId));
        }

        // Khách đã trả phòng vẫn thanh toán được hóa đơn của mình (spec §4.1, nghiệm thu 12).
        [Fact]
        public async Task TerminatedContract_TenantCanStillCreateRequest()
        {
            var seeded = await SeedSentInvoiceAsync("thanh_ly", trangThaiHopDong: TrangThaiHopDong.DaKetThuc);

            await using var ctx = _fixture.CreateDbContext();
            var result = await CreateServices(ctx).Requests.CreateAsync(seeded.InvoiceId, seeded.NguoiThueId, seeded.TenantUserId, null);

            Assert.True(result.Success, result.Message);
        }

        [Fact]
        public async Task ConfirmAndManualCollection_Concurrent_NeverExceedTotal()
        {
            var seeded = await SeedSentInvoiceAsync("xn_thu", choPhepMotPhan: true);
            var request = await SeedRequestAsync(seeded, 1_000_000m, DateTime.UtcNow.AddMinutes(-30), withProof: true);
            var proofId = request.MinhChungThanhToanHoaDons.Single().MinhChungThanhToanHoaDonId;

            // Thu thủ công bị chặn khi có lượt đang đối chiếu; xác nhận 1.000.000 qua; tổng không vượt 1.500.000.
            var results = await RunBlockedAsync<ServiceResult>(new[] { seeded.InvoiceId },
                s => s.Confirmation.ConfirmAsync(new ConfirmPaymentRequest { MinhChungId = proofId, SoTienThucNhan = 1_000_000m, NgayGiaoDichUtc = DateTime.UtcNow.AddMinutes(-5) }, seeded.ActorId).ContinueWith(t => (ServiceResult)t.Result),
                s => s.Ledger.CollectManuallyAsync(Cash(seeded.InvoiceId, 1_500_000m), seeded.ActorId).ContinueWith(t => (ServiceResult)t.Result));

            Assert.True(results[0].Success, results[0].Message);
            Assert.False(results[1].Success);

            await using var verifyCtx = _fixture.CreateDbContext();
            var total = await verifyCtx.LichSuThanhToans.Where(l => l.HoaDonId == seeded.InvoiceId && !l.IsDeleted).SumAsync(l => l.SoTienThanhToan);
            var invoice = await verifyCtx.HoaDons.FirstAsync(h => h.HoaDonId == seeded.InvoiceId);
            Assert.Equal(1_000_000m, total);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, invoice.TrangThaiHoaDon);
        }

        [Fact]
        public async Task SameBankCode_OnTwoInvoices_Concurrent_OnlyOneRecorded()
        {
            var a = await SeedSentInvoiceAsync("ma_a");
            var b = await SeedSentInvoiceAsync("ma_b");
            var bankCode = $"FT{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

            ManualCollectionRequest Transfer(int hoaDonId) => new()
            {
                HoaDonId = hoaDonId,
                SoTienThucNhan = 1_500_000m,
                PhuongThuc = AppPhuongThucThanhToan.ChuyenKhoan,
                NgayThanhToanUtc = DateTime.UtcNow.AddMinutes(-1),
                MaGiaoDich = bankCode
            };

            var results = await RunBlockedAsync(new[] { a.InvoiceId, b.InvoiceId },
                s => s.Ledger.CollectManuallyAsync(Transfer(a.InvoiceId), a.ActorId),
                s => s.Ledger.CollectManuallyAsync(Transfer(b.InvoiceId), b.ActorId));

            Assert.Equal(1, results.Count(r => r.Success));
            Assert.Equal("Mã giao dịch đã được ghi nhận.", results.Single(r => !r.Success).Message);

            await using var verifyCtx = _fixture.CreateDbContext();
            Assert.Equal(1, await verifyCtx.LichSuThanhToans.CountAsync(l => l.MaGiaoDich == bankCode));
        }

        [Fact]
        public async Task UniqueViolation_IsTranslated_AndTransactionNotReused()
        {
            var seeded = await SeedSentInvoiceAsync("unique");
            var dupCode = $"DUP-{Guid.NewGuid().ToString("N")[..10]}";
            await using (var ctx = _fixture.CreateDbContext())
            {
                ctx.LichSuThanhToans.Add(new LichSuThanhToan { HoaDonId = seeded.InvoiceId, MaGiaoDich = dupCode, SoTienThanhToan = 1m, PhuongThucThanhToan = PhuongThucThanhToan.TienMat });
                await ctx.SaveChangesAsync();
            }

            await using var context = _fixture.CreateDbContext();
            var transaction = new InvoicePaymentTransaction(new InvoicePaymentStore(context), new EfUnitOfWork(context), new NoOpNotifier(), NullLogger<InvoicePaymentTransaction>.Instance);

            // Bỏ qua kiểm tra trùng ở Application để chạm thẳng unique index.
            var result = await transaction.RunAsync(seeded.InvoiceId, seeded.ActorId, async scope =>
            {
                scope.Store.AddLedgerEntry(new LichSuThanhToan { HoaDonId = seeded.InvoiceId, MaGiaoDich = dupCode, SoTienThanhToan = 1m, PhuongThucThanhToan = PhuongThucThanhToan.TienMat });
                await scope.ApplyLedgerChangeAsync("trùng");
                return ServiceResult<bool>.Ok(true);
            });

            Assert.False(result.Success);
            Assert.Equal("Mã giao dịch đã được ghi nhận.", result.Message);
            Assert.Equal(1, await context.LichSuThanhToans.AsNoTracking().CountAsync(l => l.MaGiaoDich == dupCode));
        }

        [Fact]
        public async Task ExpiredRequest_IsPersistedAsHetHan_EvenWhenOperationFails()
        {
            var seeded = await SeedSentInvoiceAsync("het_han");
            var request = await SeedRequestAsync(seeded, 1_500_000m, DateTime.UtcNow.AddHours(-30));

            await using (var ctx = _fixture.CreateDbContext())
            {
                var result = await CreateServices(ctx).Requests.CancelAsync(request.YeuCauThanhToanHoaDonId, seeded.NguoiThueId, seeded.TenantUserId);
                Assert.False(result.Success);
                Assert.Contains("hết hạn", result.Message);
            }

            await using var verifyCtx = _fixture.CreateDbContext();
            var persisted = await verifyCtx.YeuCauThanhToanHoaDons.FirstAsync(y => y.YeuCauThanhToanHoaDonId == request.YeuCauThanhToanHoaDonId);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, persisted.TrangThai);
            var history = await verifyCtx.LichSuTrangThaiYeuCauThanhToanHoaDons
                .Where(l => l.YeuCauThanhToanHoaDonId == request.YeuCauThanhToanHoaDonId)
                .OrderByDescending(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
                .FirstAsync();
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, history.TrangThaiMoi);
            Assert.Null(history.NguoiThucHienId);
        }

        [Fact]
        public async Task SubmitConfirmFlow_PersistsProofLedgerAndNotifications()
        {
            var seeded = await SeedSentInvoiceAsync("tron_luong");
            int yeuCauId;
            await using (var ctx = _fixture.CreateDbContext())
            {
                var created = await CreateServices(ctx).Requests.CreateAsync(seeded.InvoiceId, seeded.NguoiThueId, seeded.TenantUserId, null);
                Assert.True(created.Success, created.Message);
                yeuCauId = created.Data!.YeuCauId;
            }

            await using (var ctx = _fixture.CreateDbContext())
            {
                var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 };
                var submitted = await CreateServices(ctx).Requests.SubmitProofAsync(new SubmitPaymentProofRequest
                {
                    YeuCauId = yeuCauId,
                    File = new UploadFile(new MemoryStream(bytes), "a.jpg", "image/jpeg", bytes.Length),
                    NgayChuyenUtc = DateTime.UtcNow.AddMinutes(-3)
                }, seeded.NguoiThueId, seeded.TenantUserId);
                Assert.True(submitted.Success, submitted.Message);
            }

            await using (var verifyCtx = _fixture.CreateDbContext())
            {
                var proof = await verifyCtx.MinhChungThanhToanHoaDons.SingleAsync(m => m.YeuCauThanhToanHoaDonId == yeuCauId);
                Assert.StartsWith("payment-proofs/", proof.PublicId);
                // Nhân viên được phân công nhận thông báo; Admin (nếu có trong DB) cũng nhận.
                Assert.True(await verifyCtx.ThongBaos.AnyAsync(t => t.NguoiDungId == seeded.ActorId && t.LinhVuc == "HoaDon"));

                await using var ctx = _fixture.CreateDbContext();
                var confirmed = await CreateServices(ctx).Confirmation.ConfirmAsync(new ConfirmPaymentRequest
                {
                    MinhChungId = proof.MinhChungThanhToanHoaDonId,
                    SoTienThucNhan = 1_500_000m,
                    NgayGiaoDichUtc = DateTime.UtcNow.AddMinutes(-2)
                }, seeded.ActorId);
                Assert.True(confirmed.Success, confirmed.Message);
            }

            await using var finalCtx = _fixture.CreateDbContext();
            var ledger = await finalCtx.LichSuThanhToans.SingleAsync(l => l.HoaDonId == seeded.InvoiceId);
            Assert.NotNull(ledger.MinhChungThanhToanHoaDonId);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, (await finalCtx.HoaDons.FirstAsync(h => h.HoaDonId == seeded.InvoiceId)).TrangThaiHoaDon);
            Assert.True(await finalCtx.ThongBaos.AnyAsync(t => t.NguoiDungId == seeded.TenantUserId));
        }

        [Fact]
        public async Task Store_QueriesTranslate_ActivePredicate_Lookup_WaitingAdminQueue()
        {
            var seeded = await SeedSentInvoiceAsync("store_q");
            var request = await SeedRequestAsync(seeded, 1_500_000m, DateTime.UtcNow.AddMinutes(-10), withProof: true, maGiaoDichProof: "FT-Q1");
            await using (var ctx = _fixture.CreateDbContext())
            {
                var tracked = await ctx.YeuCauThanhToanHoaDons
                    .Include(y => y.LichSuTrangThaiYeuCauThanhToanHoaDons)
                    .FirstAsync(y => y.YeuCauThanhToanHoaDonId == request.YeuCauThanhToanHoaDonId);
                tracked.GhiChuyenAdmin(seeded.ActorId, DateTime.UtcNow, PaymentNoteTags.ChoAdmin(2_000_000m, 1_500_000m, "dư"));
                await ctx.SaveChangesAsync();
            }

            await using var queryCtx = _fixture.CreateDbContext();
            var store = new InvoicePaymentStore(queryCtx);
            var now = DateTime.UtcNow;

            Assert.NotNull(await store.GetActiveRequestAsync(seeded.InvoiceId, now));
            var target = await store.GetTargetByMaYeuCauAsync($"  {request.MaYeuCau.ToLowerInvariant()} ");
            Assert.Equal(seeded.InvoiceId, target!.HoaDonId);
            Assert.Equal(seeded.ChiNhanhId, target.ChiNhanhId);
            Assert.True(await store.ExistsPendingProofMaGiaoDichAsync("ft-q1", null));

            var waiting = await store.GetReviewQueueAsync(new ReviewQueueQuery { ChiNhanhId = seeded.ChiNhanhId, ChiChoAdmin = true, Length = 10 });
            var row = Assert.Single(waiting.Rows);
            Assert.True(row.ChoAdmin);
            Assert.Equal(request.MaYeuCau, row.MaYeuCau);

            var otherBranch = await store.GetReviewQueueAsync(new ReviewQueueQuery { AllowedBranchIds = new[] { -1 }, Length = 10 });
            Assert.Empty(otherBranch.Rows);

            var reviewers = await store.GetReviewerUserIdsAsync(seeded.ChiNhanhId);
            Assert.Contains(seeded.ActorId, reviewers);
            Assert.Equal(seeded.TenantUserId, await store.GetTenantUserIdAsync((await queryCtx.HoaDons.FirstAsync(h => h.HoaDonId == seeded.InvoiceId)).HopDongId));

            var snapshots = await store.GetRequestSnapshotsAsync(seeded.InvoiceId);
            Assert.Equal(3, Assert.Single(snapshots).LichSu.Count);
        }
    }
}
