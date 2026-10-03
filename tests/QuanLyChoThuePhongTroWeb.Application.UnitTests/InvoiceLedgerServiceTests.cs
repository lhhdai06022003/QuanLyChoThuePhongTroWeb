using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoiceLedgerServiceTests
    {
        private const int Staff = PaymentTestHarness.StaffId;
        private const int OtherStaff = PaymentTestHarness.OtherStaffId;
        private const int Admin = PaymentTestHarness.AdminId;
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();

        private static ManualCollectionRequest Cash(decimal soTien, string? ghiChu = null) => new()
        {
            HoaDonId = 7,
            SoTienThucNhan = soTien,
            PhuongThuc = AppPhuongThucThanhToan.TienMat,
            NgayThanhToanUtc = Now.AddMinutes(-5),
            GhiChu = ghiChu
        };

        private static ManualCollectionRequest Transfer(decimal soTien, string? ma, string? ghiChu = null) => new()
        {
            HoaDonId = 7,
            SoTienThucNhan = soTien,
            PhuongThuc = AppPhuongThucThanhToan.ChuyenKhoan,
            NgayThanhToanUtc = Now.AddMinutes(-5),
            MaGiaoDich = ma,
            GhiChu = ghiChu
        };

        // ---------- Thu tiền thủ công (§18) ----------

        [Fact]
        public async Task Collect_FullRemaining_InCash_PaysInvoice_WithGeneratedCode()
        {
            _h.AddSentInvoice();

            var result = await _h.Ledger.CollectManuallyAsync(Cash(2_000_000m), Staff);

            Assert.True(result.Success, result.Message);
            var ledger = _h.Store.Ledger.Single();
            Assert.Matches("^TM-20261003-[A-Z0-9]{8}$", ledger.MaGiaoDich);
            Assert.Equal(PhuongThucThanhToan.TienMat, ledger.PhuongThucThanhToan);
            Assert.Equal(Now, ledger.NgayXacNhan);
            Assert.Null(ledger.MinhChungThanhToanHoaDonId);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
            Assert.Equal(0m, result.Data!.ConLai);
        }

        [Fact]
        public async Task Collect_PartialAmount_EvenWhenPartialDisabled()
        {
            _h.AddSentInvoice(choPhepMotPhan: false);

            var result = await _h.Ledger.CollectManuallyAsync(Cash(700_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, _h.Store.Invoices[7].TrangThaiHoaDon);
            Assert.Equal(1_300_000m, result.Data!.ConLai);
        }

        [Fact]
        public async Task Collect_TwoCashPaymentsInSameSecond_GetDistinctCodes()
        {
            _h.AddSentInvoice();

            await _h.Ledger.CollectManuallyAsync(Cash(500_000m), Staff);
            await _h.Ledger.CollectManuallyAsync(Cash(500_000m), Staff);

            Assert.Equal(2, _h.Store.Ledger.Select(l => l.MaGiaoDich).Distinct().Count());
        }

        [Fact]
        public async Task Collect_WithWaitingRequest_CancelsRequestThenCollects()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();

            var result = await _h.Ledger.CollectManuallyAsync(Cash(2_000_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, request.TrangThai);
            Assert.Equal(request.MaYeuCau, result.Data!.MaLuotDaHuy);
            var history = request.LichSuTrangThaiYeuCauThanhToanHoaDons.Last();
            Assert.Equal("Đã thu bằng phương thức khác", history.LyDo);
            Assert.Equal(Staff, history.NguoiThucHienId);
        }

        [Fact]
        public async Task Collect_WithRequestUnderReview_IsBlocked()
        {
            _h.AddSentInvoice();
            _h.AddProof(_h.AddRequest());

            var result = await _h.Ledger.CollectManuallyAsync(Cash(2_000_000m), Staff);

            Assert.False(result.Success);
            Assert.Contains("đối chiếu trước", result.Message);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task Collect_TransferWithoutCodeAndNote_IsBlocked_ButNoteAllowsGeneratedCode()
        {
            _h.AddSentInvoice();

            var noNote = await _h.Ledger.CollectManuallyAsync(Transfer(2_000_000m, null), Staff);
            var withNote = await _h.Ledger.CollectManuallyAsync(Transfer(1_000_000m, null, "Chuyển gộp FT999 cho 2 hóa đơn"), Staff);

            Assert.False(noNote.Success);
            Assert.True(withNote.Success, withNote.Message);
            Assert.StartsWith("CK-", _h.Store.Ledger.Single().MaGiaoDich);
        }

        [Fact]
        public async Task Collect_NoteWithReservedTag_IsRejected()
        {
            _h.AddSentInvoice();

            var result = await _h.Ledger.CollectManuallyAsync(Cash(500_000m, "[TIEN-THUA:9000000] giả"), Staff);

            Assert.False(result.Success);
            Assert.Empty(_h.Store.Ledger);
        }

        [Fact]
        public async Task Collect_DuplicateBankCode_IsBlocked()
        {
            _h.AddSentInvoice();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddLedger(8, 100_000m, "FT777");

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(500_000m, "ft777"), Staff);

            Assert.False(result.Success);
            Assert.Equal("Mã giao dịch đã được ghi nhận.", result.Message);
        }

        [Fact]
        public async Task Collect_OverDebt_StaffBlocked_AdminRecordsDebtWithTag()
        {
            _h.AddSentInvoice();

            var staff = await _h.Ledger.CollectManuallyAsync(Cash(2_300_000m, "dư"), Staff);
            var adminNoNote = await _h.Ledger.CollectManuallyAsync(Cash(2_300_000m), Admin);
            var admin = await _h.Ledger.CollectManuallyAsync(Cash(2_300_000m, "Trừ kỳ sau"), Admin);

            Assert.Equal("Số tiền vượt số còn nợ, cần Admin xử lý.", staff.Message);
            Assert.False(adminNoNote.Success);
            Assert.True(admin.Success, admin.Message);
            var ledger = _h.Store.Ledger.Single();
            Assert.Equal(2_000_000m, ledger.SoTienThanhToan);
            Assert.Equal("[TIEN-THUA:300000] Trừ kỳ sau", ledger.GhiChu);
            Assert.Equal(300_000m, admin.Data!.TienThua);
        }

        [Fact]
        public async Task Collect_OtherBranchStaff_Forbidden()
        {
            _h.AddSentInvoice();

            var result = await _h.Ledger.CollectManuallyAsync(Cash(2_000_000m), OtherStaff);

            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot)]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        public async Task Collect_InvoiceNotSent_Fails(TrangThaiPhatHanhHoaDon status)
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = status;

            var result = await _h.Ledger.CollectManuallyAsync(Cash(2_000_000m), Staff);

            Assert.False(result.Success);
            Assert.True(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task Collect_FutureDate_OrFraction_Fails()
        {
            _h.AddSentInvoice();
            var future = Cash(100_000m);
            future.NgayThanhToanUtc = Now.AddHours(1);

            Assert.False((await _h.Ledger.CollectManuallyAsync(future, Staff)).Success);
            Assert.False((await _h.Ledger.CollectManuallyAsync(Cash(100_000.5m), Staff)).Success);
        }

        [Fact]
        public async Task Collect_PaidBeforeLockAcquired_Rejects()
        {
            _h.AddSentInvoice();
            _h.Store.OnLock = hd =>
            {
                if (!_h.Store.Ledger.Any())
                {
                    _h.AddLedger(hd.HoaDonId, 2_000_000m, "TM-OTHER");
                }
            };

            var result = await _h.Ledger.CollectManuallyAsync(Cash(2_000_000m), Staff);

            Assert.False(result.Success);
            Assert.Single(_h.Store.Ledger);
        }

        // ---------- Hủy ghi nhận (§20) ----------

        [Fact]
        public async Task Void_WithOtherPaymentsLeft_GoesToPartial_AndAlwaysWritesHistory()
        {
            _h.AddSentInvoice();
            var first = _h.AddLedger(7, 500_000m, "TM-1");
            _h.AddLedger(7, 1_500_000m, "TM-2");

            var result = await _h.Ledger.VoidPaymentAsync(first.LichSuThanhToanId, Admin, "Ghi nhầm");

            Assert.True(result.Success, result.Message);
            Assert.True(first.IsDeleted);
            Assert.Contains("[DA-HUY 03/10/2026 10:00 bởi #1] Ghi nhầm", first.GhiChu);
            var hoaDon = _h.Store.Invoices[7];
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, hoaDon.TrangThaiHoaDon);
            var history = hoaDon.LichSuTrangThaiHoaDons.Single();
            Assert.Equal(Admin, history.NguoiThucHienId);
            Assert.Equal(Now, history.NgayThucHien);
            Assert.Contains("Hủy ghi nhận thanh toán TM-1 (500.000đ): Ghi nhầm", history.LyDo);
        }

        [Fact]
        public async Task Void_LastPayment_GoesBackToUnpaid_ProofAndRequestUntouched()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();
            var proof = _h.AddProof(request);
            await _h.Confirmation.ConfirmAsync(new ConfirmPaymentRequest
            {
                MinhChungId = proof.MinhChungThanhToanHoaDonId,
                SoTienThucNhan = 2_000_000m,
                NgayGiaoDichUtc = Now.AddHours(-1)
            }, Staff);
            var ledger = _h.Store.Ledger.Single();

            var result = await _h.Ledger.VoidPaymentAsync(ledger.LichSuThanhToanId, Admin, "Ngân hàng hoàn giao dịch");

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
            Assert.Equal(TrangThaiMinhChungThanhToan.DaXacNhan, proof.TrangThaiDoiChieu);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
        }

        [Fact]
        public async Task Void_StaffForbidden_EmptyReasonRejected_TwiceRejected()
        {
            _h.AddSentInvoice();
            var ledger = _h.AddLedger(7, 500_000m);

            Assert.Equal(ServiceErrorKind.Forbidden, (await _h.Ledger.VoidPaymentAsync(ledger.LichSuThanhToanId, Staff, "x")).ErrorKind);
            Assert.False((await _h.Ledger.VoidPaymentAsync(ledger.LichSuThanhToanId, Admin, " ")).Success);
            Assert.True((await _h.Ledger.VoidPaymentAsync(ledger.LichSuThanhToanId, Admin, "sai")).Success);
            Assert.False((await _h.Ledger.VoidPaymentAsync(ledger.LichSuThanhToanId, Admin, "sai")).Success);
        }

        // ---------- Hủy hóa đơn (§31.2) ----------

        [Fact]
        public async Task CancelInvoice_WithWaitingRequest_CancelsRequestAndInvoice()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest();

            var result = await _h.Ledger.CancelInvoiceAsync(7, Staff, "Lập sai");

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHuy, request.TrangThai);
            Assert.Equal("Hóa đơn bị hủy: Lập sai", request.LichSuTrangThaiYeuCauThanhToanHoaDons.Last().LyDo);
            var hoaDon = _h.Store.Invoices[7];
            Assert.Equal(TrangThaiPhatHanhHoaDon.DaHuy, hoaDon.TrangThaiPhatHanh);
            Assert.True(hoaDon.IsDeleted);
            Assert.Equal(Now, hoaDon.LichSuTrangThaiHoaDons.Last().NgayThucHien);
        }

        [Fact]
        public async Task CancelInvoice_StatusPaidWithoutLedger_IsBusinessError()
        {
            _h.AddSentInvoice().TrangThaiHoaDon = TrangThaiHoaDon.ThanhToanMotPhan;

            var result = await _h.Ledger.CancelInvoiceAsync(7, Staff, "Lập sai");

            Assert.False(result.Success);
            Assert.Contains("đã phát sinh khoản thanh toán", result.Message);
            Assert.False(_h.Store.Invoices[7].IsDeleted);
        }

        [Fact]
        public async Task CancelInvoice_WithRequestUnderReview_IsBlocked()
        {
            _h.AddSentInvoice();
            _h.AddProof(_h.AddRequest());

            var result = await _h.Ledger.CancelInvoiceAsync(7, Staff, "Lập sai");

            Assert.False(result.Success);
            Assert.Contains("đối chiếu trước khi hủy", result.Message);
            Assert.False(_h.Store.Invoices[7].IsDeleted);
        }

        [Fact]
        public async Task CancelInvoice_ExpiredRequest_DoesNotBlock()
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(createdAt: Now.AddHours(-30));

            var result = await _h.Ledger.CancelInvoiceAsync(7, Staff, "Lập sai");

            Assert.True(result.Success, result.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.HetHan, request.TrangThai);
        }

        [Fact]
        public async Task CancelInvoice_WithPayment_IsBlocked()
        {
            _h.AddSentInvoice();
            _h.AddLedger(7, 100_000m);

            var result = await _h.Ledger.CancelInvoiceAsync(7, Staff, "Lập sai");

            Assert.False(result.Success);
            Assert.Contains("đã phát sinh khoản thanh toán", result.Message);
        }

        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap)]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet)]
        public async Task CancelInvoice_DraftOrPending_IsRejected(TrangThaiPhatHanhHoaDon status)
        {
            _h.AddSentInvoice().TrangThaiPhatHanh = status;

            var result = await _h.Ledger.CancelInvoiceAsync(7, Staff, "Lập sai");

            Assert.False(result.Success);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public async Task CancelInvoice_WithoutReason_IsRejected(string? lyDo)
        {
            _h.AddSentInvoice();

            var result = await _h.Ledger.CancelInvoiceAsync(7, Staff, lyDo!);

            Assert.False(result.Success);
            Assert.Empty(_h.UnitOfWork.Transactions);
        }

        [Fact]
        public async Task CancelInvoice_OtherBranchStaff_Forbidden_AlreadyCancelled_NotFound()
        {
            _h.AddSentInvoice();

            Assert.Equal(ServiceErrorKind.Forbidden, (await _h.Ledger.CancelInvoiceAsync(7, OtherStaff, "x")).ErrorKind);
            Assert.True((await _h.Ledger.CancelInvoiceAsync(7, Staff, "x")).Success);
            Assert.Equal(ServiceErrorKind.NotFound, (await _h.Ledger.CancelInvoiceAsync(7, Staff, "x")).ErrorKind);
        }
    }
}
