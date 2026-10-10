using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AutoInvoiceFinalizeUseCaseTests
    {
        private const int Staff = 7;
        private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);

        private sealed class FakeIssuanceStore : IInvoiceIssuanceStore
        {
            public Dictionary<int, HoaDon> Invoices { get; } = new();
            public Dictionary<int, TrangThaiGhiNhan> PeriodStatus { get; } = new();
            public HashSet<int> HasOtherActive { get; } = new();

            public Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default) => throw new NotSupportedException();

            public Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default)
                => Task.FromResult(Invoices.TryGetValue(hoaDonId, out var h) && !h.IsDeleted ? h : null);

            public Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default)
                => Task.FromResult(HasOtherActive.Contains(excludeHoaDonId));

            public Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default)
            {
                if (!Invoices.TryGetValue(hoaDonId, out var h) || h.IsDeleted)
                {
                    return Task.FromResult<(int? MeterPeriodId, int ChiNhanhId)?>(null);
                }

                return Task.FromResult<(int? MeterPeriodId, int ChiNhanhId)?>((h.DichVuDienNuocCuaPhongId, 1));
            }

            public Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default)
                => Task.FromResult<TrangThaiGhiNhan?>(PeriodStatus.TryGetValue(meterPeriodId, out var s) ? s : TrangThaiGhiNhan.DaDuyet);

            public Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default) => throw new NotSupportedException();
            public Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime nextMonthStartUtc, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default) => throw new NotSupportedException();
            public Task LockMeterPeriodsForRoomsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<int>> GetContractIdsForTenantRoomAsync(int phongTroId, int nguoiThueId, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private sealed class ThrowingOnceEmail : IEmailService
        {
            public bool Throw { get; set; }
            public List<string> Sent { get; } = new();

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                if (Throw)
                {
                    throw new InvalidOperationException("SMTP lỗi");
                }

                Sent.Add(hoaDon.MaHoaDon);
                return Task.FromResult((true, string.Empty));
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
                => throw new NotSupportedException();
        }

        private sealed class Exporter : IInvoiceDocumentExporter
        {
            public byte[] ExportExcel(HoaDonChiTietRes hoaDon) => new byte[] { 1 };
            public byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName) => new byte[] { 2 };
        }

        private sealed class QrService : IVietQRService
        {
            public string GenerateVietQRString(string bankIdOrBin, string accountNumber, decimal amount, string memo) => "QR";
            public byte[] GenerateQRCodePNGBytes(string payload) => new byte[] { 3 };
        }

        private sealed class Harness
        {
            public FakeAutoInvoiceStore Auto { get; } = new();
            public FakeIssuanceStore Issuance { get; } = new();
            public FakeInvoicePublicationStore Publication { get; } = new();
            public FakeHoaDonStore HoaDonStore { get; } = new();
            public RecordingUnitOfWork Uow { get; } = new();
            public FakeThongBaoService ThongBao { get; } = new();
            public ThrowingOnceEmail Email { get; } = new();
            public CapturingLogger<AutoInvoiceFinalizeUseCase> Logger { get; } = new();
            public AutoInvoiceFinalizeNotifyGuard Guard { get; } = new();
            public FixedTimeProvider Time { get; } = new(new DateTimeOffset(Now));
            public AutoInvoiceFinalizeUseCase UseCase { get; }

            public Harness()
            {
                Publication.Recipients[10] = new InvoiceRecipientInfo { NguoiThueId = 9, HoVaTen = "A", Email = "khach@example.com", NguoiDungId = 3 };
                UseCase = new AutoInvoiceFinalizeUseCase(
                    Auto, Issuance, Publication, HoaDonStore, Uow, ThongBao, Email, new Exporter(), new QrService(),
                    new VietQrSettings(), new InvoiceIssuanceOptions { SoNgayHanThanhToan = 7 }, Guard, Logger, Time);
            }

            public HoaDon AddInvoice(int id, DateTime han, int periodId = 0, decimal total = 1_000_000m, int phong = 101, int? submitter = Staff)
            {
                var invoice = new HoaDon
                {
                    HoaDonId = id, HopDongId = 10, MaHoaDon = $"HD-{id}", Thang = 10, Nam = 2026, TongTien = total,
                    TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.ChoDuyet, TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    HanPhanHoi = han, DichVuDienNuocCuaPhongId = periodId == 0 ? 40 + id : periodId,
                    ChiTietHoaDonDichVus = new List<ChiTietHoaDon> { new() { TenDichVu = "Tiền phòng", TongTien = 1_000_000m } }
                };
                Issuance.Invoices[id] = invoice;
                Auto.DueItems.Add(new AutoInvoiceDueItem(id, invoice.MaHoaDon, phong, han));
                Auto.LastSubmitters[id] = submitter;
                HoaDonStore.Details[id] = new HoaDonChiTietRes
                {
                    HoaDonId = id, MaHoaDon = invoice.MaHoaDon, TenNguoiThue = "A", TongTien = total, ConLai = total,
                    ChiTietHoaDons = new List<ChiTietHoaDonRes> { new() { TenDichVu = "Tiền phòng", TongTien = total } }
                };
                return invoice;
            }
        }

        [Fact]
        public async Task NoDueInvoices_ReturnsZero_AndTouchesNothing()
        {
            var h = new Harness();

            var count = await h.UseCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Empty(h.Uow.Transactions);
            Assert.Empty(h.ThongBao.Sent);
        }

        [Fact]
        public async Task InvoiceDueExactlyNow_IsFinalizedAndPublished_WithTwoAutoHistoryRows()
        {
            var h = new Harness();
            var invoice = h.AddInvoice(1, Now);

            var count = await h.UseCase.ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, invoice.TrangThaiPhatHanh);
            Assert.Equal(Now, invoice.NgayGui);
            Assert.Equal(InvoiceDueDateCalculator.Compute(Now, 7), invoice.HanThanhToan);
            Assert.Equal(Staff, invoice.NguoiChotId);
            var history = invoice.LichSuTrangThaiHoaDons.ToList();
            Assert.Equal(2, history.Count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaChot, history[0].TrangThaiPhatHanhMoi);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, history[1].TrangThaiPhatHanhMoi);
            Assert.All(history, e =>
            {
                Assert.StartsWith("[TU_DONG]", e.LyDo);
                Assert.Equal(Staff, e.NguoiThucHienId);
            });
            var tx = Assert.Single(h.Uow.Transactions);
            Assert.True(tx.Committed);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Khach", notice.Kind);
            Assert.Equal(9, notice.TargetId);
            Assert.Equal("/KhachThue/HoaDon?hoaDonId=1", notice.Link);
            Assert.Contains("1,000,000", notice.NoiDung);
            Assert.Equal(new[] { "HD-1" }, h.Email.Sent);
        }

        [Fact]
        public async Task DeadlineMovedForwardAfterListing_IsSkippedSilently()
        {
            var h = new Harness();
            var invoice = h.AddInvoice(1, Now);
            invoice.HanPhanHoi = Now.AddSeconds(1); // vừa được điều chỉnh sau lúc liệt kê

            var count = await h.UseCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, invoice.TrangThaiPhatHanh);
            Assert.True(Assert.Single(h.Uow.Transactions).RolledBack);
            Assert.Empty(h.ThongBao.Sent);
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task InvoiceNoLongerChoDuyetOrWithoutDeadline_IsSkippedSilently()
        {
            var h = new Harness();
            var sent = h.AddInvoice(1, Now.AddHours(-1));
            sent.TrangThaiPhatHanh = TrangThaiPhatHanhHoaDon.DaGui;
            var manual = h.AddInvoice(2, Now.AddHours(-1));
            manual.HanPhanHoi = null;

            var count = await h.UseCase.ExecuteAsync();

            Assert.Equal(0, count);
            Assert.Empty(h.ThongBao.Sent);
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task PeriodNoLongerApproved_WarnsAndNotifiesStaffOnlyOncePerReasonAcrossRuns()
        {
            var h = new Harness();
            var invoice = h.AddInvoice(1, Now.AddHours(-1), phong: 101);
            h.Issuance.PeriodStatus[invoice.DichVuDienNuocCuaPhongId!.Value] = TrangThaiGhiNhan.TuChoi;

            await h.UseCase.ExecuteAsync();
            await h.UseCase.ExecuteAsync();

            Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, invoice.TrangThaiPhatHanh);
            var notice = Assert.Single(h.ThongBao.Sent);
            Assert.Equal("Phong", notice.Kind);
            Assert.Equal(101, notice.TargetId);
            Assert.Equal("/QuanLyNhaTro/QuanLyHoaDon", notice.Link);
            Assert.Contains("Kỳ chỉ số điện nước liên kết không còn ở trạng thái đã chốt", notice.NoiDung);
            Assert.Equal(2, h.Logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task EachReasonIsReportedAndLeavesInvoiceUntouched()
        {
            var mismatch = new Harness();
            mismatch.AddInvoice(1, Now.AddHours(-1), total: 2_000_000m);
            var otherActive = new Harness();
            otherActive.AddInvoice(1, Now.AddHours(-1));
            otherActive.Issuance.HasOtherActive.Add(1);
            var noSubmitter = new Harness();
            noSubmitter.AddInvoice(1, Now.AddHours(-1), submitter: null);

            await mismatch.UseCase.ExecuteAsync();
            await otherActive.UseCase.ExecuteAsync();
            await noSubmitter.UseCase.ExecuteAsync();

            Assert.Contains("Tổng tiền hóa đơn không khớp", Assert.Single(mismatch.ThongBao.Sent).NoiDung);
            Assert.Contains("Đã tồn tại hóa đơn đang hoạt động khác", Assert.Single(otherActive.ThongBao.Sent).NoiDung);
            Assert.Contains("Không xác định được người gửi hóa đơn tạm tính", Assert.Single(noSubmitter.ThongBao.Sent).NoiDung);
            Assert.All(new[] { mismatch, otherActive, noSubmitter }, x =>
                Assert.Equal(TrangThaiPhatHanhHoaDon.ChoDuyet, x.Issuance.Invoices[1].TrangThaiPhatHanh));
        }

        [Fact]
        public async Task ExceptionOnFirstInvoice_LogsOneError_NotifiesStaff_AndSecondInvoiceStillFinalized()
        {
            var h = new Harness();
            h.AddInvoice(1, Now.AddHours(-2));
            var second = h.AddInvoice(2, Now.AddHours(-1));
            h.Uow.FailOnSave = n => n == 1 ? new InvalidOperationException("deadlock") : null;

            var count = await h.UseCase.ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, second.TrangThaiPhatHanh);
            Assert.True(h.Uow.Transactions[0].RolledBack);
            Assert.True(h.Uow.Transactions[1].Committed);
            Assert.Single(h.Logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Contains(h.ThongBao.Sent, n => n.Kind == "Phong" && n.NoiDung.Contains("Lỗi hệ thống khi tự chốt"));
            Assert.DoesNotContain(h.ThongBao.Sent, n => n.Kind == "Khach" && n.Link == "/KhachThue/HoaDon?hoaDonId=1");
        }

        [Fact]
        public async Task EmailAndNoticeFailures_StillFinalizeInvoice_WithWarningsOnly()
        {
            var h = new Harness();
            var invoice = h.AddInvoice(1, Now);
            h.Email.Throw = true;
            h.ThongBao.ThrowOnSend = true;

            var count = await h.UseCase.ExecuteAsync();

            Assert.Equal(1, count);
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaGui, invoice.TrangThaiPhatHanh);
            Assert.True(Assert.Single(h.Uow.Transactions).Committed);
            Assert.Equal(2, h.Logger.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(h.Logger.Entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task Cancellation_StopsTheLoopBeforeProcessingAnyInvoice()
        {
            var h = new Harness();
            h.AddInvoice(1, Now.AddHours(-1));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var count = await h.UseCase.ExecuteAsync(cts.Token);

            Assert.Equal(0, count);
            Assert.Empty(h.Uow.Transactions);
        }

        [Fact]
        public void NotifyGuard_ReportsEachInvoiceReasonPairOnce_AndForgets()
        {
            var guard = new AutoInvoiceFinalizeNotifyGuard();

            Assert.True(guard.ShouldNotify(1, "a"));
            Assert.False(guard.ShouldNotify(1, "a"));
            Assert.True(guard.ShouldNotify(1, "b"));
            Assert.True(guard.ShouldNotify(2, "a"));
            guard.Forget(1);
            Assert.True(guard.ShouldNotify(1, "b"));
        }
    }
}
