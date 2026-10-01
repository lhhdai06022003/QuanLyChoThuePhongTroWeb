using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class InvoiceDueDateCalculatorTests
    {
        [Fact]
        public void Compute_28Sep0800Utc_7Days_Returns05Oct1659_59Utc()
        {
            var ngayGuiUtc = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

            var han = InvoiceDueDateCalculator.Compute(ngayGuiUtc, 7);

            Assert.Equal(new DateTime(2026, 10, 5, 16, 59, 59, DateTimeKind.Utc), han);
            Assert.Equal(DateTimeKind.Utc, han.Kind);
        }

        [Fact]
        public void Compute_28Sep1659_59Utc_StillSameVnDay_7Days_Returns05Oct1659_59Utc()
        {
            // 28/09 16:59:59 UTC = 23:59:59 giờ VN ngày 28/09
            var ngayGuiUtc = new DateTime(2026, 9, 28, 16, 59, 59, DateTimeKind.Utc);

            var han = InvoiceDueDateCalculator.Compute(ngayGuiUtc, 7);

            Assert.Equal(new DateTime(2026, 10, 5, 16, 59, 59, DateTimeKind.Utc), han);
        }

        [Fact]
        public void Compute_28Sep1700Utc_RollsToVnDay29_7Days_Returns06Oct1659_59Utc()
        {
            // 28/09 17:00:00 UTC = 00:00:00 giờ VN ngày 29/09 -> ngày gửi VN là 29/09
            var ngayGuiUtc = new DateTime(2026, 9, 28, 17, 0, 0, DateTimeKind.Utc);

            var han = InvoiceDueDateCalculator.Compute(ngayGuiUtc, 7);

            Assert.Equal(new DateTime(2026, 10, 6, 16, 59, 59, DateTimeKind.Utc), han);
        }

        [Fact]
        public void Compute_28Sep1730Utc_RollsToVnDay29_7Days_Returns06Oct1659_59Utc()
        {
            // 28/09 17:30:00 UTC = 00:30:00 giờ VN ngày 29/09 -> ngày gửi VN là 29/09
            var ngayGuiUtc = new DateTime(2026, 9, 28, 17, 30, 0, DateTimeKind.Utc);

            var han = InvoiceDueDateCalculator.Compute(ngayGuiUtc, 7);

            Assert.Equal(new DateTime(2026, 10, 6, 16, 59, 59, DateTimeKind.Utc), han);
            Assert.Equal(DateTimeKind.Utc, han.Kind);
        }

        [Fact]
        public void Compute_28Dec0800Utc_7Days_CrossesYearBoundary_Returns04Jan2027_1659_59Utc()
        {
            var ngayGuiUtc = new DateTime(2026, 12, 28, 8, 0, 0, DateTimeKind.Utc);

            var han = InvoiceDueDateCalculator.Compute(ngayGuiUtc, 7);

            Assert.Equal(new DateTime(2027, 1, 4, 16, 59, 59, DateTimeKind.Utc), han);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Compute_SoNgayNotPositive_ThrowsArgumentOutOfRangeException(int soNgay)
        {
            var ngayGuiUtc = DateTime.UtcNow;

            Assert.Throws<ArgumentOutOfRangeException>(() => InvoiceDueDateCalculator.Compute(ngayGuiUtc, soNgay));
        }

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void Compute_NgayGuiNotUtc_ThrowsArgumentOutOfRangeException(DateTimeKind kind)
        {
            var ngayGuiKhongUtc = DateTime.SpecifyKind(new DateTime(2026, 9, 28, 8, 0, 0), kind);

            Assert.Throws<ArgumentOutOfRangeException>(() => InvoiceDueDateCalculator.Compute(ngayGuiKhongUtc, 7));
        }

        [Fact]
        public void Validate_DefaultValue_DoesNotThrow()
        {
            var options = new InvoiceIssuanceOptions();

            options.Validate();

            Assert.Equal(7, options.SoNgayHanThanhToan);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Validate_NonPositive_ThrowsInvalidOperationException(int soNgay)
        {
            var options = new InvoiceIssuanceOptions { SoNgayHanThanhToan = soNgay };

            var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
            Assert.Equal("Cấu hình InvoiceIssuanceOptions:SoNgayHanThanhToan phải lớn hơn 0.", ex.Message);
        }
    }
}
