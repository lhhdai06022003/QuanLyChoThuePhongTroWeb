using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HopDongs
{
    public class WordExportService
    {
        public static byte[] GenerateHopDongWord(HopDongPrintRes data)
        {
            using (var ms = new MemoryStream())
            {
                using (var document = DocX.Create(ms))
                {
                    // Thiết lập khổ giấy A4 và margin
                    document.MarginTop = 50f;
                    document.MarginBottom = 50f;
                    document.MarginLeft = 50f;
                    document.MarginRight = 50f;
                    
                    var fontName = "Times New Roman";

                    // Quốc hiệu
                    var quocHieu = document.InsertParagraph("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc")
                        .Font(fontName)
                        .FontSize(14)
                        .Bold()
                        .Alignment = Alignment.center;

                    document.InsertParagraph($"........., ngày {DateTime.Now.Day} tháng {DateTime.Now.Month} năm {DateTime.Now.Year}")
                        .Font(fontName)
                        .FontSize(13)
                        .Italic()
                        .Alignment = Alignment.center;

                    document.InsertParagraph();

                    // Tiêu đề
                    document.InsertParagraph("HỢP ĐỒNG THUÊ PHÒNG TRỌ")
                        .Font(fontName)
                        .FontSize(18)
                        .Bold()
                        .Alignment = Alignment.center;

                    document.InsertParagraph($"(Số: {data.MaHopDong})")
                        .Font(fontName)
                        .FontSize(13)
                        .Italic()
                        .Alignment = Alignment.center;

                    document.InsertParagraph();

                    document.InsertParagraph($"Hôm nay, ngày {DateTime.Now.Day} tháng {DateTime.Now.Month} năm {DateTime.Now.Year}, tại địa chỉ: {data.DiaChiChiNhanh}. Chúng tôi gồm:")
                        .Font(fontName)
                        .FontSize(14)
                        .Alignment = Alignment.left;

                    document.InsertParagraph();

                    // Bên A
                    document.InsertParagraph("BÊN CHO THUÊ PHÒNG TRỌ (BÊN A):").Font(fontName).FontSize(14).Bold();
                    document.InsertParagraph($"- Đại diện / Quản lý: {data.TenChiNhanh}").Font(fontName).FontSize(14);
                    document.InsertParagraph($"- Địa chỉ khu trọ: {data.DiaChiChiNhanh}").Font(fontName).FontSize(14);
                    document.InsertParagraph($"- Điện thoại: {data.SoDienThoaiChiNhanh}").Font(fontName).FontSize(14);

                    document.InsertParagraph();

                    // Bên B
                    document.InsertParagraph("BÊN THUÊ PHÒNG TRỌ (BÊN B):").Font(fontName).FontSize(14).Bold();
                    document.InsertParagraph($"- Ông/Bà: {data.HoVaTenNguoiThue}").Font(fontName).FontSize(14);
                    document.InsertParagraph($"- CCCD số: {data.CCCDNguoiThue} cấp ngày {data.NgayCapCCCD?.ToString("dd/MM/yyyy") ?? "..."} tại {data.NoiCapCCCD ?? "..."}").Font(fontName).FontSize(14);
                    document.InsertParagraph($"- Hộ khẩu thường trú: {data.QueQuan ?? "..."}").Font(fontName).FontSize(14);
                    document.InsertParagraph($"- Điện thoại: {data.SoDienThoaiNguoiThue}").Font(fontName).FontSize(14);

                    document.InsertParagraph();
                    document.InsertParagraph("Sau khi bàn bạc, hai bên thống nhất ký kết Hợp đồng thuê phòng trọ với các điều khoản sau:").Font(fontName).FontSize(14).Bold();

                    // Điều 1
                    document.InsertParagraph();
                    document.InsertParagraph("Điều 1: Đối tượng hợp đồng").Font(fontName).FontSize(14).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                    document.InsertParagraph($"Bên A đồng ý cho Bên B thuê phòng trọ số {data.SoPhong} tại địa chỉ: {data.DiaChiChiNhanh}.")
                        .Font(fontName).FontSize(14);

                    // Điều 2
                    document.InsertParagraph();
                    document.InsertParagraph("Điều 2: Thời hạn thuê").Font(fontName).FontSize(14).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                    string endDate = data.ThoiDiemKetThuc.HasValue ? $"đến ngày {data.ThoiDiemKetThuc.Value:dd/MM/yyyy}" : "không xác định thời hạn";
                    document.InsertParagraph($"- Hợp đồng có giá trị kể từ ngày {data.ThoiDiemBatDau:dd/MM/yyyy} {endDate}.")
                        .Font(fontName).FontSize(14);

                    // Điều 3
                    document.InsertParagraph();
                    document.InsertParagraph("Điều 3: Giá thuê và Tiền cọc").Font(fontName).FontSize(14).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                    document.InsertParagraph($"- Tiền thuê phòng: {data.TienThuePhong:N0} VNĐ/tháng.").Font(fontName).FontSize(14);
                    document.InsertParagraph($"- Tiền đặt cọc: {data.TienCocPhong:N0} VNĐ.").Font(fontName).FontSize(14);
                    document.InsertParagraph("- Tiền cọc sẽ được Bên A hoàn trả cho Bên B khi chấm dứt hợp đồng, sau khi đã trừ đi các khoản chi phí điện, nước, dịch vụ còn nợ và chi phí đền bù hư hỏng tài sản (nếu có) theo đúng thỏa thuận.")
                        .Font(fontName).FontSize(14);

                    // Điều 4 (Dịch vụ)
                    document.InsertParagraph();
                    document.InsertParagraph("Điều 4: Chi phí khác (Dịch vụ)").Font(fontName).FontSize(14).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                    document.InsertParagraph("Ngoài tiền thuê phòng, Bên B phải thanh toán các chi phí dịch vụ sử dụng hàng tháng (tính theo đơn giá bên dưới):")
                        .Font(fontName).FontSize(14);

                    if (data.DichVus != null && data.DichVus.Count > 0)
                    {
                        var t = document.InsertTable(data.DichVus.Count + 1, 3);
                        t.Alignment = Alignment.center;
                        t.Design = TableDesign.TableGrid;
                        t.Rows[0].Cells[0].Paragraphs.First().Append("Tên dịch vụ").Bold().Font(fontName);
                        t.Rows[0].Cells[1].Paragraphs.First().Append("Đơn giá").Bold().Font(fontName);
                        t.Rows[0].Cells[2].Paragraphs.First().Append("Đơn vị").Bold().Font(fontName);

                        for (int i = 0; i < data.DichVus.Count; i++)
                        {
                            t.Rows[i + 1].Cells[0].Paragraphs.First().Append(data.DichVus[i].TenDichVu).Font(fontName);
                            t.Rows[i + 1].Cells[1].Paragraphs.First().Append($"{data.DichVus[i].DonGia:N0} VNĐ").Font(fontName);
                            t.Rows[i + 1].Cells[2].Paragraphs.First().Append(data.DichVus[i].DonViTinh).Font(fontName);
                        }
                    }
                    else
                    {
                        document.InsertParagraph("- (Không đăng ký dịch vụ nào thêm ngoài tiền phòng)").Font(fontName).FontSize(14).Italic();
                    }

                    // Điều khoản thư viện
                    int index = 5;
                    if (data.DieuKhoans != null && data.DieuKhoans.Count > 0)
                    {
                        foreach (var dk in data.DieuKhoans)
                        {
                            document.InsertParagraph();
                            document.InsertParagraph($"Điều {index}: {dk.TieuDe}").Font(fontName).FontSize(14).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                            
                            var lines = dk.NoiDung.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                            foreach(var line in lines)
                            {
                                document.InsertParagraph(line).Font(fontName).FontSize(14);
                            }
                            index++;
                        }

                        document.InsertParagraph();
                        document.InsertParagraph($"Điều {index}: Cam kết chung").Font(fontName).FontSize(14).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                        document.InsertParagraph("Hai bên cam kết thực hiện đúng các điều khoản đã thỏa thuận trong hợp đồng này. Trong quá trình thực hiện, nếu có phát sinh tranh chấp, hai bên sẽ ưu tiên giải quyết thông qua thương lượng. Nếu không tự giải quyết được, sẽ yêu cầu Tòa án giải quyết theo quy định của pháp luật.")
                            .Font(fontName).FontSize(14);
                        document.InsertParagraph("Hợp đồng này được lập thành 02 (hai) bản, có giá trị pháp lý như nhau, mỗi bên giữ 01 (một) bản để thực hiện.")
                            .Font(fontName).FontSize(14);
                    }
                    else
                    {
                        document.InsertParagraph();
                        document.InsertParagraph("Điều 5: Cam kết chung").Font(fontName).FontSize(14).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                        document.InsertParagraph("Hai bên cam kết thực hiện đúng các điều khoản đã thỏa thuận trong hợp đồng này. Hợp đồng này được lập thành 02 (hai) bản, có giá trị pháp lý như nhau, mỗi bên giữ 01 (một) bản để thực hiện.")
                            .Font(fontName).FontSize(14);
                    }

                    // Ký tên
                    document.InsertParagraph();
                    document.InsertParagraph();
                    var signTable = document.InsertTable(2, 2);
                    signTable.SetBorder(TableBorderType.InsideH, new Border(BorderStyle.Tcbs_none, 0, 0, Xceed.Drawing.Color.White));
                    signTable.SetBorder(TableBorderType.InsideV, new Border(BorderStyle.Tcbs_none, 0, 0, Xceed.Drawing.Color.White));
                    signTable.SetBorder(TableBorderType.Bottom, new Border(BorderStyle.Tcbs_none, 0, 0, Xceed.Drawing.Color.White));
                    signTable.SetBorder(TableBorderType.Top, new Border(BorderStyle.Tcbs_none, 0, 0, Xceed.Drawing.Color.White));
                    signTable.SetBorder(TableBorderType.Left, new Border(BorderStyle.Tcbs_none, 0, 0, Xceed.Drawing.Color.White));
                    signTable.SetBorder(TableBorderType.Right, new Border(BorderStyle.Tcbs_none, 0, 0, Xceed.Drawing.Color.White));
                    signTable.Alignment = Alignment.center;
                    
                    signTable.Rows[0].Cells[0].Paragraphs.First().Append("BÊN A (Bên cho thuê)").Font(fontName).FontSize(14).Bold().Alignment = Alignment.center;
                    signTable.Rows[0].Cells[1].Paragraphs.First().Append("BÊN B (Bên thuê)").Font(fontName).FontSize(14).Bold().Alignment = Alignment.center;
                    
                    signTable.Rows[1].Cells[0].Paragraphs.First().Append("(Ký và ghi rõ họ tên)").Font(fontName).FontSize(14).Italic().Alignment = Alignment.center;
                    signTable.Rows[1].Cells[1].Paragraphs.First().Append("(Ký và ghi rõ họ tên)").Font(fontName).FontSize(14).Italic().Alignment = Alignment.center;

                    document.Save();
                }
                return ms.ToArray();
            }
        }
    }
}
