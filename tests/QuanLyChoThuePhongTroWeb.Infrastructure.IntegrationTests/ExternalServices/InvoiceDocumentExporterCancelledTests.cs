using System;
using System.Collections.Generic;
using System.Text;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    public class InvoiceDocumentExporterCancelledTests
    {
        private static bool StartsWithPdfHeader(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) return false;
            var header = Encoding.ASCII.GetString(bytes, 0, 4);
            return header == "%PDF";
        }

        private static HoaDonChiTietRes CreateBaseInvoice()
        {
            return new HoaDonChiTietRes
            {
                HoaDonId = 1,
                MaHoaDon = "HD-TEST-001",
                TenChiNhanh = "Chi nhánh 1",
                DiaChiChiNhanh = "123 Đường Số 1",
                TenPhong = "Phòng 101",
                TenNguoiThue = "Nguyễn Văn A",
                Thang = 10,
                Nam = 2026,
                TongTien = 3500000m,
                TrangThaiHoaDon = "Chưa thanh toán",
                NgayTao = "01/10/2026 08:00",
                ChiTietHoaDons = new List<ChiTietHoaDonRes>
                {
                    new ChiTietHoaDonRes
                    {
                        TenDichVu = "Tiền thuê phòng",
                        DonGia = 3000000m,
                        SoLuong = 1,
                        TongTien = 3000000m,
                        DonVi = "Tháng"
                    },
                    new ChiTietHoaDonRes
                    {
                        TenDichVu = "Tiền điện",
                        DonGia = 3500m,
                        SoLuong = 100,
                        TongTien = 350000m,
                        DonVi = "kWh"
                    }
                }
            };
        }

        [Fact]
        public void ExportPdf_Cancelled_DiffersFromNormal()
        {
            var exporter = new InvoiceDocumentExporter();

            var normal = CreateBaseInvoice();
            normal.DaHuy = false;
            normal.LyDoHuy = string.Empty;

            var cancelled = CreateBaseInvoice();
            cancelled.DaHuy = true;
            cancelled.LyDoHuy = "Sai chỉ số";

            var normalPdf = exporter.ExportPdf(normal, null, "MB", "123456", "CHU TRO");
            var cancelledPdf = exporter.ExportPdf(cancelled, null, "MB", "123456", "CHU TRO");

            Assert.True(StartsWithPdfHeader(normalPdf), "Normal PDF must start with %PDF");
            Assert.True(StartsWithPdfHeader(cancelledPdf), "Cancelled PDF must start with %PDF");
            Assert.NotEqual(normalPdf, cancelledPdf);
        }

        [Fact]
        public void ExportPdf_Cancelled_500CharReason_Succeeds()
        {
            var exporter = new InvoiceDocumentExporter();

            var cancelled = CreateBaseInvoice();
            cancelled.DaHuy = true;
            // 500 characters of Vietnamese text
            var sb = new StringBuilder();
            while (sb.Length < 500)
            {
                sb.Append("Lý do hủy hóa đơn có dấu tiếng Việt rất dài để kiểm tra tràn trang và hiển thị QuestPDF. ");
            }
            cancelled.LyDoHuy = sb.ToString().Substring(0, 500);

            var cancelledPdf = exporter.ExportPdf(cancelled, null, "MB", "123456", "CHU TRO");

            Assert.NotNull(cancelledPdf);
            Assert.True(StartsWithPdfHeader(cancelledPdf), "Cancelled PDF with 500-char reason must start with %PDF");
        }
    }
}
