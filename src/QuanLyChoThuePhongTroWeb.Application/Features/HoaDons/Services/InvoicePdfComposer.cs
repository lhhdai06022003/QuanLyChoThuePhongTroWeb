using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public static class InvoicePdfComposer
    {
        public static byte[] Compose(HoaDonChiTietRes hd, IVietQRService vietQr, VietQrSettings settings, IInvoiceDocumentExporter exporter)
        {
            byte[]? qrBytes = null;
            string bankId = "";
            string accountNumber = "";
            string accountName = "";
            if (!hd.DaHuy && hd.ConLai > 0)
            {
                bankId = settings.BankId ?? "MB";
                accountNumber = settings.AccountNumber ?? "";
                accountName = settings.AccountName ?? "";

                // Nội dung là mã chuyển khoản TTPTHD{Id} để quét QR trên PDF cũng tự khớp giao dịch SePay.
                string memo = InvoiceTransferHandler.TransferCode(hd.HoaDonId);
                string qrString = vietQr.GenerateVietQRString(bankId, accountNumber, hd.ConLai, memo);
                qrBytes = vietQr.GenerateQRCodePNGBytes(qrString);
            }

            return exporter.ExportPdf(hd, qrBytes, bankId, accountNumber, accountName);
        }
    }
}
