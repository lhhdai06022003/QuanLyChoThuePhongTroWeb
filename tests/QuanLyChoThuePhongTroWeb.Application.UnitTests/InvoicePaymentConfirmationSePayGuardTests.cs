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
    // Chặn ghi trùng khi xác nhận minh chứng cũ hoặc thu thủ công mà webhook SePay đã ghi cùng khoản tiền
    // (spec thanh toán trực tuyến §5.4). Minh chứng của PaymentTestHarness được nộp lúc Now - 20 phút.
    public class InvoicePaymentConfirmationSePayGuardTests
    {
        private const int Staff = PaymentTestHarness.StaffId;
        private const int Admin = PaymentTestHarness.AdminId;
        private const string SePayAlreadyRecorded = "Mã giao dịch này đã được hệ thống ghi nhận tự động qua SePay.";
        private static readonly DateTime Now = PaymentTestHarness.Now;
        private static readonly DateTime BeforeTheProof = Now.AddMinutes(-30);
        private static readonly DateTime AfterTheProof = Now.AddMinutes(-10);

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

        // ---------- (a) SePay đã thu đủ sau khi khách nộp ----------

        [Theory]
        [InlineData(Staff)]
        [InlineData(Admin)]
        public async Task SePayPaidInFullAfterTheProof_IsRejectedForStaffAndAdmin_AndNothingIsAdded(int actor)
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 2_000_000m, "FT26ABC", AfterTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ghiChu: "đã kiểm tra"), actor);

            Assert.False(result.Success);
            Assert.Contains("đã được thanh toán đủ", result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, proof.TrangThaiDoiChieu);
        }

        // ---------- (b) SePay thu một phần sau khi khách nộp ----------

        [Fact]
        public async Task SePayPartialAfterTheProof_StaffIsSentToAdmin_AndTheLedgerIsUnchanged()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "khách chuyển nốt"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Đã chuyển Admin xử lý.", result.Message);
            Assert.Equal(KetQuaDoiChieu.DaChuyenAdmin, result.Data!.KetQua);
            Assert.Equal(0m, result.Data.SoTienGhiNhan);
            Assert.Equal(0m, result.Data.ChenhLech);
            Assert.Equal(500_000m, result.Data.ConLai);
            Assert.Equal("Thanh toán một phần", result.Data.TrangThaiHoaDon);
            Assert.Equal("Hóa đơn đã nhận chuyển khoản tự động sau khi khách nộp minh chứng, đã chuyển Admin kiểm trùng.", result.Data.Message);

            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, proof.TrangThaiDoiChieu);

            var reason = LatestHistoryReason(request);
            Assert.StartsWith("[CHO-ADMIN]", reason);
            Assert.Contains("SEPAY-FT26ABC", reason);
            Assert.Contains("thực nhận 500.000", reason);
            Assert.EndsWith("khách chuyển nốt", reason);
            Assert.True(PaymentNoteTags.IsWaitingAdmin(request));
            Assert.Equal(Staff, request.LichSuTrangThaiYeuCauThanhToanHoaDons.Last().NguoiThucHienId);
            Assert.True(_h.LastTx.Committed);
        }

        [Fact]
        public async Task SePayPartialAfterTheProof_AdminIsNotified_WithALinkToTheReviewScreen()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);

            await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            var notification = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == Admin);
            Assert.Equal("Thanh toán cần Admin kiểm trùng", notification.TieuDe);
            Assert.Contains(request.MaYeuCau, notification.NoiDung);
            Assert.Contains("HD-7", notification.NoiDung);
            Assert.Contains("SEPAY-FT26ABC", notification.NoiDung);
            Assert.Contains("500.000đ", notification.NoiDung);
            Assert.Equal($"/QuanLyNhaTro/DoiChieuThanhToan?ma={request.MaYeuCau}", notification.LinkDieuHuong);
            Assert.DoesNotContain(_h.Store.Notifications, n => n.NguoiDungId == PaymentTestHarness.TenantUserId);
            Assert.Contains(_h.Notifier.Sent, s => s.UserId == Admin);
        }

        [Fact]
        public async Task SePayPartialAfterTheProof_StaffCannotRetry_BecauseTheRequestWaitsForAdmin()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            var retry = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "thử lại"), Staff);

            Assert.False(retry.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, retry.ErrorKind);
            Assert.Contains("đang chờ Admin", retry.Message);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task SePayPartialAfterTheProof_AdminNeedsANote()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);

            var withoutNote = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Admin);
            var blankNote = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "   "), Admin);

            Assert.False(withoutNote.Success);
            Assert.False(blankNote.Success);
            Assert.Equal("Hóa đơn đã nhận chuyển khoản tự động sau khi khách nộp minh chứng, vui lòng ghi chú kết quả kiểm trùng.", withoutNote.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.ChoXacNhan, proof.TrangThaiDoiChieu);
        }

        [Fact]
        public async Task SePayPartialAfterTheProof_AdminWithANote_RecordsTheLedgerAsUsual()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "đã đối chiếu sao kê: khoản khác"), Admin);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, _h.Store.Ledger.Count);
            var added = _h.Store.Ledger.Last();
            Assert.Equal(500_000m, added.SoTienThanhToan);
            Assert.Equal(Admin, added.NguoiXacNhanId);
            Assert.Equal(proof.MinhChungThanhToanHoaDonId, added.MinhChungThanhToanHoaDonId);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
            Assert.Equal(TrangThaiMinhChungThanhToan.DaXacNhan, proof.TrangThaiDoiChieu);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task SePayPartialAfterTheProof_AdminAfterStaffWasSentToAdmin_CanFinish()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);
            await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "khoản khác với dòng SePay"), Admin);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, _h.Store.Ledger.Count);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
        }

        [Fact]
        public async Task SePayRecordedAtTheSameInstantAsTheProof_Counts()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", proof.NgayTao);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            Assert.Equal(KetQuaDoiChieu.DaChuyenAdmin, result.Data!.KetQua);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task TwoSePayRowsAfterTheProof_NameTheEarliestOne()
        {
            var (request, proof) = PendingProof(tongTien: 3_000_000m, soTienLuot: 3_000_000m);
            _h.AddSePayLedger(7, 500_000m, "FT-LATE", AfterTheProof);
            _h.AddSePayLedger(7, 500_000m, "FT-EARLY", Now.AddMinutes(-15));

            await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Staff);

            var reason = LatestHistoryReason(request);
            Assert.Contains("SEPAY-FT-EARLY", reason);
            Assert.DoesNotContain("SEPAY-FT-LATE", reason);
        }

        // ---------- (c) SePay ghi trước khi khách nộp minh chứng ----------

        [Fact]
        public async Task SePayRecordedBeforeTheProof_StaffConfirmsAsUsual()
        {
            var (request, proof) = PendingProof(soTienLuot: 1_500_000m);
            _h.AddSePayLedger(7, 500_000m, "FT-OLD", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
            Assert.Equal(2, _h.Store.Ledger.Count);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
        }

        // ---------- (d) SePay đã hủy ghi nhận ----------

        [Fact]
        public async Task VoidedSePayRowAfterTheProof_DoesNotBlock()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 500_000m, "FT-DA-HUY", AfterTheProof, isDeleted: true);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
            Assert.Equal(2, _h.Store.Ledger.Count);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        // ---------- (e) Mã giao dịch trùng dòng SePay ----------

        [Fact]
        public async Task StaffCodeMatchingASePayRow_IsRejected_IgnoringSpacesAndCase()
        {
            var (request, proof) = PendingProof();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: " ft26 abc ", ghiChu: "x"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
        }

        [Fact]
        public async Task StaffCodeMatchingASePayRow_IsRejected_ForAdminToo()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: "FT26ABC", ghiChu: "x"), Admin);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.Single(_h.Store.Ledger);
        }

        [Fact]
        public async Task StaffCodeMatchingAVoidedSePayRow_IsNotBlockedByTheCode()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof, isDeleted: true);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ma: "FT26ABC"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal("FT26ABC", _h.Store.Ledger.Last().MaGiaoDich);
        }

        [Fact]
        public async Task StaffCodeMatchingASePayRowOfAnotherInvoice_IsRejected()
        {
            var (_, proof) = PendingProof();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddSePayLedger(8, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m, ma: "FT26ABC"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
        }

        [Fact]
        public async Task StaffCodeThatMatchesNoSePayRow_IsNotBlocked()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 500_000m, "FT-KHAC", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: "FT26ABC", ghiChu: "x"), Staff);

            Assert.True(result.Success, result.Message);
        }

        // ---------- Q2: mã khách khai trên minh chứng ----------

        [Fact]
        public async Task DeclaredCodeMatchingASePayRow_IsRejected_EvenWhenStaffTypedNothing()
        {
            var (request, proof) = PendingProof(maKhaiBao: "FT26ABC");
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ghiChu: "x"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
        }

        [Fact]
        public async Task DeclaredCodeMatchingASePayRow_IsRejected_WhenStaffTypedADifferentCode()
        {
            var (_, proof) = PendingProof(maKhaiBao: " ft26 abc ");
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: "FT-NHAN-VIEN", ghiChu: "x"), Admin);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
        }

        [Fact]
        public async Task DeclaredCodeMatchingAVoidedSePayRow_IsNotBlocked()
        {
            var (_, proof) = PendingProof(maKhaiBao: "FT26ABC");
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof, isDeleted: true);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Staff);

            Assert.True(result.Success, result.Message);
        }

        [Fact]
        public async Task BlankDeclaredAndStaffCodes_AreNotChecked()
        {
            var (_, proof) = PendingProof(maKhaiBao: "   ");
            _h.AddSePayLedger(7, 500_000m, "FT-OLD", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: "  ", ghiChu: "x"), Staff);

            Assert.True(result.Success, result.Message);
        }

        // ---------- (f) Dòng SEPAY- có người xác nhận ----------

        [Fact]
        public async Task SePayCodedRowConfirmedByAPerson_DoesNotTriggerTheAfterProofRule()
        {
            var (request, proof) = PendingProof(soTienLuot: 500_000m);
            var row = _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);
            row.NguoiXacNhanId = Admin;

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
            Assert.Equal(2, _h.Store.Ledger.Count);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
        }

        [Fact]
        public async Task OrdinaryLedgerRowAfterTheProof_DoesNotTriggerTheRule()
        {
            var (_, proof) = PendingProof(soTienLuot: 500_000m);
            var row = _h.AddLedger(7, 1_500_000m, "GD-TAY");
            row.NgayXacNhan = AfterTheProof;

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
        }

        // ---------- Q3: thu thủ công ----------

        private static ManualCollectionRequest Transfer(decimal soTien, string? ma, string? ghiChu = null) => new()
        {
            HoaDonId = 7,
            SoTienThucNhan = soTien,
            PhuongThuc = AppPhuongThucThanhToan.ChuyenKhoan,
            NgayThanhToanUtc = Now.AddMinutes(-5),
            MaGiaoDich = ma,
            GhiChu = ghiChu
        };

        [Theory]
        [InlineData(Staff)]
        [InlineData(Admin)]
        public async Task ManualCollection_WithACodeRecordedBySePay_IsRejected_IgnoringSpacesAndCase(int actor)
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, " ft26 abc "), actor);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task ManualCollection_WithACodeRecordedBySePayOnAnotherInvoice_IsRejected()
        {
            _h.AddSentInvoice();
            _h.AddSentInvoice(hoaDonId: 8);
            _h.AddSePayLedger(8, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(2_000_000m, "FT26ABC"), Staff);

            Assert.False(result.Success);
            Assert.Equal(SePayAlreadyRecorded, result.Message);
            Assert.Empty(_h.Store.Ledger.Where(l => l.HoaDonId == 7));
        }

        [Fact]
        public async Task ManualCollection_WithACodeOfAVoidedSePayRow_IsAccepted()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof, isDeleted: true);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(2_000_000m, "FT26ABC"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal("FT26ABC", _h.Store.Ledger.Last().MaGiaoDich);
        }

        [Fact]
        public async Task ManualCollection_WithADifferentCode_IsAccepted()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, "FT-KHAC"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, _h.Store.Ledger.Count);
        }

        [Fact]
        public async Task ManualCollection_InCash_IsNotAffected()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Ledger.CollectManuallyAsync(new ManualCollectionRequest
            {
                HoaDonId = 7,
                SoTienThucNhan = 1_500_000m,
                PhuongThuc = AppPhuongThucThanhToan.TienMat,
                NgayThanhToanUtc = Now.AddMinutes(-5)
            }, Staff);

            Assert.True(result.Success, result.Message);
        }

        // T4 (26/10/2026): mã có tiền tố SEPAY- bị chặn ngay ở bước kiểm dữ liệu nhập, trước cả kiểm trùng.
        [Fact]
        public async Task ManualCollection_WithExactSePayKeyTyped_IsStillRejected_ByTheSystemPrefixRule()
        {
            _h.AddSentInvoice();
            _h.AddSePayLedger(7, 500_000m, "FT26ABC", BeforeTheProof);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, "SEPAY-FT26ABC"), Staff);

            Assert.False(result.Success);
            Assert.Equal(InvoicePaymentPolicy.SystemPrefixMessage, result.Message);
            Assert.Single(_h.Store.Ledger);
        }

        // ---------- Khóa SEPAY- dùng chung ----------

        [Theory]
        [InlineData("FT26ABC", "SEPAY-FT26ABC")]
        [InlineData("ft26abc", "SEPAY-FT26ABC")]
        [InlineData(" ft26 abc ", "SEPAY-FT26ABC")]
        [InlineData("FT-26\tABC", "SEPAY-FT-26ABC")]
        public void SePayLedgerKey_RemovesWhitespace_AndUpperCases(string reference, string expected)
        {
            Assert.Equal(expected, InvoicePaymentPolicy.SePayLedgerKey(reference));
            Assert.StartsWith(InvoicePaymentPolicy.SePayMaGiaoDichPrefix, expected);
        }
    }
}
