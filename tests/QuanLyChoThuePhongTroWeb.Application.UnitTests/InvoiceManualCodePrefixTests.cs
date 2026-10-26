using System;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // T4 (người dùng chọn 26/10/2026): mã giao dịch nhân viên nhập tay không được mang tiền tố SEPAY-/VNPAY- của dòng sổ
    // hệ thống tự ghi, ở cả thu thủ công lẫn xác nhận minh chứng cũ. Không phân biệt hoa thường, sau khi cắt khoảng trắng hai đầu.
    public class InvoiceManualCodePrefixTests
    {
        private static readonly DateTime Now = PaymentTestHarness.Now;

        private readonly PaymentTestHarness _h = new();

        public static TheoryData<string> PrefixedCodes => new()
        {
            "VNPAY-14123456",
            "vnpay-14123456",
            "SEPAY-FT26ABC",
            "Sepay-ID-92704",
            "  VNPAY-1  "
        };

        [Theory]
        [MemberData(nameof(PrefixedCodes))]
        public void Validate_RejectsSystemPrefixes(string code)
        {
            Assert.Equal(InvoicePaymentPolicy.SystemPrefixMessage, InvoicePaymentPolicy.ValidateMaGiaoDich(InvoicePaymentPolicy.NormalizeText(code)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("14123456")]
        [InlineData("FT26ABC")]
        [InlineData("VNPAY14123456")]
        [InlineData("GD-SEPAY-1")]
        public void Validate_AcceptsCodesWithoutTheSystemPrefix(string? code)
        {
            Assert.Null(InvoicePaymentPolicy.ValidateMaGiaoDich(code));
        }

        [Fact]
        public void Validate_StillReportsTheLengthFirst()
        {
            Assert.Equal($"Mã giao dịch tối đa {InvoicePaymentPolicy.MaxMaGiaoDichLength} ký tự.",
                InvoicePaymentPolicy.ValidateMaGiaoDich("VNPAY-" + new string('1', InvoicePaymentPolicy.MaxMaGiaoDichLength)));
        }

        [Theory]
        [MemberData(nameof(PrefixedCodes))]
        public async Task ManualCollection_WithAPrefixedCode_IsRejected_AndRecordsNothing(string code)
        {
            _h.AddSentInvoice();

            var result = await _h.Ledger.CollectManuallyAsync(new ManualCollectionRequest
            {
                HoaDonId = 7,
                SoTienThucNhan = 2_000_000m,
                PhuongThuc = AppPhuongThucThanhToan.ChuyenKhoan,
                NgayThanhToanUtc = Now.AddMinutes(-5),
                MaGiaoDich = code
            }, PaymentTestHarness.AdminId);

            Assert.False(result.Success);
            Assert.Equal(InvoicePaymentPolicy.SystemPrefixMessage, result.Message);
            Assert.Empty(_h.Store.Ledger);
            Assert.Empty(_h.UnitOfWork.Transactions);
            Assert.Equal(TrangThaiHoaDon.ChuaThanhToan, _h.Store.Invoices[7].TrangThaiHoaDon);
        }

        [Fact]
        public async Task ManualCollection_WithTheRawCode_IsStillAccepted()
        {
            _h.AddSentInvoice();

            var result = await _h.Ledger.CollectManuallyAsync(new ManualCollectionRequest
            {
                HoaDonId = 7,
                SoTienThucNhan = 2_000_000m,
                PhuongThuc = AppPhuongThucThanhToan.ChuyenKhoan,
                NgayThanhToanUtc = Now.AddMinutes(-5),
                MaGiaoDich = "14123456"
            }, PaymentTestHarness.AdminId);

            Assert.True(result.Success, result.Message);
            Assert.Equal("14123456", Assert.Single(_h.Store.Ledger).MaGiaoDich);
        }

        [Theory]
        [MemberData(nameof(PrefixedCodes))]
        public async Task ProofConfirmation_WithAPrefixedCode_IsRejected_AndRecordsNothing(string code)
        {
            _h.AddSentInvoice();
            var request = _h.AddRequest(soTien: 2_000_000m);
            var proof = _h.AddProof(request, null);

            var result = await _h.Confirmation.ConfirmAsync(new ConfirmPaymentRequest
            {
                MinhChungId = proof.MinhChungThanhToanHoaDonId,
                SoTienThucNhan = 2_000_000m,
                MaGiaoDich = code,
                NgayGiaoDichUtc = Now.AddHours(-1)
            }, PaymentTestHarness.AdminId);

            Assert.False(result.Success);
            Assert.Equal(InvoicePaymentPolicy.SystemPrefixMessage, result.Message);
            Assert.Empty(_h.Store.Ledger);
            Assert.Empty(_h.UnitOfWork.Transactions);
        }
    }
}
