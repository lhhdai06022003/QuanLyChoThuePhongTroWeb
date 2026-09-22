using System;
using System.IO;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters
{
    public class InvoiceDocumentExporter : IInvoiceDocumentExporter
    {
        public byte[] ExportExcel(HoaDonChiTietRes hd)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Hóa đơn");

            // Header info
            ws.Cell(1, 1).Value = "HÓA ĐƠN THANH TOÁN";
            ws.Range(1, 1, 1, 6).Merge().Style.Font.Bold = true;
            ws.Range(1, 1, 1, 6).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 1).Value = "Mã hóa đơn:"; ws.Cell(3, 2).Value = hd.MaHoaDon;
            ws.Cell(4, 1).Value = "Chi nhánh:"; ws.Cell(4, 2).Value = hd.TenChiNhanh;
            ws.Cell(5, 1).Value = "Phòng:"; ws.Cell(5, 2).Value = hd.TenPhong;
            ws.Cell(6, 1).Value = "Người thuê:"; ws.Cell(6, 2).Value = hd.TenNguoiThue;
            ws.Cell(7, 1).Value = "Kỳ thanh toán:"; ws.Cell(7, 2).Value = $"Tháng {hd.Thang}/{hd.Nam}";
            ws.Cell(8, 1).Value = "Trạng thái:"; ws.Cell(8, 2).Value = hd.TrangThaiHoaDon;
            ws.Cell(9, 1).Value = "Ngày tạo:"; ws.Cell(9, 2).Value = hd.NgayTao;

            for (int r = 3; r <= 9; r++)
            {
                ws.Cell(r, 1).Style.Font.Bold = true;
            }

            // Table header
            int row = 11;
            ws.Cell(row, 1).Value = "STT";
            ws.Cell(row, 2).Value = "Tên dịch vụ";
            ws.Cell(row, 3).Value = "Đơn giá";
            ws.Cell(row, 4).Value = "Số lượng";
            ws.Cell(row, 5).Value = "Đơn vị";
            ws.Cell(row, 6).Value = "Thành tiền";

            var headerRange = ws.Range(row, 1, row, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Table data
            int stt = 1;
            foreach (var ct in hd.ChiTietHoaDons)
            {
                row++;
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = ct.TenDichVu;
                ws.Cell(row, 3).Value = ct.DonGia;
                ws.Cell(row, 4).Value = ct.SoLuong;
                ws.Cell(row, 5).Value = string.IsNullOrEmpty(ct.DonVi) ? "-" : ct.DonVi;
                ws.Cell(row, 6).Value = ct.TongTien;
                
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            }

            // Total row
            row++;
            ws.Cell(row, 1).Value = "";
            ws.Range(row, 1, row, 5).Merge();
            ws.Cell(row, 5).Value = "TỔNG CỘNG:";
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 6).Value = hd.TongTien;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;

            // Border
            var tableRange = ws.Range(11, 1, row, 6);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Column widths
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 35;
            ws.Column(3).Width = 15;
            ws.Column(4).Width = 12;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 18;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] ExportPdf(HoaDonChiTietRes hd, byte[]? qrBytes, string bankId, string accountNumber, string accountName)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("HÓA ĐƠN THANH TOÁN").Bold().FontSize(20).FontColor(Colors.Blue.Darken2);
                        col.Item().AlignCenter().Text($"{hd.TenChiNhanh}").FontSize(12).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(hd.DiaChiChiNhanh))
                            col.Item().AlignCenter().Text($"Địa chỉ: {hd.DiaChiChiNhanh}").FontSize(10).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Info rows
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Mã hóa đơn: ").Bold();
                                t.Span(hd.MaHoaDon);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Ngày tạo: ").Bold();
                                t.Span(hd.NgayTao);
                            });
                        });

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Phòng: ").Bold();
                                t.Span(hd.TenPhong);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Kỳ: ").Bold();
                                t.Span($"Tháng {hd.Thang}/{hd.Nam}");
                            });
                        });

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Người thuê: ").Bold();
                                t.Span(hd.TenNguoiThue);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Trạng thái: ").Bold();
                                t.Span(hd.TrangThaiHoaDon).FontColor(
                                    hd.TrangThaiHoaDon == "Đã thanh toán" ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            });
                        });

                        col.Item().PaddingVertical(10);

                        // Table
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(35);  // STT
                                columns.RelativeColumn(5);   // Tên DV
                                columns.RelativeColumn(2);   // Đơn giá
                                columns.ConstantColumn(50);  // SL
                                columns.RelativeColumn(1.5f);// Đơn vị
                                columns.RelativeColumn(2);   // Thành tiền
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                                    .Text("STT").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .Text("Tên dịch vụ").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                                    .Text("Đơn giá").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                                    .Text("SL").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                                    .Text("Đơn vị").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                                    .Text("Thành tiền").FontColor(Colors.White).Bold();
                            });

                            int stt = 1;
                            foreach (var ct in hd.ChiTietHoaDons)
                            {
                                var bgColor = stt % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(stt.ToString());
                                table.Cell().Background(bgColor).Padding(5).Text(ct.TenDichVu);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(FormatVND(ct.DonGia));
                                table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(ct.SoLuong.ToString());
                                table.Cell().Background(bgColor).Padding(5).AlignCenter().Text(string.IsNullOrEmpty(ct.DonVi) ? "-" : ct.DonVi);
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(FormatVND(ct.TongTien));
                                stt++;
                            }
                        });

                        col.Item().PaddingTop(5).AlignRight().Text(t =>
                        {
                            t.Span("TỔNG CỘNG: ").Bold().FontSize(14);
                            t.Span(FormatVND(hd.TongTien)).Bold().FontSize(14).FontColor(Colors.Red.Darken2);
                        });

                        if (qrBytes != null)
                        {
                            col.Item().PaddingTop(15).Row(row =>
                            {
                                row.RelativeItem();
                                row.ConstantItem(150).Column(c =>
                                {
                                    c.Item().AlignCenter().Text("Quét mã QR để thanh toán").FontSize(10).Italic();
                                    c.Item().PaddingTop(5).Image(qrBytes);
                                    c.Item().AlignCenter().Text(accountName).Bold().FontSize(9);
                                    c.Item().AlignCenter().Text($"{bankId} - {accountNumber}").FontSize(8);
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Ngày xuất: ").FontSize(9);
                        t.Span(DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static string FormatVND(decimal amount)
        {
            return string.Format("{0:#,##0} ₫", amount);
        }
    }
}
