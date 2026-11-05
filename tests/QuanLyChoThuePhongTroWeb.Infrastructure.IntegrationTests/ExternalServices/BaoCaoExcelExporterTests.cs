using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không cần DB: chỉ dựng workbook từ DTO rồi mở lại.
    public class BaoCaoExcelExporterTests
    {
        private static readonly BaoCaoPhamViDto PhamVi = new()
        {
            TenChiNhanh = "Tất cả chi nhánh",
            TuThang = 1, TuNam = 2026, DenThang = 3, DenNam = 2026,
            TuKyText = "T1/2026", DenKyText = "T3/2026",
            ThoiDiemTinhText = "05/11/2026 10:00"
        };

        private readonly BaoCaoExcelExporter _exporter = new();

        private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

        private static string? GiaTriCanh(IXLWorksheet ws, string nhan)
        {
            var cell = ws.Column(1).CellsUsed().FirstOrDefault(c => c.GetString() == nhan);
            return cell?.CellRight().GetString();
        }

        [Fact]
        public void XuatDoanhThu_CoSheetTongHopVaOCoKieuSo()
        {
            var tong = new BaoCaoDoanhThuDongDto { Nhan = "Tổng", PhatSinh = 2_000_000m, DaThu = 1_234_567m, ThuTien = 1_200_000m, CanTruCoc = 34_567m, ConNo = 765_433m, TyLeThu = 61.7m, TienThua = 5_000m, ThuKhacGiuCho = 300_000m };
            var data = new BaoCaoDoanhThuDto
            {
                PhamVi = PhamVi,
                Tong = tong,
                TheoKy = new[] { new BaoCaoDoanhThuDongDto { Nhan = "T1/2026", Thang = 1, Nam = 2026, PhatSinh = 1m }, new BaoCaoDoanhThuDongDto { Nhan = "T2/2026", TyLeThu = null } },
                TheoChiNhanh = new[] { new BaoCaoDoanhThuDongDto { Nhan = "Chi nhánh A", ChiNhanhId = 1 } },
                TienCocDangGiu = 9_000_000m,
                GiuChoDangChoQuyetDinh = 400_000m
            };

            using var wb = Open(_exporter.XuatDoanhThu(data));

            Assert.Equal("Tổng hợp", wb.Worksheets.First().Name);
            Assert.True(wb.Worksheets.Contains("Theo kỳ"));
            Assert.True(wb.Worksheets.Contains("Theo chi nhánh"));
            var ws = wb.Worksheet("Tổng hợp");
            Assert.Equal("Tất cả chi nhánh", GiaTriCanh(ws, "Chi nhánh"));
            Assert.Equal("T1/2026", GiaTriCanh(ws, "Từ kỳ"));
            Assert.Equal("05/11/2026 10:00", GiaTriCanh(ws, "Ngày xuất"));

            var daThu = ws.Column(1).CellsUsed().First(c => c.GetString() == "Đã thu").CellRight();
            Assert.Equal(XLDataType.Number, daThu.DataType);
            Assert.Equal(1_234_567d, daThu.GetDouble());
            Assert.Equal("#,##0", daThu.Style.NumberFormat.Format);
            foreach (var nhan in new[] { "Phát sinh", "Thu tiền", "Cấn trừ cọc", "Còn nợ", "Tỷ lệ thu (%)", "Tiền thừa", "Tiền cọc đang giữ", "Thu khác: tiền giữ chỗ không hoàn", "Tiền giữ chỗ đang chờ quyết định" })
            {
                Assert.NotNull(GiaTriCanh(ws, nhan));
            }

            Assert.Equal(9_000_000d, ws.Column(1).CellsUsed().First(c => c.GetString() == "Tiền cọc đang giữ").CellRight().GetDouble());
        }

        [Fact]
        public void XuatDoanhThu_KhongCoChiNhanh_KhongCoSheetTheoChiNhanh()
        {
            using var wb = Open(_exporter.XuatDoanhThu(new BaoCaoDoanhThuDto { PhamVi = PhamVi }));
            Assert.False(wb.Worksheets.Contains("Theo chi nhánh"));
        }

        [Fact]
        public void XuatCongNo_DungTinhToiThayChoKhoangKy()
        {
            var data = new BaoCaoCongNoDto
            {
                PhamVi = PhamVi,
                TongConNo = 5_000m,
                NhomTuoiNo = new[] { new BaoCaoTuoiNoDto { Ma = "ChuaDenHan", Nhan = "Chưa đến hạn", SoHoaDon = 1, SoTien = 5_000m } },
                TheoKhach = new[] { new BaoCaoKhachNoDto { HoVaTen = "An", SoPhong = "P1", TenChiNhanh = "A", SoHoaDon = 1, TongNo = 5_000m, KyNoCuNhat = "T1/2026", SoNgayQuaHanLonNhat = null, DaTraPhong = true } }
            };

            using var wb = Open(_exporter.XuatCongNo(data));
            var ws = wb.Worksheet("Tổng hợp");

            Assert.Equal("05/11/2026 10:00", GiaTriCanh(ws, "Tính tới"));
            Assert.Null(GiaTriCanh(ws, "Từ kỳ"));
            Assert.Equal(5_000d, ws.Column(1).CellsUsed().First(c => c.GetString() == "Tổng còn nợ").CellRight().GetDouble());
            Assert.True(wb.Worksheets.Contains("Tuổi nợ"));
            Assert.True(wb.Worksheets.Contains("Theo khách"));
        }

        [Fact]
        public void XuatCongSuat_HopDong_DienNuoc_MoDuocVaCoSheetChiTiet()
        {
            using var congSuat = Open(_exporter.XuatCongSuatPhong(new BaoCaoCongSuatDto
            {
                PhamVi = PhamVi,
                TongPhong = 3,
                Tong = new BaoCaoCongSuatDongDto { Nhan = "Tổng", NgayPhongCoTheThue = 90, NgayPhongDaThue = 45, TyLeLapDay = 50.0m },
                TheoKy = new[] { new BaoCaoCongSuatDongDto { Nhan = "T1/2026", TyLeLapDay = null } },
                PhongTrongLauNhat = new[] { new BaoCaoPhongTrongDto { SoPhong = "P1", TenChiNhanh = "A", SoNgayTrong = 10, TuNgayText = "01/01/2026" } }
            }));
            Assert.Equal("Tổng hợp", congSuat.Worksheets.First().Name);
            Assert.True(congSuat.Worksheets.Contains("Phòng trống lâu"));

            using var hopDong = Open(_exporter.XuatHopDong(new BaoCaoHopDongDto
            {
                PhamVi = PhamVi,
                DangHoatDong = 4,
                Tong = new BaoCaoHopDongDongDto { Nhan = "Tổng", HopDongMoi = 2 },
                TheoKy = new[] { new BaoCaoHopDongDongDto { Nhan = "T1/2026" } }
            }));
            Assert.Equal(4d, hopDong.Worksheet("Tổng hợp").Column(1).CellsUsed().First(c => c.GetString() == "Đang hoạt động").CellRight().GetDouble());

            using var dienNuoc = Open(_exporter.XuatDienNuoc(new BaoCaoDienNuocDto
            {
                PhamVi = PhamVi,
                TongKwh = 12.345m,
                TheoKy = new[] { new BaoCaoDienNuocDongDto { Nhan = "T1/2026", Kwh = 12.345m } },
                TopPhong = new[] { new BaoCaoPhongDienNuocDto { SoPhong = "P1", TenChiNhanh = "A", Kwh = 12.345m, SoKy = 1 } }
            }));
            var kwh = dienNuoc.Worksheet("Tổng hợp").Column(1).CellsUsed().First(c => c.GetString() == "Tổng kWh").CellRight();
            Assert.Equal(12.345d, kwh.GetDouble(), 3);
            Assert.Equal("#,##0.###", kwh.Style.NumberFormat.Format);
            Assert.True(dienNuoc.Worksheets.Contains("Phòng dùng nhiều"));
        }
    }
}
