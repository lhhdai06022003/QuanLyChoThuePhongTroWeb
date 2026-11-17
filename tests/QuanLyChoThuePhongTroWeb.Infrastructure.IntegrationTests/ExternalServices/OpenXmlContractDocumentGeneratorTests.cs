using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không cần DB: dựng file Word hợp đồng bằng OpenXml (thay DocX, giấy phép Xceed chỉ cho dùng phi thương mại).
    public class OpenXmlContractDocumentGeneratorTests
    {
        private static HopDongPrintRes Sample(bool coDichVu = true, bool coDieuKhoan = true) => new()
        {
            HopDongId = 3,
            MaHopDong = "HD-CN-2511-003",
            // Lưu UTC: 00:00 ngày 01/11/2026 và 23:59:59 ngày 31/10/2027 giờ Việt Nam.
            ThoiDiemBatDau = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc),
            ThoiDiemKetThuc = new DateTime(2027, 10, 31, 16, 59, 59, DateTimeKind.Utc),
            TienThuePhong = 3_500_000m,
            TienCocPhong = 7_000_000m,
            NgayIn = new DateTime(2026, 11, 5),
            TenChiNhanh = "Chi nhánh Một",
            DiaChiChiNhanh = "1 Đường A, Thủ Đức",
            SoDienThoaiChiNhanh = "0900000000",
            SoPhong = "P101",
            HoVaTenNguoiThue = "Nguyễn Văn A",
            SoDienThoaiNguoiThue = "0911111111",
            CCCDNguoiThue = "079000000001",
            // Ngày cấp CCCD cũng lưu UTC: 00:00 ngày 10/05/2021 giờ Việt Nam.
            NgayCapCCCD = new DateTime(2021, 5, 9, 17, 0, 0, DateTimeKind.Utc),
            NoiCapCCCD = "Cục Cảnh sát QLHC về TTXH",
            QueQuan = "Bình Định",
            DichVus = coDichVu
                ? new List<HopDongDichVuPrintRes>
                {
                    new() { TenDichVu = "Điện", DonGia = 3_500m, DonViTinh = "kWh" },
                    new() { TenDichVu = "Nước", DonGia = 100_000m, DonViTinh = "người" }
                }
                : new List<HopDongDichVuPrintRes>(),
            DieuKhoans = coDieuKhoan
                ? new List<HopDongDieuKhoanPrintRes>
                {
                    new() { TieuDe = "Giờ giấc", NoiDung = "Không ồn ào sau 22h.\nKhóa cổng lúc 23h.", ThuTu = 1 }
                }
                : new List<HopDongDieuKhoanPrintRes>()
        };

        private static (WordprocessingDocument Doc, string Text) Open(byte[] bytes)
        {
            var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
            var text = string.Join("\n", doc.MainDocumentPart!.Document.Body!.Descendants<Paragraph>().Select(p => p.InnerText));
            return (doc, text);
        }

        [Fact]
        public void Generate_ProducesValidDocx_WithContractContent()
        {
            var bytes = new OpenXmlContractDocumentGenerator().GenerateHopDongWord(Sample());

            var (doc, text) = Open(bytes);
            using (doc)
            {
                Assert.Empty(new OpenXmlValidator().Validate(doc));
                Assert.Contains("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", text);
                Assert.Contains("HỢP ĐỒNG THUÊ PHÒNG TRỌ", text);
                Assert.Contains("(Số: HD-CN-2511-003)", text);
                Assert.Contains("........., ngày 5 tháng 11 năm 2026", text);
                Assert.Contains("- Ông/Bà: Nguyễn Văn A", text);
                Assert.Contains("Bên A đồng ý cho Bên B thuê phòng trọ số P101 tại địa chỉ: 1 Đường A, Thủ Đức.", text);
                Assert.Contains("Điều 5: Giờ giấc", text);
                Assert.Contains("Khóa cổng lúc 23h.", text);
                Assert.Contains("Điều 6: Cam kết chung", text);
                Assert.Contains("BÊN A (Bên cho thuê)", text);
                Assert.DoesNotContain("Xceed", text);
            }
        }

        [Fact]
        public void Generate_ShowsVietnamDates_NotUtcDates()
        {
            var (doc, text) = Open(new OpenXmlContractDocumentGenerator().GenerateHopDongWord(Sample()));
            using (doc)
            {
                Assert.Contains("kể từ ngày 01/11/2026 đến ngày 31/10/2027.", text);
                Assert.Contains("cấp ngày 10/05/2021", text);
                Assert.DoesNotContain("31/10/2026", text);
            }
        }

        [Fact]
        public void Generate_FormatsMoneyVietnameseStyle_AndListsServices()
        {
            var (doc, text) = Open(new OpenXmlContractDocumentGenerator().GenerateHopDongWord(Sample()));
            using (doc)
            {
                Assert.Contains("- Tiền thuê phòng: 3.500.000 VNĐ/tháng.", text);
                Assert.Contains("- Tiền đặt cọc: 7.000.000 VNĐ.", text);

                var serviceTable = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
                var rows = serviceTable.Elements<TableRow>().Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToArray()).ToList();
                Assert.Equal(new[] { "Tên dịch vụ", "Đơn giá", "Đơn vị" }, rows[0]);
                Assert.Equal(new[] { "Nước", "100.000 VNĐ", "người" }, rows[2]);
            }
        }

        [Fact]
        public void Generate_WithoutServicesOrClauses_UsesDefaultSections()
        {
            var (doc, text) = Open(new OpenXmlContractDocumentGenerator().GenerateHopDongWord(Sample(coDichVu: false, coDieuKhoan: false)));
            using (doc)
            {
                Assert.Empty(new OpenXmlValidator().Validate(doc));
                Assert.Contains("(Không đăng ký dịch vụ nào thêm ngoài tiền phòng)", text);
                Assert.Contains("Điều 5: Cam kết chung", text);
                // Chỉ còn bảng chữ ký.
                Assert.Single(doc.MainDocumentPart!.Document.Body!.Elements<Table>());
            }
        }

        [Fact]
        public void Generate_OpenEndedContract_SaysNoFixedTerm()
        {
            var data = Sample();
            data.ThoiDiemKetThuc = null;
            var (doc, text) = Open(new OpenXmlContractDocumentGenerator().GenerateHopDongWord(data));
            using (doc)
            {
                Assert.Contains("kể từ ngày 01/11/2026 không xác định thời hạn.", text);
            }
        }
    }
}
