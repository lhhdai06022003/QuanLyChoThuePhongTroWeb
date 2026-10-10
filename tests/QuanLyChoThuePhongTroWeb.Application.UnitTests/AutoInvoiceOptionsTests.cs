using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AutoInvoiceOptionsTests
    {
        [Fact]
        public void Defaults_AreValid()
        {
            var options = new AutoInvoiceOptions();

            options.Validate();

            Assert.Equal(0.90, options.NguongDoTinCay);
            Assert.Equal(3m, options.HeSoBatThuong);
            Assert.Equal(3, options.SoKyTrungBinh);
            Assert.Equal(500m, options.TranDienKwh);
            Assert.Equal(30m, options.TranNuocM3);
            Assert.Equal(5, options.ChuKyJobPhut);
        }

        [Fact]
        public void NguongDoTinCay_One_IsValid()
        {
            new AutoInvoiceOptions { NguongDoTinCay = 1 }.Validate();
        }

        [Theory]
        [InlineData(0d)]
        [InlineData(-0.1d)]
        [InlineData(1.01d)]
        [InlineData(double.NaN)]
        public void NguongDoTinCay_OutOfRange_Throws(double value)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { NguongDoTinCay = value }.Validate());
            Assert.Contains("NguongDoTinCay", ex.Message);
        }

        [Fact]
        public void HeSoBatThuong_Zero_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { HeSoBatThuong = 0 }.Validate());
            Assert.Contains("HeSoBatThuong", ex.Message);
        }

        [Fact]
        public void SoKyTrungBinh_Zero_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { SoKyTrungBinh = 0 }.Validate());
            Assert.Contains("SoKyTrungBinh", ex.Message);
        }

        [Fact]
        public void TranDienKwh_Zero_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { TranDienKwh = 0 }.Validate());
            Assert.Contains("TranDienKwh", ex.Message);
        }

        [Fact]
        public void TranNuocM3_Zero_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { TranNuocM3 = 0 }.Validate());
            Assert.Contains("TranNuocM3", ex.Message);
        }

        [Fact]
        public void ChuKyJobPhut_Zero_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { ChuKyJobPhut = 0 }.Validate());
            Assert.Contains("ChuKyJobPhut", ex.Message);
        }
    }
}
