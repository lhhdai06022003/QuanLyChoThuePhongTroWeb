using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoicePaymentTransactionTests
    {
        private readonly PaymentTestHarness _h = new();

        [Fact]
        public async Task Run_LocksInsideTransaction_ExpiresStaleRequests_AndCommits()
        {
            _h.AddSentInvoice();
            var stale = _h.AddRequest(createdAt: PaymentTestHarness.Now.AddHours(-30));

            var result = await _h.Transaction.RunAsync(7, 1, scope =>
            {
                Assert.Contains(stale.YeuCauThanhToanHoaDonId, scope.ExpiredRequestIds);
                return Task.FromResult(ServiceResult<int>.Ok(1));
            });

            Assert.True(result.Success);
            Assert.Equal(1, _h.Store.LockCalls);
            Assert.False(_h.Store.LockedOutsideTransaction);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, stale.TrangThai);
            Assert.Null(stale.LichSuTrangThaiYeuCauThanhToanHoaDons.Last().NguoiThucHienId);
            Assert.True(_h.LastTx.Committed);
        }

        [Fact]
        public async Task Run_FailureAfterExpiry_CommitsOnlyTheExpiry()
        {
            _h.AddSentInvoice();
            _h.AddRequest(createdAt: PaymentTestHarness.Now.AddHours(-30));

            var result = await _h.Transaction.RunAsync(7, 1, _ => Task.FromResult(ServiceResult<int>.Fail("lỗi nghiệp vụ")));

            Assert.False(result.Success);
            Assert.True(_h.LastTx.Committed);
            Assert.False(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task Run_FailureWithoutExpiry_RollsBack()
        {
            _h.AddSentInvoice();

            var result = await _h.Transaction.RunAsync(7, 1, _ => Task.FromResult(ServiceResult<int>.Fail("lỗi")));

            Assert.False(result.Success);
            Assert.True(_h.LastTx.RolledBack);
            Assert.False(_h.LastTx.Committed);
        }

        [Fact]
        public async Task Run_FailureAfterLogicSaved_RollsBackEvenWithExpiry()
        {
            _h.AddSentInvoice();
            _h.AddRequest(createdAt: PaymentTestHarness.Now.AddHours(-30));

            var result = await _h.Transaction.RunAsync(7, 1, async scope =>
            {
                await scope.SaveChangesAsync();
                return ServiceResult<int>.Fail("lỗi sau khi lưu");
            });

            Assert.False(result.Success);
            Assert.True(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task Run_MissingInvoice_ReturnsNotFound()
        {
            var result = await _h.Transaction.RunAsync(99, 1, _ => Task.FromResult(ServiceResult<int>.Ok(1)));

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
            Assert.True(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task Run_UniqueViolation_BecomesBusinessError_AndRollsBack()
        {
            _h.AddSentInvoice();
            _h.UnitOfWork.FailOnSave = n => new UniqueConstraintViolationException("IX_lich_su_thanh_toan_MaGiaoDich", new Exception());

            var result = await _h.Transaction.RunAsync(7, 1, _ => Task.FromResult(ServiceResult<int>.Ok(1)));

            Assert.False(result.Success);
            Assert.Equal("Mã giao dịch đã được ghi nhận.", result.Message);
            Assert.True(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task Run_UnexpectedException_RollsBack_WithGenericMessage()
        {
            _h.AddSentInvoice();

            var result = await _h.Transaction.RunAsync<int>(7, 1, _ => throw new InvalidOperationException("SECRET"));

            Assert.False(result.Success);
            Assert.DoesNotContain("SECRET", result.Message);
            Assert.True(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task Run_LedgerOverTotal_IsRejectedByDomain_AndRolledBack()
        {
            var hoaDon = _h.AddSentInvoice(tongTien: 1_000_000m);

            var result = await _h.Transaction.RunAsync(7, 1, async scope =>
            {
                scope.Store.AddLedgerEntry(new LichSuThanhToan { HoaDonId = 7, MaGiaoDich = "X", SoTienThanhToan = 1_000_001m });
                await scope.ApplyLedgerChangeAsync("vượt");
                return ServiceResult<int>.Ok(1);
            });

            Assert.False(result.Success);
            Assert.True(_h.LastTx.RolledBack);
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, hoaDon.TrangThaiHoaDon);
        }

        [Fact]
        public async Task Run_PushesNotificationsOnlyAfterCommit_AndToleratesPushFailure()
        {
            _h.AddSentInvoice();
            _h.Notifier.Throw = true;

            var result = await _h.Transaction.RunAsync(7, 1, async scope =>
            {
                await scope.NotifyAdminsAsync("Tiêu đề", "Nội dung", "/x");
                Assert.Empty(_h.Notifier.Sent);
                return ServiceResult<int>.Ok(1);
            });

            Assert.True(result.Success);
            Assert.True(_h.LastTx.Committed);
            Assert.Single(_h.Store.Notifications);
        }

        [Fact]
        public async Task Run_FailedRun_DoesNotPushNotifications()
        {
            _h.AddSentInvoice();

            await _h.Transaction.RunAsync(7, 1, async scope =>
            {
                await scope.NotifyAdminsAsync("Tiêu đề", "Nội dung", "/x");
                return ServiceResult<int>.Fail("lỗi");
            });

            Assert.Empty(_h.Notifier.Sent);
        }
    }
}
