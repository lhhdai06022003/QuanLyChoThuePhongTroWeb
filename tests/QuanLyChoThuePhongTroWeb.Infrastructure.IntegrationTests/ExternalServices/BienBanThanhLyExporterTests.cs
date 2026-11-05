using System;
using System.Collections.Generic;
using System.Text;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không cần DB: chỉ kiểm tra dựng được file PDF.
    public class BienBanThanhLyExporterTests
    {
        private static BienBanThanhLyDto Sample(decimal noConLai, string? ghiChu) => new()
        {
            MaHopDong = "HD-CN-2510-001",
            TenChiNhanh = "Chi nhánh Một",
            DiaChiChiNhanh = "1 Đường A",
            SoDienThoaiChiNhanh = "0900000000",
            SoPhong = "P101",
            TenNguoiDaiDien = "Nguyễn Văn A",
            CccdNguoiDaiDien = "0123456789",
            SdtNguoiDaiDien = "0911111111",
            ThanhVien = new List<string> { "Nguyễn Văn A", "Trần Thị B" },
            ThoiDiemBatDauUtc = new DateTime(2025, 10, 31, 17, 0, 0, DateTimeKind.Utc),
            ThoiDiemKetThucUtc = new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc),
            NgayThanhLyUtc = new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc),
            NgayInUtc = new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc),
            TienCoc = 3_000_000m,
            TongCanTru = 2_500_000m,
            CocHoanTrucTiep = 500_000m,
            NoConLai = noConLai,
            GhiChu = ghiChu,
            HoaDons = new List<BienBanHoaDonDto>
            {
                new() { MaHoaDon = "HDN-1", MaHopDong = "HD-CN-2510-001", Thang = 10, Nam = 2026, TongTien = 2_500_000m, DaThuKhac = 0, CanTru = 2_500_000m, ConLai = noConLai }
            }
        };

        [Fact]
        public void ExportPdf_WithDebtAndNote_ReturnsPdfBytes()
        {
            var bytes = new BienBanThanhLyExporter().ExportPdf(Sample(500_000m, "Hẹn trả trước 30/11"));

            Assert.True(bytes.Length > 100);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public void ExportPdf_NoDebtNoNote_DoesNotThrow()
        {
            var bytes = new BienBanThanhLyExporter().ExportPdf(Sample(0m, null));

            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
    }
}
