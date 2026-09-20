using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IInvoiceDocumentExporter
    {
        byte[] ExportExcel(HoaDonChiTietRes hoaDon);
        byte[] ExportPdf(HoaDonChiTietRes hoaDon, byte[]? qrBytes, string bankId, string accountNumber, string accountName);
    }
}
