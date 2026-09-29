using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

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
            if (hd.TrangThaiHoaDon == "Chưa thanh toán")
            {
                bankId = settings.BankId ?? "MB";
                accountNumber = settings.AccountNumber ?? "";
                accountName = settings.AccountName ?? "";

                string memo = $"THANH TOAN {hd.MaHoaDon}";
                string qrString = vietQr.GenerateVietQRString(bankId, accountNumber, hd.TongTien, memo);
                qrBytes = vietQr.GenerateQRCodePNGBytes(qrString);
            }

            return exporter.ExportPdf(hd, qrBytes, bankId, accountNumber, accountName);
        }
    }
}
