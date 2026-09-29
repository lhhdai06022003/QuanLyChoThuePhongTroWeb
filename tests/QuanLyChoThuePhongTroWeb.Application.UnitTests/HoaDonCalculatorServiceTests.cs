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
            double giaThue = 3000000;
            DateTime batDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? ketThuc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

            // Act
            var (soTien, dienGiai, soNgayO) = _calculator.TinhTienPhong(giaThue, batDau, ketThuc, 6, 2026);

            // Assert
            Assert.Equal(3000000, soTien);
            Assert.Equal(30, soNgayO);
            Assert.Equal("Tiền thuê phòng", dienGiai);
        }

        [Fact]
        public void TinhTienPhong_PartialMonth_ReturnsProRatedRentPrice()
        {
            // Arrange
            double giaThue = 3000000;
            DateTime batDau = new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
            DateTime? ketThuc = null;

            // Act
            var (soTien, dienGiai, soNgayO) = _calculator.TinhTienPhong(giaThue, batDau, ketThuc, 6, 2026);

            // Assert
            Assert.Equal(15, soNgayO);
            Assert.Equal(1500000, soTien);
            Assert.Contains("thực tế ở 15/30 ngày", dienGiai);
        }

        [Fact]
        public void TinhTienDienNuoc_Normal_ReturnsExactCost()
        {
            // Arrange
            double chiSoCu = 100;
            double chiSoMoi = 150;
            double donGia = 3500;
            string tenDichVu = "Điện";
            int soNgayO = 30;
            int tongNgayTrongThang = 30;

            // Act
            var (soLuong, soTien, dienGiai) = _calculator.TinhTienDienNuoc(chiSoMoi, chiSoCu, donGia, tenDichVu, soNgayO, tongNgayTrongThang);

            // Assert
            Assert.Equal(50, soLuong);
            Assert.Equal(175000, soTien);
            Assert.Contains("100 → 150", dienGiai);
        }

        [Fact]
        public void TinhTienDienNuoc_InvalidNewIndex_ThrowsInvalidOperationException()
        {
            // Arrange
            double chiSoCu = 200;
            double chiSoMoi = 150;

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _calculator.TinhTienDienNuoc(chiSoMoi, chiSoCu, 3500, "Điện", 30, 30));

            Assert.Contains("nhỏ hơn chỉ số cũ", ex.Message);
        }

        [Fact]
        public void TinhTienDichVuCoDinh_FullMonth_ReturnsExpectedAmount()
        {
            // Arrange
            double giaDv = 100000;
            int soLuong = 2;
            DateTime batDau = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime? ketThuc = null;

            // Act
            var (soTien, dienGiai) = _calculator.TinhTienDichVuCoDinh(giaDv, soLuong, "Phí giữ xe", batDau, ketThuc, 6, 2026);

            // Assert
            Assert.Equal(200000, soTien);
            Assert.Contains("Phí giữ xe", dienGiai);
        }
    }
}
