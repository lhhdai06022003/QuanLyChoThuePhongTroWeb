using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Chặn ghi trùng khi thu thủ công hoặc xác nhận minh chứng mà IPN VNPay đã ghi cùng khoản tiền (spec VNPay §6.1),
    // cùng khuôn với InvoicePaymentConfirmationSePayGuardTests. Minh chứng của PaymentTestHarness được nộp lúc Now - 20 phút.
    public class InvoicePaymentVnPayGuardTests
    {
        private const int Staff = PaymentTestHarness.StaffId;
        private const int Admin = PaymentTestHarness.AdminId;
        private const string VnPayAlreadyRecorded = "Mã giao dịch này đã được hệ thống ghi nhận tự động qua VNPay.";
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

        private static ManualCollectionRequest Transfer(decimal soTien, string? ma, string? ghiChu = null) => new()
        {
            HoaDonId = 7,
            SoTienThucNhan = soTien,
            PhuongThuc = AppPhuongThucThanhToan.ChuyenKhoan,
            NgayThanhToanUtc = Now.AddMinutes(-5),
            MaGiaoDich = ma,
            GhiChu = ghiChu
        };

        // ---------- Thu thủ công ----------

        [Theory]
        [InlineData(Staff)]
        [InlineData(Admin)]
        public async Task ManualCollection_WithACodeRecordedByVnPay_IsRejected_IgnoringSpacesAndCase(int actor)
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 500_000m, "14123456", BeforeTheProof);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, " 14123 456 "), actor);

            Assert.False(result.Success);
            Assert.Equal(VnPayAlreadyRecorded, result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiHoaDon.ThanhToanMotPhan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task ManualCollection_WithACodeOfAVoidedVnPayRow_IsAccepted()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 500_000m, "14123456", BeforeTheProof, isDeleted: true);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(2_000_000m, "14123456"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal("14123456", _h.Store.Ledger.Last().MaGiaoDich);
        }

        [Fact]
        public async Task ManualCollection_WithADifferentCode_IsAccepted()
        {
            _h.AddSentInvoice();
            _h.AddVnPayLedger(7, 500_000m, "14123456", BeforeTheProof);

            var result = await _h.Ledger.CollectManuallyAsync(Transfer(1_500_000m, "FT-KHAC"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, _h.Store.Ledger.Count);
        }

        // ---------- Mã giao dịch của minh chứng ----------

        [Theory]
        [InlineData(Staff)]
        [InlineData(Admin)]
        public async Task StaffCodeMatchingAVnPayRow_IsRejected_ForStaffAndAdmin(int actor)
        {
            var (request, proof) = PendingProof();
            _h.AddVnPayLedger(7, 500_000m, "14123456", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ma: " 14123456 ", ghiChu: "x"), actor);

            Assert.False(result.Success);
            Assert.Equal(VnPayAlreadyRecorded, result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
        }

        [Fact]
        public async Task DeclaredCodeMatchingAVnPayRow_IsRejected_EvenWhenStaffTypedNothing()
        {
            var (request, proof) = PendingProof(maKhaiBao: "14123456");
            _h.AddVnPayLedger(7, 500_000m, "14123456", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m, ghiChu: "x"), Staff);

            Assert.False(result.Success);
            Assert.Equal(VnPayAlreadyRecorded, result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
        }

        [Fact]
        public async Task CodeMatchingAVoidedVnPayRow_IsNotBlocked()
        {
            var (_, proof) = PendingProof(maKhaiBao: "14123456");
            _h.AddVnPayLedger(7, 500_000m, "14123456", BeforeTheProof, isDeleted: true);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 2_000_000m), Staff);

            Assert.True(result.Success, result.Message);
        }

        // ---------- VNPay ghi sau khi khách nộp minh chứng ----------

        [Fact]
        public async Task VnPayPartialAfterTheProof_StaffIsSentToAdmin_WithTheVnPayWording()
        {
            var (request, proof) = PendingProof();
            _h.AddVnPayLedger(7, 1_500_000m, "14123456", AfterTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "khách chuyển nốt"), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaChuyenAdmin, result.Data!.KetQua);
            Assert.Equal("Hóa đơn đã nhận thanh toán VNPay tự động sau khi khách nộp minh chứng, đã chuyển Admin kiểm trùng.", result.Data.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);

            var reason = LatestHistoryReason(request);
            Assert.StartsWith("[CHO-ADMIN] Hóa đơn đã nhận thanh toán VNPay tự động VNPAY-14123456 sau khi khách nộp minh chứng;", reason);
            Assert.DoesNotContain("chuyển khoản tự động", reason);
            Assert.Contains("thực nhận 500.000", reason);
            Assert.EndsWith("khách chuyển nốt", reason);
            Assert.True(PaymentNoteTags.IsWaitingAdmin(request));

            var notification = Assert.Single(_h.Store.Notifications, n => n.NguoiDungId == Admin);
            Assert.Equal("Thanh toán cần Admin kiểm trùng", notification.TieuDe);
            Assert.Contains("hóa đơn đã nhận thanh toán VNPay tự động VNPAY-14123456 sau khi khách nộp minh chứng", notification.NoiDung);
            Assert.DoesNotContain("chuyển khoản tự động", notification.NoiDung);
        }

        [Fact]
        public async Task VnPayPartialAfterTheProof_AdminNeedsANote_WithTheVnPayWording()
        {
            var (request, proof) = PendingProof();
            _h.AddVnPayLedger(7, 1_500_000m, "14123456", AfterTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Admin);

            Assert.False(result.Success);
            Assert.Equal("Hóa đơn đã nhận thanh toán VNPay tự động sau khi khách nộp minh chứng, vui lòng ghi chú kết quả kiểm trùng.", result.Message);
            Assert.Single(_h.Store.Ledger);
            Assert.Equal(TrangThaiYeuCauThanhToan.DangDoiChieu, request.TrangThai);
        }

        [Fact]
        public async Task VnPayPartialAfterTheProof_AdminWithANote_RecordsTheLedgerAsUsual()
        {
            var (request, proof) = PendingProof();
            _h.AddVnPayLedger(7, 1_500_000m, "14123456", AfterTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m, ghiChu: "đã đối chiếu: khoản khác"), Admin);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, _h.Store.Ledger.Count);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
            Assert.Equal(TrangThaiHoaDon.DaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task VnPayRecordedBeforeTheProof_StaffConfirmsAsUsual()
        {
            var (request, proof) = PendingProof(soTienLuot: 1_500_000m);
            _h.AddVnPayLedger(7, 500_000m, "14123456", BeforeTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 1_500_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
            Assert.Equal(2, _h.Store.Ledger.Count);
            Assert.Equal(TrangThaiYeuCauThanhToan.DaHoanTat, request.TrangThai);
            Assert.False(PaymentNoteTags.IsWaitingAdmin(request));
        }

        [Fact]
        public async Task VnPayRowConfirmedByAPerson_DoesNotTriggerTheAfterProofRule()
        {
            var (_, proof) = PendingProof(soTienLuot: 500_000m);
            var row = _h.AddVnPayLedger(7, 1_500_000m, "14123456", AfterTheProof);
            row.NguoiXacNhanId = Admin;

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            Assert.True(result.Success, result.Message);
            Assert.Equal(KetQuaDoiChieu.DaXacNhan, result.Data!.KetQua);
        }

        [Fact]
        public async Task SePayWording_IsUnchanged_ForASePayRowAfterTheProof()
        {
            var (_, proof) = PendingProof();
            _h.AddSePayLedger(7, 1_500_000m, "FT26ABC", AfterTheProof);

            var result = await _h.Confirmation.ConfirmAsync(Confirm(proof, 500_000m), Staff);

            Assert.Equal("Hóa đơn đã nhận chuyển khoản tự động sau khi khách nộp minh chứng, đã chuyển Admin kiểm trùng.", result.Data!.Message);
        }
    }
}
