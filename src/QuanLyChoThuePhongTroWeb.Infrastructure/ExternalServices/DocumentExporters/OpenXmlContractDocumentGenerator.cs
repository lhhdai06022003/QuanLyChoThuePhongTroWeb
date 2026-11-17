using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters
{
    // Sinh hợp đồng .docx bằng DocumentFormat.OpenXml (MIT). Thay DocX vì giấy phép Xceed Community chỉ cho dùng phi thương mại.
    // Nội dung giữ như trang in Views/HopDong/Print.cshtml. Ngày trong DTO lưu UTC, đổi sang giờ Việt Nam khi in.
    public class OpenXmlContractDocumentGenerator : IContractDocumentGenerator
    {
        private const string FontName = "Times New Roman";
        private const int BodySize = 28; // nửa point: 14pt
        private const int PageWidth = 11906; // A4, đơn vị twip
        private const int PageHeight = 16838;
        private const int PageMargin = 1000; // 50pt
        private const int TextWidth = PageWidth - 2 * PageMargin;

        private static readonly CultureInfo ViVn = CultureInfo.GetCultureInfo("vi-VN");

        public byte[] GenerateHopDongWord(HopDongPrintRes data)
        {
            using var ms = new MemoryStream();
            using (var document = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                AddDefaultStyles(main);
                main.Document = new Document(BuildBody(data));
                main.Document.Save();
            }

            return ms.ToArray();
        }

        private static Body BuildBody(HopDongPrintRes data)
        {
            var body = new Body();
            var ngayIn = $"ngày {data.NgayIn.Day} tháng {data.NgayIn.Month} năm {data.NgayIn.Year}";

            body.Append(Para(new[] { "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", "Độc lập - Tự do - Hạnh phúc" }, size: 28, bold: true, center: true));
            body.Append(Para($"........., {ngayIn}", size: 26, italic: true, center: true));
            body.Append(Blank());

            body.Append(Para("HỢP ĐỒNG THUÊ PHÒNG TRỌ", size: 36, bold: true, center: true));
            body.Append(Para($"(Số: {data.MaHopDong})", size: 26, italic: true, center: true));
            body.Append(Blank());

            body.Append(Para($"Hôm nay, {ngayIn}, tại địa chỉ: {data.DiaChiChiNhanh}. Chúng tôi gồm:"));
            body.Append(Blank());

            body.Append(Para("BÊN CHO THUÊ PHÒNG TRỌ (BÊN A):", bold: true));
            body.Append(Para($"- Đại diện / Quản lý: {data.TenChiNhanh}"));
            body.Append(Para($"- Địa chỉ khu trọ: {data.DiaChiChiNhanh}"));
            body.Append(Para($"- Điện thoại: {data.SoDienThoaiChiNhanh}"));
            body.Append(Blank());

            body.Append(Para("BÊN THUÊ PHÒNG TRỌ (BÊN B):", bold: true));
            body.Append(Para($"- Ông/Bà: {data.HoVaTenNguoiThue}"));
            body.Append(Para($"- CCCD số: {data.CCCDNguoiThue} cấp ngày {VnDate(data.NgayCapCCCD) ?? "..."} tại {data.NoiCapCCCD ?? "..."}"));
            body.Append(Para($"- Hộ khẩu thường trú: {data.QueQuan ?? "..."}"));
            body.Append(Para($"- Điện thoại: {data.SoDienThoaiNguoiThue}"));
            body.Append(Blank());
            body.Append(Para("Sau khi bàn bạc, hai bên thống nhất ký kết Hợp đồng thuê phòng trọ với các điều khoản sau:", bold: true));

            AppendClause(body, "Điều 1: Đối tượng hợp đồng",
                $"Bên A đồng ý cho Bên B thuê phòng trọ số {data.SoPhong} tại địa chỉ: {data.DiaChiChiNhanh}.");

            var ketThuc = data.ThoiDiemKetThuc.HasValue ? $"đến ngày {VnDate(data.ThoiDiemKetThuc)}" : "không xác định thời hạn";
            AppendClause(body, "Điều 2: Thời hạn thuê",
                $"- Hợp đồng có giá trị kể từ ngày {VnDate(data.ThoiDiemBatDau)} {ketThuc}.");

            AppendClause(body, "Điều 3: Giá thuê và Tiền cọc",
                $"- Tiền thuê phòng: {Money(data.TienThuePhong)} VNĐ/tháng.",
                $"- Tiền đặt cọc: {Money(data.TienCocPhong)} VNĐ.",
                "- Tiền cọc sẽ được Bên A hoàn trả cho Bên B khi chấm dứt hợp đồng, sau khi đã trừ đi các khoản chi phí điện, nước, dịch vụ còn nợ và chi phí đền bù hư hỏng tài sản (nếu có) theo đúng thỏa thuận.");

            AppendClause(body, "Điều 4: Chi phí khác (Dịch vụ)",
                "Ngoài tiền thuê phòng, Bên B phải thanh toán các chi phí dịch vụ sử dụng hàng tháng (tính theo đơn giá bên dưới):");
            if (data.DichVus is { Count: > 0 })
            {
                body.Append(ServiceTable(data.DichVus));
            }
            else
            {
                body.Append(Para("- (Không đăng ký dịch vụ nào thêm ngoài tiền phòng)", italic: true));
            }

            var index = 5;
            if (data.DieuKhoans is { Count: > 0 })
            {
                foreach (var dk in data.DieuKhoans)
                {
                    AppendClause(body, $"Điều {index}: {dk.TieuDe}",
                        (dk.NoiDung ?? string.Empty).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
                    index++;
                }

                AppendClause(body, $"Điều {index}: Cam kết chung",
                    "Hai bên cam kết thực hiện đúng các điều khoản đã thỏa thuận trong hợp đồng này. Trong quá trình thực hiện, nếu có phát sinh tranh chấp, hai bên sẽ ưu tiên giải quyết thông qua thương lượng. Nếu không tự giải quyết được, sẽ yêu cầu Tòa án giải quyết theo quy định của pháp luật.",
                    "Hợp đồng này được lập thành 02 (hai) bản, có giá trị pháp lý như nhau, mỗi bên giữ 01 (một) bản để thực hiện.");
            }
            else
            {
                AppendClause(body, "Điều 5: Cam kết chung",
                    "Hai bên cam kết thực hiện đúng các điều khoản đã thỏa thuận trong hợp đồng này. Hợp đồng này được lập thành 02 (hai) bản, có giá trị pháp lý như nhau, mỗi bên giữ 01 (một) bản để thực hiện.");
            }

            body.Append(Blank());
            body.Append(Blank());
            body.Append(SignatureTable());

            body.Append(new SectionProperties(
                new PageSize { Width = (uint)PageWidth, Height = (uint)PageHeight },
                new PageMargin
                {
                    Top = PageMargin,
                    Bottom = PageMargin,
                    Left = (uint)PageMargin,
                    Right = (uint)PageMargin,
                    Header = 720U,
                    Footer = 720U,
                    Gutter = 0U
                }));

            return body;
        }

        private static void AppendClause(Body body, string title, params string[] lines)
        {
            body.Append(Blank());
            body.Append(Para(title, bold: true, underline: true));
            foreach (var line in lines)
            {
                body.Append(Para(line));
            }
        }

        private static Table ServiceTable(IReadOnlyList<HopDongDichVuPrintRes> dichVus)
        {
            int[] widths = { TextWidth * 50 / 100, TextWidth * 30 / 100, TextWidth - TextWidth * 50 / 100 - TextWidth * 30 / 100 };
            var table = NewTable(widths, bordered: true);
            table.Append(Row(widths, true, false, false, "Tên dịch vụ", "Đơn giá", "Đơn vị"));
            foreach (var dv in dichVus)
            {
                table.Append(Row(widths, false, false, false, dv.TenDichVu, $"{Money(dv.DonGia)} VNĐ", dv.DonViTinh));
            }

            return table;
        }

        private static Table SignatureTable()
        {
            int[] widths = { TextWidth / 2, TextWidth - TextWidth / 2 };
            var table = NewTable(widths, bordered: false);
            table.Append(Row(widths, true, true, false, "BÊN A (Bên cho thuê)", "BÊN B (Bên thuê)"));
            table.Append(Row(widths, false, true, true, "(Ký và ghi rõ họ tên)", "(Ký và ghi rõ họ tên)"));
            return table;
        }

        private static Table NewTable(int[] widths, bool bordered)
        {
            var border = bordered ? BorderValues.Single : BorderValues.None;
            var size = bordered ? 4U : 0U;
            var properties = new TableProperties(
                new TableWidth { Width = TextWidth.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                new TableJustification { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                    new TopBorder { Val = border, Size = size },
                    new LeftBorder { Val = border, Size = size },
                    new BottomBorder { Val = border, Size = size },
                    new RightBorder { Val = border, Size = size },
                    new InsideHorizontalBorder { Val = border, Size = size },
                    new InsideVerticalBorder { Val = border, Size = size }));

            var grid = new TableGrid();
            foreach (var width in widths)
            {
                grid.Append(new GridColumn { Width = width.ToString(CultureInfo.InvariantCulture) });
            }

            return new Table(properties, grid);
        }

        private static TableRow Row(int[] widths, bool bold, bool center, bool italic, params string[] cells)
        {
            var row = new TableRow();
            for (var i = 0; i < cells.Length; i++)
            {
                row.Append(new TableCell(
                    new TableCellProperties(new TableCellWidth { Width = widths[i].ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa }),
                    Para(cells[i], bold: bold, italic: italic, center: center)));
            }

            return row;
        }

        private static Paragraph Blank() => new();

        private static Paragraph Para(string text, int size = BodySize, bool bold = false, bool italic = false, bool underline = false, bool center = false) =>
            Para(new[] { text }, size, bold, italic, underline, center);

        // Mỗi phần tử của lines là một dòng, ngắt bằng xuống dòng mềm trong cùng đoạn.
        private static Paragraph Para(string[] lines, int size = BodySize, bool bold = false, bool italic = false, bool underline = false, bool center = false)
        {
            var paragraph = new Paragraph();
            if (center)
            {
                paragraph.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            }

            var run = new Run(RunProps(size, bold, italic, underline));
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0) run.Append(new Break());
                run.Append(new Text(lines[i] ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
            }

            paragraph.Append(run);
            return paragraph;
        }

        // Thứ tự phần tử con theo schema CT_RPr: rFonts, b, i, sz, szCs, u.
        private static RunProperties RunProps(int size, bool bold, bool italic, bool underline)
        {
            var props = new RunProperties(new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName, EastAsia = FontName });
            if (bold) props.Append(new Bold());
            if (italic) props.Append(new Italic());
            var sizeText = size.ToString(CultureInfo.InvariantCulture);
            props.Append(new FontSize { Val = sizeText });
            props.Append(new FontSizeComplexScript { Val = sizeText });
            if (underline) props.Append(new Underline { Val = UnderlineValues.Single });
            return props;
        }

        // Kiểu mặc định của tài liệu: Times New Roman 14pt, tiếng Việt, không giãn đoạn, để dòng trống có cùng cỡ chữ.
        private static void AddDefaultStyles(MainDocumentPart main)
        {
            var sizeText = BodySize.ToString(CultureInfo.InvariantCulture);
            var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                new DocDefaults(
                    new RunPropertiesDefault(new RunPropertiesBaseStyle(
                        new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName, EastAsia = FontName },
                        new FontSize { Val = sizeText },
                        new FontSizeComplexScript { Val = sizeText },
                        new Languages { Val = "vi-VN" })),
                    new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                        new SpacingBetweenLines { After = "0", Line = "276", LineRule = LineSpacingRuleValues.Auto }))));
            stylesPart.Styles.Save();
        }

        private static string? VnDate(DateTime? utc) =>
            utc?.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static string Money(decimal amount) => amount.ToString("N0", ViVn);
    }
}
