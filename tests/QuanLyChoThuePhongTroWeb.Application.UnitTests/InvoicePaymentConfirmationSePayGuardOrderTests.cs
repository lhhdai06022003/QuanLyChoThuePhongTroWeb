using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Bổ sung cho InvoicePaymentConfirmationSePayGuardTests: thứ tự các kiểm tra §5.4, biên thời gian chính xác đến tick,
    // tổ hợp mã trùng với dòng SePay sau lúc nộp, hàng đợi "chờ Admin" và thu thủ công bị chặn thì rollback.
    public class InvoicePaymentConfirmationSePayGuardOrderTests
    {
        private const int Staff = PaymentTestHarness.StaffId;
        private const int OtherStaff = PaymentTestHarness.OtherStaffId;
        private const int Admin = PaymentTestHarness.AdminId;
        private const string SePayAlreadyRecorded = "Mã giao dịch này đã được hệ thống ghi nhận tự động qua SePay.";
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();

        private (YeuCauThanhToanHoaDon Request, MinhChungThanhToanHoaDon Proof) PendingProof(
            decimal tongTien = 2_000_000m, decimal soTienLuot = 2_000_000m, string? maKhaiBao = null)
        {
            _h.AddSentInvoice(tongTien: tongTien);
            var request = _h.AddRequest(soTien: soTienLuot);
            return (request, _h.AddProof(request, maKhaiBao));
        }

        private ConfirmPaymentRequest Confirm(MinhChungThanhToanHoaDon proof, decimal thucNhan, string? ma = null, string? ghiChu = null) => new()
        {
            MinhChungId = proof.MinhChungThanhToanHoaDonId,
            SoTienThucNhan = thucNhan,
            MaGiaoDich = ma,
            NgayGiaoDichUtc = Now.AddHours(-1),
            GhiChu = ghiChu
        };

        private static string LatestHistoryReason(YeuCauThanhToanHoaDon request) => request.LichSuTrangThaiYeuCauThanhToanHoaDons
            .OrderByDescending(l => l.NgayThucHien)
            .ThenByDescending(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
            .First().LyDo!;

        // ---------- Thứ tự kiểm tra ----------

        [Theory]
        [InlineData(Staff)]
        [InlineData(Admin)]
        public async Task PaidInFull_WinsOverTheSePayCodeCheck(int actor)
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ma: "FT26ABC", ghiChu: "x"), actor);

            Assert.False(result.Success);
            Assert.Contains("đã được thanh toán đủ", result.Message);
            Assert.NotEqual(SePayAlreadyRecorded, result.Message);
        }

        [Fact]
        public async Task TheSePayCodeCheck_WinsOverSendingStaffToAdmin()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ma: "FT26ABC"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
            Assert.Empty(request.LichSuTrangThaiYeuCauThanhToanHoaDons.Where(l => (l.LyDo ?? "").StartsWith("[CHO-ADMIN]")));
            Assert.DoesNotContain(_h.Store.Notifications, n => n.TieuDe == "Thanh toán cần Admin kiểm trùng");
            Assert.True(_h.LastTx.RolledBack);
        }

        [Fact]
        public async Task TheSePayCodeCheck_WinsOverTheAdminNoteRequirement()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ma: "FT26ABC"), Admin);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
        }

        [Fact]
        public async Task TheDeclaredCode_AlsoWinsOverSendingStaffToAdmin()
        {
            var (request, proof) = PendingProof(maKhaiBao: "ft26 abc");
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
        }

        [Fact]
        public async Task TheSePayRule_WinsOverTheVarianceNoteRequirement_ForStaff()
        {
            // Số thực nhận khác số lượt và không ghi chú: lẽ ra báo "vui lòng ghi chú", nhưng dòng SePay sau lúc nộp chuyển Admin trước.
            var (request, proof) = PendingProof(soTienLuot: 2_000_000m);
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 700_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaChuyenAdmin, result.Data!.KetQua);
            Assert.True(PaymentNoteTags.IsWaitingAdmin(request));
        }

        // ---------- Biên thời gian ----------

        [Fact]
        public async Task SePayRowRecordedOneTickBeforeTheProof_DoesNotCount()
        {
            var (_, proof) = PendingProof(soTienLuot: 1_500_000m);
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", proof.NgayTao.AddTicks(-1));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
            Assert.Equal(2, _h.Store.Ledger.Count);
        }

        [Fact]
        public async Task SePayRowRecordedOneTickAfterTheProof_Counts()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", proof.NgayTao.AddTicks(1));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m), Staff);

            Assert.Equal(KetQuaDoiChieu.DaChuyenAdmin, result.Data!.KetQua);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task ASePayRowWithoutAConfirmationTime_NeverCounts()
        {
            var (_, proof) = PendingProof();
            var row = _h.AddSePayLedger(7, 500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));
            row.NgayXacNhan = null;

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ghiChu: "x"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaGhiNhanSoThucNhan, result.Data!.KetQua);
        }

        // ---------- Nhân viên bị chuyển Admin ----------

        [Fact]
        public async Task StaffSentToAdminWithoutANote_HistoryEndsWithTheRule_AndTheLedgerIsUntouched()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.EndsWith("cần Admin kiểm trùng.", LatestHistoryReason(request));
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, _h.Store.Invoices[7].TrangThaiHoaDon);
            Assert.DoesNotContain(_h.Store.Notifications, n => n.NguoiDungId == OtherStaff);
            Assert.DoesNotContain(_h.Store.Notifications, n => n.NguoiDungId == Staff);
        }

        [Fact]
        public async Task StaffOfAnotherBranch_CannotConfirm_SoTheSePayRuleIsNeverReached()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), OtherStaff);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
            Assert.Empty(_h.Store.Notifications);
        }

        [Fact]
        public async Task RequestSentToAdminBecauseOfSePay_ShowsInTheAdminOnlyQueue()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            var queue = await _h.Confirmation.GetReviewQueueAsync(
                Admin, new ReviewQueueFilter { ChiChoAdmin = true }, new DataTableRequest { Draw = 1, Start = 0, Length = 25 });

            Assert.True(queue.Success, queue.Message);
            var row = Assert.Single(queue.Data!.data);
            Assert.True(row.ChoAdmin);
            Assert.Equal("HD-7", row.MaHoaDon);
        }

        [Fact]
        public async Task AdminWithANote_ThenConfirmingAgain_IsRejectedAsAlreadyHandled()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "đã kiểm tra"), Admin);

            var again = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "đã kiểm tra"), Admin);

            Assert.False(again.Success);
            Assert.Contains("đã được xử lý", again.Message);
            Assert.Equal(2, _h.Store.Ledger.Count);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
        }

        [Fact]
        public async Task AdminWithANote_OverpayingAfterASePayRow_StillGoesThroughTheOverpayRule()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao.AddMinutes(5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 800_000m, ghiChu: "khoản khác, khách chuyển dư"), Admin);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhanCoTienThua, result.Data!.KetQua);
            Assert.Equal(500_000m, result.Data.SoTienGhiNhan);
            Assert.Equal(300_000m, result.Data.ChenhLech);
        }

        // ---------- Mã SEPAY- nhân viên nhập và mã khách khai ----------

        [Fact]
        public async Task EitherCodeMatching_IsEnough_WhenTheOtherCodeMatchesNothing()
        {
            var (_, proof) = PendingProof(maKhaiBao: "FT-KHONG-TRUNG");
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", proof.NgayTao.AddMinutes(-5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: "FT26ABC", ghiChu: "x"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
        }

        [Fact]
        public async Task DeclaredCode_RunsAgainstTheSePayRowsOfAnyInvoice()
        {
            var (_, proof) = PendingProof(maKhaiBao: "FT26ABC");
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddSePayLedger(8, 500_000m, "FT26ABC", proof.NgayTao.AddMinutes(-5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
        }

        [Fact]
        public async Task RejectedByTheSePayCodeCheck_LeavesTheProofReviewable()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", proof.NgayTao.AddMinutes(-5));
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: "FT26ABC"), Staff);

            var afterwards = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: "GD-TAY-1", ghiChu: "x"), Staff);

            Assert.True(afterwards.Success, afterwards.Message);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
            Assert.Equal("GD-TAY-1", _h.Store.Ledger.Last().MaGiaoDich);
        }

        [Fact]
        public async Task TheSePayKey_OfALongCode_IsStillMatchedExactly()
        {
            var (_, proof) = PendingProof();
            var code = new string('A', 40);
            _h.AddSePayLedger(7, 500_000m, code, proof.NgayTao.AddMinutes(-5));

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: code.ToLowerInvariant(), ghiChu: "x"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
        }

        // ---------- Thu thủ công ----------

        private static ManualCollectionRequest Transfer(decimal soTien, string? ma, string? ghiChu = null) => new()
        {
            HoaDonId = 7,
            SoTienThucNhan = soTien,
            PhuongThuc = AppPhuongThucThanhToan.ChuyenKhoan,
            NgayThanhToanUtc = Now.AddMinutes(-5),
            MaGiaoDich = ma,
            GhiChu = ghiChu
        };

        [Fact]
        public async Task ManualCollection_RejectedBySePayCode_RollsBack_AndLeavesTheWaitingRequestAlone()
        {
            _h.AddSentInvoice();
            var waiting = _h.AddRequest();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", Now.AddHours(-3));

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, "ft26abc"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.True(_h.LastTx.RolledBack);
            Assert.False(_h.LastTx.Committed);
            Assert.Equal(TrangThaiYeuCauThanhToan.ChoThanhToan, waiting.TrangThai);
            Assert.DoesNotContain(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
        }

        [Fact]
        public async Task ManualCollection_ExactLedgerCodeMessage_WinsOverTheSePayMessage()
        {
            // Mã trùng chính xác một dòng sổ thường: câu cũ "Mã giao dịch đã được ghi nhận." đứng trước câu SePay.
            _h.AddSentInvoice();
            _h.AddLedger(7, 500_000m, "GD-OLD");

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, "GD-OLD"), Staff);

            Assert.False(result.Success);
            Assert.Equal("Mã giao dịch đã được ghi nhận.", result.Message);
        }

        [Fact]
        public async Task ManualCollection_FullyPaidInvoice_ReportsPaidInFull_BeforeTheSePayCodeCheck()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", Now.AddHours(-3));

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_000_000m, "FT26ABC"), Staff);

            Assert.False(result.Success);
            Assert.Contains("đã được thanh toán đủ", result.Message);
        }

        [Fact]
        public async Task ManualCollection_ByAStaffOfAnotherBranch_IsForbidden_BeforeTheSePayCodeCheck()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", Now.AddHours(-3));

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, "FT26ABC"), OtherStaff);

            Assert.False(result.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
        }

        [Fact]
        public async Task ManualCollection_WithoutACode_IsNotChecked_AndNeedsANote()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", Now.AddHours(-3));

            var withoutNote = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, null), Staff);
            var withNote = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, null, "chuyển chung nhiều hóa đơn"), Staff);

            Assert.False(withoutNote.Success);
            Assert.Contains("ghi chú tham chiếu", withoutNote.Message);
            Assert.True(withNote.Success, withNote.Message);
        }
    }
}
