using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    public class HoaDonRowLockConcurrencyTests
    {
        private readonly PostgreSqlFixture _fixture;

        public HoaDonRowLockConcurrencyTests(PostgreSqlFixture fixture)
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

        private class FakeEmailService : IEmailService
        {
            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
                => Task.FromResult((true, string.Empty));

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs.ContractExpiryAlertData alertData)
                => Task.FromResult((true, string.Empty));
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

        private HoaDonService CreateService(ApplicationDbContext ctx)
        {
            return new HoaDonService(
                new HoaDonStore(ctx),
                new EfUnitOfWork(ctx),
                new VietQrSettings(),
                NullLogger<HoaDonService>.Instance,
                new HoaDonCalculatorService(),
                new FakeDocumentExporter(),
                new FakeVietQRService(),
                new AllowAllEmployeeAccessService(),
                new FakeEmailService());
        }

        // Dựng chi nhánh, phòng, người thuê, hợp đồng, một nhân viên (làm người xác nhận) và một hóa đơn DaGui chưa thanh toán.
        private async Task<(int InvoiceId, int ActorId)> SeedSentInvoiceAsync(string prefix)
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

            var tenant = new NguoiThue
            {
                HoVaTen = $"Khach {suffix}",
                SoDienThoai = "0987654321",
                Email = $"khach_{suffix}@test.com",
                CCCD = $"01234567{suffix[..4]}"
            };
            context.NguoiThues.Add(tenant);

            var staff = new NguoiDung
            {
                TenDangNhap = $"staff_{prefix}_{suffix}",
                MatKhauHash = "hash",
                Role = Role.NhanVien,
                IsActive = true
            };
            context.NguoiDungs.Add(staff);
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

            var invoice = new HoaDon
            {
                MaHoaDon = $"HD-LOCK-{prefix}-{suffix}",
                HopDongId = contract.HopDongId,
                Thang = 9,
                Nam = 2026,
                TongTien = 1_500_000m,
                TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui,
                TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                NgayGui = DateTime.UtcNow
            };
            context.HoaDons.Add(invoice);
            await context.SaveChangesAsync();

            return (invoice.HoaDonId, staff.NguoiDungId);
        }

        private async Task<Task<T>[]> RunBlockedAsync<T>(int invoiceId, Func<HoaDonService, Task<T>> first, Func<HoaDonService, Task<T>> second)
        {
            // Kết nối giữ khóa trên hóa đơn để buộc hai lệnh phải chờ nhau
            await using var holderCtx = _fixture.CreateDbContext();
            await using var holderTx = await holderCtx.Database.BeginTransactionAsync();
            await holderCtx.HoaDons
                .FromSqlInterpolated($"SELECT * FROM hoa_don WHERE \"HoaDonId\" = {invoiceId} FOR UPDATE")
                .ToListAsync();

            Task<T> Start(Func<HoaDonService, Task<T>> action) => Task.Run(async () =>
            {
                await using var ctx = _fixture.CreateDbContext();
                await ctx.Database.ExecuteSqlRawAsync("SET lock_timeout = '10000ms';");
                return await action(CreateService(ctx));
            });

            var t1 = Start(first);
            var t2 = Start(second);

            await WaitForBlockedLocksAsync(expectedWaitingCount: 2, timeout: TimeSpan.FromSeconds(5));

            await holderTx.CommitAsync();

            await Task.WhenAll(t1, t2);
            return new[] { t1, t2 };
        }

        [Fact]
        public async Task ThuTienAndCancel_Concurrent_OnlyOneSucceeds_NoCancelledInvoiceWithPayment()
        {
            var (invoiceId, actorId) = await SeedSentInvoiceAsync("thu_huy");

            var tasks = await RunBlockedAsync<(bool IsSuccess, string? ErrorMessage)>(
                invoiceId,
                s => s.ThuTienAsync(invoiceId, (int)PhuongThucThanhToan.TienMat, "Thu", actorId),
                s => s.DeleteHoaDonAsync(invoiceId, actorId, "Hủy kiểm thử"));

            var results = tasks.Select(t => t.Result).ToList();
            Assert.Equal(1, results.Count(r => r.IsSuccess));

            await using var verifyCtx = _fixture.CreateDbContext();
            var persisted = await verifyCtx.HoaDons.IgnoreQueryFilters().FirstAsync(h => h.HoaDonId == invoiceId);
            var paymentCount = await verifyCtx.LichSuThanhToans.CountAsync(l => l.HoaDonId == invoiceId);

            if (persisted.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy)
            {
                Assert.Equal(0, paymentCount);
                Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, persisted.TrangThaiHoaDon);
            }
            else
            {
                Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, persisted.TrangThaiPhatHanh);
                Assert.Equal(TrangThaiHoaDon.DaThanhToan, persisted.TrangThaiHoaDon);
                Assert.Equal(1, paymentCount);
                Assert.False(persisted.IsDeleted);
            }
        }

        [Fact]
        public async Task ThuTien_TwoConcurrent_CreatesSingleLichSuThanhToan()
        {
            var (invoiceId, actorId) = await SeedSentInvoiceAsync("thu_thu");

            var tasks = await RunBlockedAsync<(bool IsSuccess, string? ErrorMessage)>(
                invoiceId,
                s => s.ThuTienAsync(invoiceId, (int)PhuongThucThanhToan.TienMat, "Thu 1", actorId),
                s => s.ThuTienAsync(invoiceId, (int)PhuongThucThanhToan.TienMat, "Thu 2", actorId));

            var results = tasks.Select(t => t.Result).ToList();
            Assert.Equal(1, results.Count(r => r.IsSuccess));
            Assert.Equal("Hóa đơn này đã được thanh toán trước đó.", results.Single(r => !r.IsSuccess).ErrorMessage);

            await using var verifyCtx = _fixture.CreateDbContext();
            var payments = await verifyCtx.LichSuThanhToans.Where(l => l.HoaDonId == invoiceId).ToListAsync();
            var payment = Assert.Single(payments);
            Assert.Equal(1_500_000m, payment.SoTienThanhToan);
        }
    }
}
