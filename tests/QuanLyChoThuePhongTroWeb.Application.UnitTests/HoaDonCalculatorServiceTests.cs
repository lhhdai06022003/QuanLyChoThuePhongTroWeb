using System;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class HoaDonCalculatorServiceTests
    {
        private readonly HoaDonCalculatorService _calculator = new();

        [Fact]
        public void TinhTienPhong_FullMonth_ReturnsExactRentPrice()
        {
            // Arrange
            decimal giaThue = 3000000m;
            DateTime batDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? ketThuc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

            // Act
            var (soTien, dienGiai, soNgayO) = _calculator.TinhTienPhong(giaThue, batDau, ketThuc, 6, 2026);

            // Assert
            Assert.Equal(3000000m, soTien);
            Assert.Equal(30, soNgayO);
            Assert.Equal("Tiền thuê phòng", dienGiai);
        }

        [Fact]
        public void TinhTienPhong_PartialMonth_ReturnsProRatedRentPrice()
        {
            // Arrange
            decimal giaThue = 3000000m;
            DateTime batDau = new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
            DateTime? ketThuc = null;

            // Act
            var (soTien, dienGiai, soNgayO) = _calculator.TinhTienPhong(giaThue, batDau, ketThuc, 6, 2026);

            // Assert
            Assert.Equal(15, soNgayO);
            Assert.Equal(1500000m, soTien);
            Assert.Contains("thực tế ở 15/30 ngày", dienGiai);
        }

        [Fact]
        public void TinhTienDienNuoc_Normal_ReturnsExactCost()
        {
            // Arrange
            decimal chiSoCu = 100m;
            decimal chiSoMoi = 150m;
            decimal donGia = 3500m;
            string tenDichVu = "Điện";
            int soNgayO = 30;
            int tongNgayTrongThang = 30;

            // Act
            var (soLuong, soTien, dienGiai) = _calculator.TinhTienDienNuoc(chiSoMoi, chiSoCu, donGia, tenDichVu, soNgayO, tongNgayTrongThang);

            // Assert
            Assert.Equal(50m, soLuong);
            Assert.Equal(175000m, soTien);
            Assert.Contains("100 → 150", dienGiai);
        }

        [Fact]
        public void TinhTienDienNuoc_DbScaleReadings_DescriptionHasNoTrailingZeros()
        {
            // Chỉ số đọc từ DB có scale 3 (100.000m); diễn giải không được in "100.000" (dễ đọc thành một trăm nghìn).
            var (_, _, dienGiai) = _calculator.TinhTienDienNuoc(190m, 100.000m, 3500m, "Tiền điện", 30, 30);
            Assert.Equal("Tiền điện (100 → 190)", dienGiai);

            var (_, _, tyLe) = _calculator.TinhTienDienNuoc(1250.500m, 1234.000m, 3500m, "Tiền điện", 15, 30);
            Assert.Equal("Tiền điện (tỷ lệ 15/30 ngày, 1.234 → 1.250,5)", tyLe);
        }

        [Theory]
        [InlineData("100.000", "100")]
        [InlineData("0", "0")]
        [InlineData("1234.5", "1.234,5")]
        [InlineData("1234567.125", "1.234.567,125")]
        [InlineData("18.200", "18,2")]
        public void ChiSo_DinhDang_VietnameseStyleWithoutTrailingZeros(string input, string expected)
        {
            var value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expected, QuanLyChoThuePhongTroWeb.Application.Common.Formatting.ChiSo.DinhDang(value));
        }

        [Fact]
        public void TinhTienDienNuoc_InvalidNewIndex_ThrowsInvalidOperationException()
        {
            // Arrange
            decimal chiSoCu = 200m;
            decimal chiSoMoi = 150m;

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _calculator.TinhTienDienNuoc(chiSoMoi, chiSoCu, 3500m, "Điện", 30, 30));

            Assert.Contains("nhỏ hơn chỉ số cũ", ex.Message);
        }

        [Fact]
        public void TinhTienDichVuCoDinh_FullMonth_ReturnsExpectedAmount()
        {
            // Arrange
            decimal giaDv = 100000m;
            decimal soLuong = 2m;
            DateTime batDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? ketThuc = null;

            // Act
            var (soTien, dienGiai) = _calculator.TinhTienDichVuCoDinh(giaDv, soLuong, "Phí giữ xe", batDau, ketThuc, 6, 2026);

            // Assert
            Assert.Equal(200000m, soTien);
            Assert.Contains("Phí giữ xe", dienGiai);
        }

        [Fact]
        public void Calculate_UsesDecimalWithoutBinaryRoundingDrift()
        {
            decimal quantity = 12.345m;
            decimal unitPrice = 3_456.78m;
            decimal total = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(42_673.95m, total);
        }

        [Fact]
        public void TinhTienDienNuoc_DecimalPrecision_ReturnsExactCalculation()
        {
            decimal chiSoCu = 100.125m;
            decimal chiSoMoi = 150.456m;
            decimal donGia = 3500.50m;
            (decimal soLuong, decimal soTien, string dienGiai) = _calculator.TinhTienDienNuoc(chiSoMoi, chiSoCu, donGia, "Điện", 30, 30);
            Assert.Equal(50.331m, soLuong);
            // 50.331 × 3500.50 = 176183.6655, làm tròn về đồng nguyên (spec thanh toán §31.1).
            Assert.Equal(176184m, soTien);
        }

        [Theory]
        [InlineData(3_700_000, 2026, 2, 1)]
        [InlineData(3_700_000, 2026, 1, 17)]
        [InlineData(1_234_567, 2026, 4, 30)]
        public void MoneyResults_AreWholeVnd_ForProratedPeriods(decimal gia, int nam, int thang, int ngayBatDau)
        {
            var batDauUtc = new DateTime(nam, thang, ngayBatDau, 0, 0, 0, DateTimeKind.Utc).AddHours(-7);

            var (tienPhong, _, _) = _calculator.TinhTienPhong(gia, batDauUtc, null, thang, nam);
            var (tienDichVu, _) = _calculator.TinhTienDichVuCoDinh(gia, 3, "Wifi", batDauUtc, null, thang, nam);
            var (_, tienDien, _) = _calculator.TinhTienDienNuoc(123.457m, 100m, 3_333m, "Điện", 13, 31);

            Assert.Equal(decimal.Truncate(tienPhong), tienPhong);
            Assert.Equal(decimal.Truncate(tienDichVu), tienDichVu);
            Assert.Equal(decimal.Truncate(tienDien), tienDien);
        }
    }
}
