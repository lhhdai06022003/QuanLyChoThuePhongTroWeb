using System;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters
{
    public class BienBanThanhLyExporter : IBienBanThanhLyExporter
    {
        public byte[] ExportPdf(BienBanThanhLyDto data)
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
                        col.Item().AlignCenter().Text("BIÊN BẢN THANH LÝ HỢP ĐỒNG THUÊ PHÒNG").Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
                        col.Item().AlignCenter().Text($"Mã hợp đồng: {data.MaHopDong}").FontSize(12);
                        col.Item().AlignCenter().Text($"Ngày thanh lý: {Ngay(data.NgayThanhLyUtc)}").FontSize(11).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(4);

                        col.Item().Text("BÊN CHO THUÊ").Bold();
                        col.Item().Text($"Chi nhánh: {data.TenChiNhanh}");
                        if (!string.IsNullOrWhiteSpace(data.DiaChiChiNhanh))
                            col.Item().Text($"Địa chỉ: {data.DiaChiChiNhanh}");
                        if (!string.IsNullOrWhiteSpace(data.SoDienThoaiChiNhanh))
                            col.Item().Text($"Điện thoại: {data.SoDienThoaiChiNhanh}");

                        col.Item().PaddingTop(6).Text("BÊN THUÊ").Bold();
                        col.Item().Text($"Họ tên: {data.TenNguoiDaiDien}");
                        col.Item().Text($"CCCD: {data.CccdNguoiDaiDien}    Điện thoại: {data.SdtNguoiDaiDien}");
                        if (data.ThanhVien.Count > 0)
                            col.Item().Text($"Thành viên ở cùng: {string.Join(", ", data.ThanhVien)}");

                        col.Item().PaddingTop(6).Text("PHÒNG VÀ THỜI HẠN").Bold();
                        col.Item().Text($"Phòng: {data.SoPhong}");
                        col.Item().Text($"Thời hạn: {Ngay(data.ThoiDiemBatDauUtc)} - {Ngay(data.ThoiDiemKetThucUtc)}");
                        col.Item().Text($"Ngày trả phòng: {Ngay(data.ThoiDiemKetThucUtc)}");

                        col.Item().PaddingTop(8).Text("CÁC HÓA ĐƠN LIÊN QUAN").Bold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.2f); // Mã HĐ
                                c.RelativeColumn(2.2f); // Mã hợp đồng
                                c.RelativeColumn(1.2f); // Kỳ
                                c.RelativeColumn(2);    // Tổng
                                c.RelativeColumn(2);    // Đã thu
                                c.RelativeColumn(2);    // Trừ cọc
                                c.RelativeColumn(2);    // Còn lại
                            });

                            table.Header(h =>
                            {
                                foreach (var title in new[] { "Mã hóa đơn", "Hợp đồng", "Kỳ", "Tổng", "Đã thu", "Trừ tiền cọc", "Còn lại" })
                                {
                                    h.Cell().Background(Colors.Blue.Darken2).Padding(4).AlignCenter()
                                        .Text(title).FontColor(Colors.White).Bold().FontSize(9);
                                }
                            });

                            foreach (var hd in data.HoaDons)
                            {
                                table.Cell().BorderBottom(0.5f).Padding(3).Text(hd.MaHoaDon).FontSize(9);
                                table.Cell().BorderBottom(0.5f).Padding(3).Text(hd.MaHopDong).FontSize(9);
                                table.Cell().BorderBottom(0.5f).Padding(3).AlignCenter().Text($"T{hd.Thang}/{hd.Nam}").FontSize(9);
                                table.Cell().BorderBottom(0.5f).Padding(3).AlignRight().Text(Tien(hd.TongTien)).FontSize(9);
                                table.Cell().BorderBottom(0.5f).Padding(3).AlignRight().Text(Tien(hd.DaThuKhac)).FontSize(9);
                                table.Cell().BorderBottom(0.5f).Padding(3).AlignRight().Text(hd.CanTru > 0 ? "-" + Tien(hd.CanTru) : "0").FontSize(9);
                                table.Cell().BorderBottom(0.5f).Padding(3).AlignRight().Text(Tien(hd.ConLai)).FontSize(9);
                            }
                        });

                        col.Item().PaddingTop(8).Text("TỔNG KẾT").Bold();
                        col.Item().Text($"Tiền cọc: {Tien(data.TienCoc)} đ");
                        col.Item().Text($"Trừ tiền cọc: -{Tien(data.TongCanTru)} đ");
                        col.Item().Text($"Cọc hoàn trực tiếp cho khách: {Tien(data.CocHoanTrucTiep)} đ").Bold();
                        col.Item().Text($"Khách còn nợ: {Tien(data.NoConLai)} đ (tính đến {Ngay(data.NgayInUtc)})")
                            .Bold().FontColor(data.NoConLai > 0 ? Colors.Red.Darken2 : Colors.Black);

                        if (data.NoConLai > 0)
                        {
                            col.Item().Text("Hạn trả: ......................");
                        }

                        if (!string.IsNullOrWhiteSpace(data.GhiChu))
                        {
                            col.Item().PaddingTop(6).Text("GHI CHÚ").Bold();
                            col.Item().Text(data.GhiChu);
                        }

                        col.Item().PaddingTop(30).Row(r =>
                        {
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("BÊN CHO THUÊ").Bold();
                                c.Item().AlignCenter().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(9);
                            });
                            r.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("BÊN THUÊ").Bold();
                                c.Item().AlignCenter().Text("(Ký, ghi rõ họ tên)").Italic().FontSize(9);
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static string Ngay(DateTime utc) => utc.AddHours(7).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static string Tien(decimal soTien) => soTien.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
