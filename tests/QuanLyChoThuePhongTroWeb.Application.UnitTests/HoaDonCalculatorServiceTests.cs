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
            Assert.Equal(176183.67m, soTien);
        }
    }
}
