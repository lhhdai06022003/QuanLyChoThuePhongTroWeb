using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoiceStatusLabelsTests
    {
        [Theory]
        [InlineData(TrangThaiPhatHanhHoaDon.Nhap, "Nháp")]
        [InlineData(TrangThaiPhatHanhHoaDon.ChoDuyet, "Chờ duyệt")]
        [InlineData(TrangThaiPhatHanhHoaDon.DaChot, "Đã chốt")]
        [InlineData(TrangThaiPhatHanhHoaDon.DaGui, "Đã gửi")]
        [InlineData(TrangThaiPhatHanhHoaDon.DaHuy, "Đã hủy")]
        [InlineData(null, "—")]
        public void PhatHanh_ReturnsExpectedLabel(TrangThaiPhatHanhHoaDon? status, string expected)
        {
            var result = InvoiceStatusLabels.PhatHanh(status);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void PhatHanh_InvalidEnumValue_ReturnsDash()
        {
            var result = InvoiceStatusLabels.PhatHanh((TrangThaiPhatHanhHoaDon)99);
            Assert.Equal("—", result);
        }

        [Theory]
        [InlineData(TrangThaiHoaDon.ChuaThanhToan, "Chưa thanh toán")]
        [InlineData(TrangThaiHoaDon.DaThanhToan, "Đã thanh toán")]
        [InlineData(TrangThaiHoaDon.ThanhToanMotPhan, "Thanh toán một phần")]
        [InlineData(null, "—")]
        public void ThanhToan_ReturnsExpectedLabel(TrangThaiHoaDon? status, string expected)
        {
            var result = InvoiceStatusLabels.ThanhToan(status);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ThanhToan_InvalidEnumValue_ReturnsDash()
        {
            var result = InvoiceStatusLabels.ThanhToan((TrangThaiHoaDon)99);
            Assert.Equal("—", result);
        }
    }
}
