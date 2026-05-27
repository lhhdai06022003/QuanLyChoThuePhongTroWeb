using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.HoaDons
{
    public class HoaDonService : IHoaDonService
    {
        private readonly ApplicationDbContext _context;

        public HoaDonService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =================== PHÁT SINH HÓA ĐƠN HÀNG LOẠT ===================
        public async Task<(bool IsSuccess, string Message, int SoHoaDonMoi)> PhatSinhHoaDonAsync(int chiNhanhId, int thang, int nam)
        {
            // 1. Lấy tất cả hợp đồng đang hoạt động tại chi nhánh
            var hopDongs = await _context.HopDongs
                .Include(h => h.PhongTro)
                .Include(h => h.NguoiThue)
                .Where(h => h.PhongTro.ChiNhanhId == chiNhanhId &&
                            h.TrangThaiHopDong == TrangThaiHopDong.DangHoatDong &&
                            !h.IsDeleted)
                .ToListAsync();

            if (!hopDongs.Any())
                return (false, "Không có hợp đồng nào đang hoạt động tại chi nhánh này.", 0);

            int soHoaDonMoi = 0;

            foreach (var hd in hopDongs)
            {
                // 2. Kiểm tra đã tồn tại hóa đơn cho tháng/năm này chưa
                bool exists = await _context.HoaDons.AnyAsync(x =>
                    x.HopDongId == hd.HopDongId && x.Thang == thang && x.Nam == nam && !x.IsDeleted);
                if (exists) continue;

                // 3. Tìm dữ liệu điện nước đã chốt
                var dienNuoc = await _context.DichVuDienNuocCuaPhongs
                    .FirstOrDefaultAsync(x => x.PhongTroId == hd.PhongTroId && x.Thang == thang && x.Nam == nam && !x.IsDeleted);

                // 4. Tạo các dòng chi tiết hóa đơn
                var chiTietList = new List<ChiTietHoaDon>();

                // Dòng 1: Tiền thuê phòng
                chiTietList.Add(new ChiTietHoaDon
                {
                    TenDichVu = "Tiền thuê phòng",
                    DonGia = hd.TienThuePhong,
                    SoLuong = 1,
                    TongTien = hd.TienThuePhong
                });

                // Dòng 2: Tiền điện
                if (dienNuoc != null)
                {
                    double soDien = dienNuoc.ChiSoDienMoi - dienNuoc.ChiSoDienCu;
                    double tienDien = soDien * dienNuoc.DonGiaDien;
                    chiTietList.Add(new ChiTietHoaDon
                    {
                        TenDichVu = $"Tiền điện ({dienNuoc.ChiSoDienCu} → {dienNuoc.ChiSoDienMoi})",
                        DonGia = dienNuoc.DonGiaDien,
                        SoLuong = (int)soDien,
                        TongTien = tienDien
                    });

                    // Dòng 3: Tiền nước
                    double soNuoc = dienNuoc.ChiSoNuocMoi - dienNuoc.ChiSoNuocCu;
                    double tienNuoc = soNuoc * dienNuoc.DonGiaNuoc;
                    chiTietList.Add(new ChiTietHoaDon
                    {
                        TenDichVu = $"Tiền nước ({dienNuoc.ChiSoNuocCu} → {dienNuoc.ChiSoNuocMoi})",
                        DonGia = dienNuoc.DonGiaNuoc,
                        SoLuong = (int)soNuoc,
                        TongTien = tienNuoc
                    });
                }

                // Dòng 4+: Các dịch vụ khác đã đăng ký cho phòng
                var dangKyDvs = await _context.DangKyDichVus
                    .Include(d => d.DichVuChiNhanh)
                        .ThenInclude(dcn => dcn.DichVu)
                    .Where(d => d.PhongTroId == hd.PhongTroId)
                    .ToListAsync();

                foreach (var dk in dangKyDvs)
                {
                    // Bỏ qua dịch vụ Điện/Nước vì đã tính ở trên
                    var tenDv = dk.DichVuChiNhanh.DichVu.TenDichVu.ToLower();
                    if (tenDv.Contains("điện") || tenDv.Contains("nước")) continue;

                    chiTietList.Add(new ChiTietHoaDon
                    {
                        TenDichVu = dk.DichVuChiNhanh.DichVu.TenDichVu,
                        DonGia = dk.DichVuChiNhanh.GiaDichVu,
                        SoLuong = dk.SoLuong,
                        TongTien = dk.DichVuChiNhanh.GiaDichVu * dk.SoLuong,
                        DichVuId = dk.DichVuChiNhanh.DichVuId
                    });
                }

                double tongTien = chiTietList.Sum(x => x.TongTien);

                // 5. Tạo hóa đơn
                var hoaDon = new HoaDon
                {
                    MaHoaDon = $"HD-{hd.PhongTro.SoPhong}-{thang:D2}{nam}",
                    HopDongId = hd.HopDongId,
                    Thang = thang,
                    Nam = nam,
                    TongTien = tongTien,
                    TrangThaiHoaDon = TrangThaiHoaDon.ChuaThanhToan,
                    DichVuDienNuocCuaPhongId = dienNuoc?.DichVuDienNuocCuaPhongId,
                    ChiTietHoaDonDichVus = chiTietList
                };

                _context.HoaDons.Add(hoaDon);
                soHoaDonMoi++;
            }

            await _context.SaveChangesAsync();
            return (true, $"Đã phát sinh {soHoaDonMoi} hóa đơn mới.", soHoaDonMoi);
        }

        // =================== DANH SÁCH HÓA ĐƠN ===================
        public async Task<DataTableResponse<HoaDonRes>> GetDanhSachHoaDonAsync(DataTableRequest request, int chiNhanhId, int thang, int nam, int trangThai)
        {
            var query = _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Where(h => !h.IsDeleted);

            if (chiNhanhId > 0)
                query = query.Where(h => h.HopDong.PhongTro.ChiNhanhId == chiNhanhId);
            if (thang > 0)
                query = query.Where(h => h.Thang == thang);
            if (nam > 0)
                query = query.Where(h => h.Nam == nam);
            if (trangThai >= 0)
                query = query.Where(h => (int)h.TrangThaiHoaDon == trangThai);

            if (!string.IsNullOrEmpty(request.SearchValue))
            {
                string s = request.SearchValue.ToLower();
                query = query.Where(h => h.MaHoaDon.ToLower().Contains(s) ||
                                         h.HopDong.PhongTro.SoPhong.ToLower().Contains(s) ||
                                         h.HopDong.NguoiThue.HoVaTen.ToLower().Contains(s));
            }

            int totalRecords = await query.CountAsync();

            query = query.OrderByDescending(h => h.HoaDonId);

            var data = await query
                .Skip(request.Start)
                .Take(request.Length)
                .Select(h => new HoaDonRes
                {
                    HoaDonId = h.HoaDonId,
                    MaHoaDon = h.MaHoaDon,
                    HopDongId = h.HopDongId,
                    MaHopDong = h.HopDong.MaHopDong,
                    TenPhong = h.HopDong.PhongTro.SoPhong,
                    TenNguoiThue = h.HopDong.NguoiThue.HoVaTen,
                    TenChiNhanh = h.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                    Thang = h.Thang,
                    Nam = h.Nam,
                    TongTien = h.TongTien,
                    TrangThaiHoaDon = h.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                    TrangThaiHoaDonValue = (int)h.TrangThaiHoaDon,
                    NgayTao = h.NgayTao.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return new DataTableResponse<HoaDonRes>
            {
                draw = request.Draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = data
            };
        }

        // =================== CHI TIẾT HÓA ĐƠN ===================
        public async Task<HoaDonChiTietRes> GetHoaDonByIdAsync(int id)
        {
            var hd = await _context.HoaDons
                .Include(h => h.HopDong).ThenInclude(hd => hd.PhongTro).ThenInclude(p => p.ChiNhanh)
                .Include(h => h.HopDong).ThenInclude(hd => hd.NguoiThue)
                .Include(h => h.ChiTietHoaDonDichVus)
                .Include(h => h.LichSuThanhToans).ThenInclude(l => l.NguoiXacNhan)
                .FirstOrDefaultAsync(h => h.HoaDonId == id && !h.IsDeleted);

            if (hd == null) return null;

            return new HoaDonChiTietRes
            {
                HoaDonId = hd.HoaDonId,
                MaHoaDon = hd.MaHoaDon,
                TenPhong = hd.HopDong.PhongTro.SoPhong,
                TenNguoiThue = hd.HopDong.NguoiThue.HoVaTen,
                TenChiNhanh = hd.HopDong.PhongTro.ChiNhanh.TenChiNhanh,
                DiaChiChiNhanh = hd.HopDong.PhongTro.ChiNhanh.DiaChi,
                Thang = hd.Thang,
                Nam = hd.Nam,
                TongTien = hd.TongTien,
                TrangThaiHoaDon = hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan ? "Đã thanh toán" : "Chưa thanh toán",
                NgayTao = hd.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                ChiTietHoaDons = hd.ChiTietHoaDonDichVus.Where(x => !x.IsDeleted).Select(ct => new ChiTietHoaDonRes
                {
                    ChiTietHoaDonId = ct.ChiTietHoaDonId,
                    TenDichVu = ct.TenDichVu,
                    DonGia = ct.DonGia,
                    SoLuong = ct.SoLuong,
                    TongTien = ct.TongTien
                }).ToList(),
                LichSuThanhToans = hd.LichSuThanhToans.Where(x => !x.IsDeleted).Select(ls => new LichSuThanhToanRes
                {
                    LichSuThanhToanId = ls.LichSuThanhToanId,
                    MaGiaoDich = ls.MaGiaoDich,
                    SoTienThanhToan = ls.SoTienThanhToan,
                    PhuongThucThanhToan = ls.PhuongThucThanhToan == PhuongThucThanhToan.TienMat ? "Tiền mặt" : "Chuyển khoản",
                    NgayThanhToan = ls.NgayThanhToan.ToString("dd/MM/yyyy HH:mm"),
                    NguoiXacNhan = ls.NguoiXacNhan?.TenDangNhap ?? "N/A",
                    GhiChu = ls.GhiChu
                }).ToList()
            };
        }

        // =================== THU TIỀN ===================
        public async Task<(bool IsSuccess, string ErrorMessage)> ThuTienAsync(int hoaDonId, int phuongThuc, string ghiChu, int nguoiXacNhanId)
        {
            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.HoaDonId == hoaDonId && !h.IsDeleted);
            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                return (false, "Hóa đơn này đã được thanh toán trước đó.");

            var lichSu = new LichSuThanhToan
            {
                MaGiaoDich = $"GD-{DateTime.UtcNow:yyyyMMddHHmmss}-{hoaDonId}",
                HoaDonId = hoaDonId,
                NguoiXacNhanId = nguoiXacNhanId,
                SoTienThanhToan = hd.TongTien,
                PhuongThucThanhToan = (PhuongThucThanhToan)phuongThuc,
                NgayThanhToan = DateTime.UtcNow,
                GhiChu = ghiChu
            };

            _context.LichSuThanhToans.Add(lichSu);

            hd.TrangThaiHoaDon = TrangThaiHoaDon.DaThanhToan;
            hd.NgayCapNhat = DateTime.UtcNow;
            _context.HoaDons.Update(hd);

            await _context.SaveChangesAsync();
            return (true, null);
        }

        // =================== XÓA HÓA ĐƠN ===================
        public async Task<(bool IsSuccess, string ErrorMessage)> DeleteHoaDonAsync(int id)
        {
            var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.HoaDonId == id && !h.IsDeleted);
            if (hd == null) return (false, "Không tìm thấy hóa đơn.");
            if (hd.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                return (false, "Không thể xóa hóa đơn đã thanh toán.");

            hd.IsDeleted = true;
            hd.NgayCapNhat = DateTime.UtcNow;
            _context.HoaDons.Update(hd);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        // =================== XUẤT EXCEL ===================
        public async Task<byte[]> ExportExcelAsync(int hoaDonId)
        {
            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Hóa đơn");

            // Header info
            ws.Cell("A1").Value = "HÓA ĐƠN THANH TOÁN";
            ws.Range("A1:E1").Merge().Style.Font.Bold = true;
            ws.Range("A1:E1").Style.Font.FontSize = 16;
            ws.Range("A1:E1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell("A3").Value = "Mã hóa đơn:"; ws.Cell("B3").Value = hd.MaHoaDon;
            ws.Cell("A4").Value = "Chi nhánh:"; ws.Cell("B4").Value = hd.TenChiNhanh;
            ws.Cell("A5").Value = "Phòng:"; ws.Cell("B5").Value = hd.TenPhong;
            ws.Cell("A6").Value = "Người thuê:"; ws.Cell("B6").Value = hd.TenNguoiThue;
            ws.Cell("A7").Value = "Kỳ thanh toán:"; ws.Cell("B7").Value = $"Tháng {hd.Thang}/{hd.Nam}";
            ws.Cell("A8").Value = "Trạng thái:"; ws.Cell("B8").Value = hd.TrangThaiHoaDon;
            ws.Cell("A9").Value = "Ngày tạo:"; ws.Cell("B9").Value = hd.NgayTao;

            ws.Range("A3:A9").Style.Font.Bold = true;

            // Table header
            int row = 11;
            ws.Cell(row, 1).Value = "STT";
            ws.Cell(row, 2).Value = "Tên dịch vụ";
            ws.Cell(row, 3).Value = "Đơn giá";
            ws.Cell(row, 4).Value = "Số lượng";
            ws.Cell(row, 5).Value = "Thành tiền";

            var headerRange = ws.Range(row, 1, row, 5);
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
                ws.Cell(row, 5).Value = ct.TongTien;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            }

            // Total row
            row++;
            ws.Cell(row, 1).Value = "";
            ws.Range(row, 1, row, 4).Merge();
            ws.Cell(row, 4).Value = "TỔNG CỘNG:";
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 5).Value = hd.TongTien;
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;

            // Border
            var tableRange = ws.Range(11, 1, row, 5);
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Column widths
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 35;
            ws.Column(3).Width = 15;
            ws.Column(4).Width = 12;
            ws.Column(5).Width = 18;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // =================== XUẤT PDF ===================
        public async Task<byte[]> ExportPdfAsync(int hoaDonId)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var hd = await GetHoaDonByIdAsync(hoaDonId);
            if (hd == null) return null;

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
                                columns.ConstantColumn(60);  // SL
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
                                table.Cell().Background(bgColor).Padding(5).AlignRight().Text(FormatVND(ct.TongTien));
                                stt++;
                            }
                        });

                        col.Item().PaddingTop(5).AlignRight().Text(t =>
                        {
                            t.Span("TỔNG CỘNG: ").Bold().FontSize(14);
                            t.Span(FormatVND(hd.TongTien)).Bold().FontSize(14).FontColor(Colors.Red.Darken2);
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Ngày xuất: ").FontSize(9);
                        t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                    });
                });
            });

            return document.GeneratePdf();
        }

        private string FormatVND(double amount)
        {
            return string.Format("{0:#,##0} ₫", amount);
        }
    }
}
