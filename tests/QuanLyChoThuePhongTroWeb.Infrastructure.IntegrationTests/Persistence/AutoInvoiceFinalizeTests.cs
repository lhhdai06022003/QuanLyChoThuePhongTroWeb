using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    // Job tự chốt chạy bằng use case thật trên DB test; email, PDF, SignalR và đồng hồ là bản thay thế.
    [Collection("PostgreSqlCollection")]
    public class AutoInvoiceFinalizeTests
    {
        private readonly PostgreSqlFixture _fixture;

        public AutoInvoiceFinalizeTests(PostgreSqlFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<int> RunAsync(DeadlineHost host, params DeadlineSeed[] seeds)
        {
            await DeadlineHarness.IsolateJobAsync(_fixture, seeds);
            await using var scope = host.Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAutoInvoiceFinalizeUseCase>().ExecuteAsync();
        }

        [Fact]
        public async Task DueInvoice_IsFinalizedAndPublished_WithHistoryNoticeAndEmail()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            host.Clock.Now = invoice.HanPhanHoi!.Value;

            var count = await RunAsync(host, seed);

            Assert.True(count >= 1);
            var after = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, seed));
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, after.TrangThaiPhatHanh);
            Assert.Equal(host.Clock.Now, after.NgayGui);
            Assert.Equal(InvoiceDueDateCalculator.Compute(host.Clock.Now, 7), after.HanThanhToan);
            Assert.Equal(invoice.TongTien, after.TongTien);

            var history = after.LichSuTrangThaiHoaDons.OrderBy(h => h.LichSuTrangThaiHoaDonId).ToList();
            Assert.Equal(4, history.Count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, history[2].TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, history[3].TrangThaiPhatHanhMoi);
            Assert.All(history.Skip(2), h =>
            {
                Assert.StartsWith("[TU_DONG]", h.LyDo);
                Assert.Equal(seed.Staff.NguoiDungId, h.NguoiThucHienId);
            });

            await using var db = _fixture.CreateDbContext();
            var notices = await db.ThongBaos.AsNoTracking().Where(t => t.NguoiDungId == seed.TenantAccount.NguoiDungId).OrderBy(t => t.Id).ToListAsync();
            Assert.Equal(2, notices.Count);
            Assert.Equal($"/KhachThue/HoaDon?hoaDonId={invoice.HoaDonId}", notices[1].LinkDieuHuong);
            Assert.Contains("hạn thanh toán", notices[1].NoiDung);

            var mail = Assert.Single(host.Email.Invoice, m => m.MaHoaDon == invoice.MaHoaDon);
            Assert.Equal(seed.Tenant.Email, mail.To);
            Assert.Equal(invoice.MaHoaDon, mail.MaHoaDon);
        }

        [Fact]
        public async Task NotYetDue_ManualChoDuyet_AndDeletedInvoices_AreLeftUntouched()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var notDue = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var notDueInvoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, notDue);
            var manual = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var manualInvoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, manual);
            var deleted = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var deletedInvoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, deleted);

            await using (var db = _fixture.CreateDbContext())
            {
                var manualEntity = await db.HoaDons.SingleAsync(h => h.HoaDonId == manualInvoice.HoaDonId);
                manualEntity.HanPhanHoi = null; // hóa đơn gửi duyệt tay
                var deletedEntity = await db.HoaDons.SingleAsync(h => h.HoaDonId == deletedInvoice.HoaDonId);
                deletedEntity.IsDeleted = true;
                await db.SaveChangesAsync();
            }

            host.Clock.Now = notDueInvoice.HanPhanHoi!.Value.AddSeconds(-1);
            await RunAsync(host, notDue, manual, deleted);

            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, (await DeadlineHarness.InvoicesAsync(_fixture, notDue)).Single().TrangThaiPhatHanh);

            host.Clock.Now = notDueInvoice.HanPhanHoi.Value.AddDays(10);
            await RunAsync(host, notDue, manual, deleted);

            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, (await DeadlineHarness.InvoicesAsync(_fixture, manual)).Single().TrangThaiPhatHanh);
            var deletedAfter = Assert.Single(await DeadlineHarness.InvoicesAsync(_fixture, deleted, includeDeleted: true));
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, deletedAfter.TrangThaiPhatHanh);
        }

        [Fact]
        public async Task InvoiceEmailFailure_StillFinalizesInvoice()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            host.Email.FailInvoiceEmail = true;
            host.Clock.Now = invoice.HanPhanHoi!.Value.AddMinutes(5);

            await RunAsync(host, seed);

            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, (await DeadlineHarness.InvoicesAsync(_fixture, seed)).Single().TrangThaiPhatHanh);
            Assert.DoesNotContain(host.Email.Invoice, m => m.MaHoaDon == invoice.MaHoaDon);
        }

        [Fact]
        public async Task PeriodNoLongerApproved_IsSkipped_AndStaffNotifiedOnlyOnce()
        {
            using var host = new DeadlineHost(_fixture.ConnectionString);
            var seed = await DeadlineHarness.SeedAsync(_fixture, host.Clock.Now);
            var invoice = await DeadlineHarness.CreateProvisionalInvoiceAsync(host, _fixture, seed);
            await using (var db = _fixture.CreateDbContext())
            {
                var period = await db.DichVuDienNuocCuaPhongs.SingleAsync(p => p.DichVuDienNuocCuaPhongId == invoice.DichVuDienNuocCuaPhongId);
                period.TrangThaiGhiNhan = TrangThaiGhiNhan.TuChoi;
                await db.SaveChangesAsync();
            }

            host.Clock.Now = invoice.HanPhanHoi!.Value.AddMinutes(1);
            await RunAsync(host, seed);
            await RunAsync(host, seed);

            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, (await DeadlineHarness.InvoicesAsync(_fixture, seed)).Single().TrangThaiPhatHanh);
            await using var verify = _fixture.CreateDbContext();
            var staffNotices = await verify.ThongBaos.AsNoTracking()
                .Where(t => t.NguoiDungId == seed.Staff.NguoiDungId && t.TieuDe.Contains("chưa tự chốt được"))
                .ToListAsync();
            Assert.Single(staffNotices);
        }
    }
}
