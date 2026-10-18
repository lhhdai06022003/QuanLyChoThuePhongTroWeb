using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // QR trong PDF hóa đơn (spec thanh toán trực tuyến §7.1): nội dung là mã chuyển khoản TTPTHD{Id}, số tiền là số còn lại.
    public class InvoicePdfComposerTests
    {
        private sealed class RecordingExporter : IInvoiceDocumentExporter
        {
            public int PdfCalls { get; private set; }
            public byte[]? QrBytes { get; private set; }
            public string? BankId { get; private set; }
            public string? AccountNumber { get; private set; }
            public string? AccountName { get; private set; }

            public byte[] ExportExcel(HoaDonChiTietRes hoaDon) => Array.Empty<byte>();

            public byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName)
            {
                PdfCalls++;
                QrBytes = qrBytes;
                BankId = bankId;
                AccountNumber = accountNumber;
                AccountName = accountName;
                return new byte[] { 1, 2, 3 };
            }
        }

        private readonly RecordingVietQr _vietQr = new();
        private readonly RecordingExporter _exporter = new();
        private readonly VietQrSettings _settings = new() { BankId = "MB", AccountNumber = "0123456789", AccountName = "NHA TRO" };

        private static HoaDonChiTietRes Invoice(decimal tong = 2_000_000m, decimal conLai = 1_500_000m, bool daHuy = false) => new()
        {
            HoaDonId = 7,
            MaHoaDon = "HD-202609-0007",
            TongTien = tong,
            DaThu = tong - conLai,
            ConLai = conLai,
            DaHuy = daHuy
        };

        [Fact]
        public void PartiallyPaidInvoice_QrCarriesTheTransferCodeAndTheRemainingAmount()
        {
            var bytes = InvoicePdfComposer.Compose(Invoice(), _vietQr, _settings, _exporter);

            Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
            Assert.Equal("TTPTHD7", _vietQr.LastMemo);
            Assert.Equal(1_500_000m, _vietQr.LastAmount);
            Assert.NotNull(_exporter.QrBytes);
            Assert.Equal("MB", _exporter.BankId);
            Assert.Equal("0123456789", _exporter.AccountNumber);
            Assert.Equal("NHA TRO", _exporter.AccountName);
        }

        [Fact]
        public void TheMemoNoLongerUsesTheInvoiceCodeText()
        {
            InvoicePdfComposer.Compose(Invoice(), _vietQr, _settings, _exporter);

            Assert.DoesNotContain("THANH TOAN", _vietQr.LastMemo);
            Assert.DoesNotContain("HD-202609-0007", _vietQr.LastMemo);
        }

        [Fact]
        public void FullyPaidInvoice_HasNoQr()
        {
            InvoicePdfComposer.Compose(Invoice(conLai: 0m), _vietQr, _settings, _exporter);

            Assert.Equal(1, _exporter.PdfCalls);
            Assert.Null(_exporter.QrBytes);
            Assert.Null(_vietQr.LastMemo);
            Assert.Equal(string.Empty, _exporter.BankId);
        }

        [Fact]
        public void CancelledInvoice_HasNoQr_EvenWhenSomethingIsStillOwed()
        {
            InvoicePdfComposer.Compose(Invoice(daHuy: true), _vietQr, _settings, _exporter);

            Assert.Null(_exporter.QrBytes);
            Assert.Null(_vietQr.LastMemo);
        }
    }
}
