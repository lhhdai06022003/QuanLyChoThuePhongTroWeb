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
            Assert.Equal(6, options.SoKyLichSuToiDa);
            Assert.Equal(2, options.SoKyToiThieu);
            Assert.Equal(2m, options.HeSoTang);
            Assert.Equal(50m, options.SanTangDienKwh);
            Assert.Equal(5m, options.SanTangNuocM3);
            Assert.Equal(0.3m, options.HeSoThap);
            Assert.Equal(20m, options.NguongXetThapDienKwh);
            Assert.Equal(2m, options.NguongXetThapNuocM3);
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
        public void SoKyLichSuToiDa_Zero_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { SoKyLichSuToiDa = 0, SoKyToiThieu = 0 }.Validate());
            Assert.Contains("SoKyLichSuToiDa", ex.Message);
        }

        [Theory]
        [InlineData(0, 6)]
        [InlineData(7, 6)]
        [InlineData(3, 2)]
        public void SoKyToiThieu_OutOfRange_Throws(int soKyToiThieu, int soKyLichSuToiDa)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                new AutoInvoiceOptions { SoKyToiThieu = soKyToiThieu, SoKyLichSuToiDa = soKyLichSuToiDa }.Validate());
            Assert.Contains("SoKyToiThieu", ex.Message);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(6, 6)]
        public void SoKyToiThieu_Boundaries_AreValid(int soKyToiThieu, int soKyLichSuToiDa)
        {
            new AutoInvoiceOptions { SoKyToiThieu = soKyToiThieu, SoKyLichSuToiDa = soKyLichSuToiDa }.Validate();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(0.5)]
        [InlineData(0)]
        public void HeSoTang_NotAboveOne_Throws(double value)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { HeSoTang = (decimal)value }.Validate());
            Assert.Contains("HeSoTang", ex.Message);
        }

        [Fact]
        public void HeSoTang_JustAboveOne_IsValid()
        {
            new AutoInvoiceOptions { HeSoTang = 1.01m }.Validate();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-0.1)]
        [InlineData(1.5)]
        public void HeSoThap_OutOfRange_Throws(double value)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { HeSoThap = (decimal)value }.Validate());
            Assert.Contains("HeSoThap", ex.Message);
        }

        [Fact]
        public void SanVaNguongXetThap_Zero_IsValid()
        {
            new AutoInvoiceOptions { SanTangDienKwh = 0, SanTangNuocM3 = 0, NguongXetThapDienKwh = 0, NguongXetThapNuocM3 = 0 }.Validate();
        }

        [Fact]
        public void SanTangDienKwh_Negative_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { SanTangDienKwh = -1 }.Validate());
            Assert.Contains("SanTangDienKwh", ex.Message);
        }

        [Fact]
        public void SanTangNuocM3_Negative_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { SanTangNuocM3 = -1 }.Validate());
            Assert.Contains("SanTangNuocM3", ex.Message);
        }

        [Fact]
        public void NguongXetThapDienKwh_Negative_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { NguongXetThapDienKwh = -1 }.Validate());
            Assert.Contains("NguongXetThapDienKwh", ex.Message);
        }

        [Fact]
        public void NguongXetThapNuocM3_Negative_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AutoInvoiceOptions { NguongXetThapNuocM3 = -1 }.Validate());
            Assert.Contains("NguongXetThapNuocM3", ex.Message);
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
