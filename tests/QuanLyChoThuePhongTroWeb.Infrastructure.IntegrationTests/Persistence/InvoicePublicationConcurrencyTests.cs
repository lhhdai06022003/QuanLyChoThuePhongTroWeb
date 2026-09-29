using System;
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
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    [Collection("PostgreSqlCollection")]
    public class InvoicePublicationConcurrencyTests
    {
        private readonly PostgreSqlFixture _fixture;

        public InvoicePublicationConcurrencyTests(PostgreSqlFixture fixture)
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

        private class CountingFakeEmailService : IEmailService
        {
            public int SendCount;
            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                Interlocked.Increment(ref SendCount);
                return Task.FromResult((true, string.Empty));
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.ContractExpiryAlertData alertData)
                => Task.FromResult((true, string.Empty));
        }

        /// <summary>
        /// Task 6.5: lần gọi gửi email đầu tiên ném exception, các lần sau thành công.
        /// Dùng để kiểm chứng lỗi email không làm hỏng công bố của hóa đơn khác trong lô.
        /// </summary>
        private class FirstCallThrowsEmailService : IEmailService
        {
            private int _callCount;
            public int CallCount => _callCount;

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                var current = Interlocked.Increment(ref _callCount);
                if (current == 1)
                {
                    throw new InvalidOperationException("Mô phỏng lỗi gửi email lần đầu.");
                }
                return Task.FromResult((true, string.Empty));
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.ContractExpiryAlertData alertData)
                => Task.FromResult((true, string.Empty));
        }

        private class NoOpThongBaoNotifier : IThongBaoNotifier
        {
            public Task SendToUserAsync(int nguoiDungId, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SendToRoleGroupAsync(string roleName, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class FakeDocumentExporter : IInvoiceDocumentExporter
        {
            public byte[] ExportExcel(HoaDonChiTietRes hoaDon) => new byte[] { 1 };
            public byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName) => new byte[] { 1, 2, 3 };
        }

        private class FakeVietQRService : IVietQRService
        {
            public string GenerateVietQRString(string bankId, string accountNumber, decimal amount, string memo) => "QR";
            public byte[] GenerateQRCodePNGBytes(string qrString) => new byte[] { 9 };
        }

        /// <summary>
        /// Bọc IUnitOfWork thật để mô phỏng SaveChangesAsync lỗi ở đúng lần gọi thứ N,
        /// dùng để kiểm chứng quyết định (B): InvoicePublicationStore.LockInvoiceAsync gọi
        /// ChangeTracker.Clear() nên hóa đơn lỗi giữa lô không làm hỏng các hóa đơn công bố sau.
        /// </summary>
        private class ThrowOnNthSaveUnitOfWork : IUnitOfWork
        {
            private readonly IUnitOfWork _inner;
            private int _saveCount;
            public int ThrowOnSaveNumber { get; set; }

            public ThrowOnNthSaveUnitOfWork(IUnitOfWork inner) => _inner = inner;

            public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
                => _inner.BeginTransactionAsync(cancellationToken);

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                var current = Interlocked.Increment(ref _saveCount);
                if (current == ThrowOnSaveNumber)
                {
                    throw new InvalidOperationException($"Mô phỏng lỗi SaveChangesAsync lần thứ {current}.");
                }
                return _inner.SaveChangesAsync(cancellationToken);
            }
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

        private async Task<(ChiNhanh branch, PhongTro room, NguoiThue tenant, HopDong contract, NguoiDung tenantAccount)> CreateHierarchyWithAccountAsync(string prefix)
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

            var tenantAccount = new NguoiDung
            {
                TenDangNhap = $"tnt_{suffix}",
                MatKhauHash = "hash",
                Role = Role.KhachThue,
                NguoiThueId = tenant.NguoiThueId,
                IsActive = true
            };
            context.NguoiDungs.Add(tenantAccount);

            await context.SaveChangesAsync();

            return (branch, room, tenant, contract, tenantAccount);
        }

        private InvoicePublicationService CreateService(ApplicationDbContext ctx, IUnitOfWork? unitOfWorkOverride = null, IEmailService? emailOverride = null, int soNgayHanThanhToan = 7)
        {
            return new InvoicePublicationService(
                new InvoicePublicationStore(ctx),
                new HoaDonStore(ctx),
                unitOfWorkOverride ?? new EfUnitOfWork(ctx),
                new AllowAllEmployeeAccessService(),
                emailOverride ?? new CountingFakeEmailService(),
                new FakeDocumentExporter(),
                new FakeVietQRService(),
                new VietQrSettings(),
                new NoOpThongBaoNotifier(),
                new InvoiceIssuanceOptions { SoNgayHanThanhToan = soNgayHanThanhToan },
                NullLogger<InvoicePublicationService>.Instance);
        }

        private static HoaDon MakeDaChotInvoice(int hopDongId, string ma)
        {
            return new HoaDon
            {
                MaHoaDon = ma,
                HopDongId = hopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1_500_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaChot
            };
        }

        [Fact]
        public async Task Publish_DaChot_PersistsDaGuiHanOneThongBaoOneHistory()
        {
            var (_, _, _, contract, tenantAccount) = await CreateHierarchyWithAccountAsync("persist");

            await using var seedCtx = _fixture.CreateDbContext();
            var invoice = MakeDaChotInvoice(contract.HopDongId, "HD-PERSIST-1");
            seedCtx.HoaDons.Add(invoice);
            await seedCtx.SaveChangesAsync();
            var invoiceId = invoice.HoaDonId;

            await using var ctx = _fixture.CreateDbContext();
            var service = CreateService(ctx);

            var result = await service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new[] { invoiceId } }, actorId: 1);
            Assert.True(result.Success);
            Assert.Equal(InvoicePublishOutcome.Published, result.Data!.Items.Single().Publish);

            await using var verifyCtx = _fixture.CreateDbContext();
            var persisted = await verifyCtx.HoaDons
                .Include(h => h.LichSuTrangThaiHoaDons)
                .FirstAsync(h => h.HoaDonId == invoiceId);

            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, persisted.TrangThaiPhatHanh);
            Assert.NotNull(persisted.NgayGui);
            Assert.NotNull(persisted.HanThanhToan);
            Assert.Single(persisted.LichSuTrangThaiHoaDons.Where(h => h.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaGui));

            var thongBaos = await verifyCtx.ThongBaos.Where(t => t.NguoiDungId == tenantAccount.NguoiDungId).ToListAsync();
            Assert.Single(thongBaos);
        }

        [Fact]
        public async Task Publish_Repeat_AfterDaGui_NoChanges()
        {
            var (_, _, _, contract, _) = await CreateHierarchyWithAccountAsync("repeat");

            await using var seedCtx = _fixture.CreateDbContext();
            var invoice = MakeDaChotInvoice(contract.HopDongId, "HD-REPEAT-1");
            seedCtx.HoaDons.Add(invoice);
            await seedCtx.SaveChangesAsync();
            var invoiceId = invoice.HoaDonId;

            await using var ctx1 = _fixture.CreateDbContext();
            var service1 = CreateService(ctx1);
            var first = await service1.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new[] { invoiceId } }, actorId: 1);
            Assert.Equal(InvoicePublishOutcome.Published, first.Data!.Items.Single().Publish);

            DateTime ngayGuiSau1Lan;
            DateTime? hanSau1Lan;
            int thongBaoCountSau1Lan;
            int historyCountSau1Lan;
            await using (var verify1 = _fixture.CreateDbContext())
            {
                var inv = await verify1.HoaDons.Include(h => h.LichSuTrangThaiHoaDons).FirstAsync(h => h.HoaDonId == invoiceId);
                ngayGuiSau1Lan = inv.NgayGui!.Value;
                hanSau1Lan = inv.HanThanhToan;
                historyCountSau1Lan = inv.LichSuTrangThaiHoaDons.Count;
                thongBaoCountSau1Lan = await verify1.ThongBaos.CountAsync();
            }

            await using var ctx2 = _fixture.CreateDbContext();
            var service2 = CreateService(ctx2);
            var second = await service2.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new[] { invoiceId } }, actorId: 1);
            Assert.Equal(InvoicePublishOutcome.AlreadyPublished, second.Data!.Items.Single().Publish);

            await using var verify2 = _fixture.CreateDbContext();
            var invAfter = await verify2.HoaDons.Include(h => h.LichSuTrangThaiHoaDons).FirstAsync(h => h.HoaDonId == invoiceId);
            Assert.Equal(ngayGuiSau1Lan, invAfter.NgayGui);
            Assert.Equal(hanSau1Lan, invAfter.HanThanhToan);
            Assert.Equal(historyCountSau1Lan, invAfter.LichSuTrangThaiHoaDons.Count);
            Assert.Equal(thongBaoCountSau1Lan, await verify2.ThongBaos.CountAsync());
        }

        [Fact]
        public async Task Publish_ExistingHanThanhToan_Kept()
        {
            var (_, _, _, contract, _) = await CreateHierarchyWithAccountAsync("keep_han");

            await using var seedCtx = _fixture.CreateDbContext();
            var invoice = MakeDaChotInvoice(contract.HopDongId, "HD-KEEP-1");
            var existingHan = new DateTime(2026, 11, 1, 16, 59, 59, DateTimeKind.Utc);
            invoice.HanThanhToan = existingHan;
            seedCtx.HoaDons.Add(invoice);
            await seedCtx.SaveChangesAsync();
            var invoiceId = invoice.HoaDonId;

            await using var ctx = _fixture.CreateDbContext();
            var service = CreateService(ctx);
            await service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new[] { invoiceId } }, actorId: 1);

            await using var verifyCtx = _fixture.CreateDbContext();
            var persisted = await verifyCtx.HoaDons.FirstAsync(h => h.HoaDonId == invoiceId);
            Assert.Equal(existingHan, persisted.HanThanhToan);
        }

        [Fact]
        public async Task Publish_TwoConcurrentRequests_OnlyOnePublishes()
        {
            var (_, _, _, contract, tenantAccount) = await CreateHierarchyWithAccountAsync("conc_pub");

            await using var seedCtx = _fixture.CreateDbContext();
            var invoice = MakeDaChotInvoice(contract.HopDongId, "HD-CONC-1");
            seedCtx.HoaDons.Add(invoice);
            await seedCtx.SaveChangesAsync();
            var invoiceId = invoice.HoaDonId;

            // Kết nối giữ khóa trên hóa đơn để buộc hai request phải chờ nhau
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {invoiceId} FOR UPDATE")
                .ToListAsync();

            var email1 = new CountingFakeEmailService();
            var email2 = new CountingFakeEmailService();

            var t1 = Task.Run(async () =>
            {
                await using var c1 = _fixture.CreateDbContext();
                await c1.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service1 = CreateService(c1, emailOverride: email1);
                return await service1.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new[] { invoiceId } }, actorId: 1);
            });

            var t2 = Task.Run(async () =>
            {
                await using var c2 = _fixture.CreateDbContext();
                await c2.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                var service2 = CreateService(c2, emailOverride: email2);
                return await service2.PublishAsync(new PublishInvoicesRequest { HoaDonIds = new[] { invoiceId } }, actorId: 1);
            });

            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(5));

            await holderTx.CommitAsync();

            var r1 = await t1;
            var r2 = await t2;

            var outcomes = new[] { r1.Data!.Items.Single().Publish, r2.Data!.Items.Single().Publish };
            Assert.Contains(InvoicePublishOutcome.Published, outcomes);
            Assert.Contains(InvoicePublishOutcome.AlreadyPublished, outcomes);

            await using var verifyCtx = _fixture.CreateDbContext();
            var persisted = await verifyCtx.HoaDons.Include(h => h.LichSuTrangThaiHoaDons).FirstAsync(h => h.HoaDonId == invoiceId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, persisted.TrangThaiPhatHanh);
            Assert.Single(persisted.LichSuTrangThaiHoaDons.Where(h => h.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaGui));

            var thongBaoCount = await verifyCtx.ThongBaos.CountAsync(t => t.NguoiDungId == tenantAccount.NguoiDungId);
            Assert.Equal(1, thongBaoCount);

            var totalEmailSent = email1.SendCount + email2.SendCount;
            Assert.Equal(1, totalEmailSent);
        }

        [Fact]
        public async Task Publish_Batch_SecondSaveChangesFails_DecisionB_MiddleInvoiceUnaffected_OthersPublished()
        {
            var (_, _, _, contract1, tenant1) = await CreateHierarchyWithAccountAsync("batch_b_1");
            var (_, _, _, contract2, tenant2) = await CreateHierarchyWithAccountAsync("batch_b_2");
            var (_, _, _, contract3, tenant3) = await CreateHierarchyWithAccountAsync("batch_b_3");

            await using var seedCtx = _fixture.CreateDbContext();
            var inv1 = MakeDaChotInvoice(contract1.HopDongId, "HD-B-1");
            var inv2 = MakeDaChotInvoice(contract2.HopDongId, "HD-B-2");
            var inv3 = MakeDaChotInvoice(contract3.HopDongId, "HD-B-3");
            seedCtx.HoaDons.AddRange(inv1, inv2, inv3);
            await seedCtx.SaveChangesAsync();

            var ids = new[] { inv1.HoaDonId, inv2.HoaDonId, inv3.HoaDonId }.OrderBy(x => x).ToArray();

            await using var ctx = _fixture.CreateDbContext();
            var throwingUow = new ThrowOnNthSaveUnitOfWork(new EfUnitOfWork(ctx)) { ThrowOnSaveNumber = 2 };
            var service = CreateService(ctx, unitOfWorkOverride: throwingUow);

            var result = await service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = ids }, actorId: 1);

            Assert.True(result.Success);
            var items = result.Data!.Items.OrderBy(i => i.HoaDonId).ToList();
            Assert.Equal(InvoicePublishOutcome.Published, items[0].Publish);
            Assert.Equal(InvoicePublishOutcome.Error, items[1].Publish);
            Assert.Equal(InvoicePublishOutcome.Published, items[2].Publish);
            Assert.Equal(2, result.Data.PublishedCount);

            // Hóa đơn thứ 2 (lỗi giữa lô) phải KHÔNG bị ghi DaGui và KHÔNG sinh ThongBao.
            await using var verifyCtx = _fixture.CreateDbContext();
            var invoicesAfter = await verifyCtx.HoaDons
                .Include(h => h.LichSuTrangThaiHoaDons)
                .Where(h => ids.Contains(h.HoaDonId))
                .OrderBy(h => h.HoaDonId)
                .ToListAsync();

            var failedInvoiceId = items[1].HoaDonId;
            var failedInvoice = invoicesAfter.Single(h => h.HoaDonId == failedInvoiceId);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, failedInvoice.TrangThaiPhatHanh);
            Assert.Null(failedInvoice.NgayGui);
            Assert.Empty(failedInvoice.LichSuTrangThaiHoaDons.Where(h => h.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaGui));

            var publishedInvoices = invoicesAfter.Where(h => h.HoaDonId != failedInvoiceId).ToList();
            Assert.All(publishedInvoices, h => Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, h.TrangThaiPhatHanh));
            Assert.All(publishedInvoices, h => Assert.Single(h.LichSuTrangThaiHoaDons.Where(x => x.TrangThaiPhatHanhMoi == TrangThaiPhatHanhHoaDon.DaGui)));

            // Không có ThongBao trùng: đúng một ThongBao cho mỗi hóa đơn công bố thành công, không có ThongBao nào cho hóa đơn lỗi.
            var allTenantIds = new[] { tenant1.NguoiDungId, tenant2.NguoiDungId, tenant3.NguoiDungId };
            var thongBaos = await verifyCtx.ThongBaos.Where(t => t.NguoiDungId != null && allTenantIds.Contains(t.NguoiDungId.Value)).ToListAsync();
            Assert.Equal(2, thongBaos.Count);
            Assert.Equal(2, thongBaos.Select(t => t.NguoiDungId).Distinct().Count());

            var failedTenantAccount = new[] { tenant1, tenant2, tenant3 }
                .Zip(new[] { inv1.HoaDonId, inv2.HoaDonId, inv3.HoaDonId }, (t, id) => (t, id))
                .Single(x => x.id == failedInvoiceId).t;
            Assert.DoesNotContain(thongBaos, t => t.NguoiDungId == failedTenantAccount.NguoiDungId);
        }

        [Fact]
        public async Task Publish_Batch_FirstEmailThrows_BothDaGuiWithThongBao()
        {
            var (_, _, _, contract1, tenant1) = await CreateHierarchyWithAccountAsync("email_throw_1");
            var (_, _, _, contract2, tenant2) = await CreateHierarchyWithAccountAsync("email_throw_2");

            await using var seedCtx = _fixture.CreateDbContext();
            var inv1 = MakeDaChotInvoice(contract1.HopDongId, "HD-ET-1");
            var inv2 = MakeDaChotInvoice(contract2.HopDongId, "HD-ET-2");
            seedCtx.HoaDons.AddRange(inv1, inv2);
            await seedCtx.SaveChangesAsync();

            var ids = new[] { inv1.HoaDonId, inv2.HoaDonId }.OrderBy(x => x).ToArray();

            await using var ctx = _fixture.CreateDbContext();
            var throwingEmail = new FirstCallThrowsEmailService();
            var service = CreateService(ctx, emailOverride: throwingEmail);

            var result = await service.PublishAsync(new PublishInvoicesRequest { HoaDonIds = ids }, actorId: 1);

            Assert.True(result.Success);
            var items = result.Data!.Items.OrderBy(i => i.HoaDonId).ToList();
            Assert.Equal(InvoicePublishOutcome.Published, items[0].Publish);
            Assert.Equal(InvoicePublishOutcome.Published, items[1].Publish);
            Assert.Equal(InvoiceEmailOutcome.Failed, items[0].Email);
            Assert.Equal(InvoiceEmailOutcome.Sent, items[1].Email);
            Assert.Equal(2, throwingEmail.CallCount);

            await using var verifyCtx = _fixture.CreateDbContext();
            var invoicesAfter = await verifyCtx.HoaDons
                .Where(h => ids.Contains(h.HoaDonId))
                .OrderBy(h => h.HoaDonId)
                .ToListAsync();
            Assert.All(invoicesAfter, h => Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, h.TrangThaiPhatHanh));

            var thongBaoCount1 = await verifyCtx.ThongBaos.CountAsync(t => t.NguoiDungId == tenant1.NguoiDungId);
            var thongBaoCount2 = await verifyCtx.ThongBaos.CountAsync(t => t.NguoiDungId == tenant2.NguoiDungId);
            Assert.Equal(1, thongBaoCount1);
            Assert.Equal(1, thongBaoCount2);
        }

        [Fact]
        public async Task TaoYeuCauThanhToan_DaChotThrows_DaGuiSucceeds()
        {
            var (_, _, _, contractDaChot, _) = await CreateHierarchyWithAccountAsync("payreq_dachot");
            var (_, _, _, contractDaGui, _) = await CreateHierarchyWithAccountAsync("payreq_dagui");

            await using var seedCtx = _fixture.CreateDbContext();
            var invoiceDaChot = MakeDaChotInvoice(contractDaChot.HopDongId, "HD-PR-1");
            var invoiceDaGui = MakeDaChotInvoice(contractDaGui.HopDongId, "HD-PR-2");
            invoiceDaGui.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            invoiceDaGui.NgayGui = DateTime.UtcNow;
            invoiceDaGui.HanThanhToan = DateTime.UtcNow.AddDays(7);
            seedCtx.HoaDons.AddRange(invoiceDaChot, invoiceDaGui);
            await seedCtx.SaveChangesAsync();
            var daChotId = invoiceDaChot.HoaDonId;
            var daGuiId = invoiceDaGui.HoaDonId;

            await using (var ctxDaChot = _fixture.CreateDbContext())
            {
                var serviceDaChot = new InvoicePaymentConfirmationService(
                    new InvoicePaymentStore(ctxDaChot),
                    new EfUnitOfWork(ctxDaChot),
                    NullLogger<InvoicePaymentConfirmationService>.Instance);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => serviceDaChot.TaoYeuCauThanhToanAsync(daChotId, 1_500_000m, "THANH TOAN HD-PR-1"));
                Assert.Equal("Chỉ hóa đơn đã gửi cho khách thuê mới được tạo yêu cầu thanh toán.", ex.Message);
            }

            await using (var verifyDaChotCtx = _fixture.CreateDbContext())
            {
                var count = await verifyDaChotCtx.YeuCauThanhToanHoaDons.CountAsync(y => y.HoaDonId == daChotId);
                Assert.Equal(0, count);
            }

            await using (var ctxDaGui = _fixture.CreateDbContext())
            {
                var serviceDaGui = new InvoicePaymentConfirmationService(
                    new InvoicePaymentStore(ctxDaGui),
                    new EfUnitOfWork(ctxDaGui),
                    NullLogger<InvoicePaymentConfirmationService>.Instance);

                var response = await serviceDaGui.TaoYeuCauThanhToanAsync(daGuiId, 1_500_000m, "THANH TOAN HD-PR-2");
                Assert.Equal(daGuiId, response.HoaDonId);
            }

            await using (var verifyDaGuiCtx = _fixture.CreateDbContext())
            {
                var requests = await verifyDaGuiCtx.YeuCauThanhToanHoaDons.Where(y => y.HoaDonId == daGuiId).ToListAsync();
                var request = Assert.Single(requests);
                Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, request.TrangThai);
                Assert.Equal(1_500_000m, request.SoTien);
            }
        }
    }
}
