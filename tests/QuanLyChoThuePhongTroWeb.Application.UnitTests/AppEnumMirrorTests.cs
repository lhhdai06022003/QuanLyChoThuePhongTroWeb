using System;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AppEnumMirrorTests
    {
        [Fact]
        public void AppEnumMirror_MatchesDomain_LoaiDongHo()
        {
            var appNames = Enum.GetNames(typeof(AppLoaiDongHo));
            var domainNames = Enum.GetNames(typeof(LoaiDongHo));

            Assert.Equal(domainNames.Length, appNames.Length);

            foreach (var name in domainNames)
            {
                Assert.Contains(name, appNames);
                var domainVal = (int)Enum.Parse(typeof(LoaiDongHo), name);
                var appVal = (int)Enum.Parse(typeof(AppLoaiDongHo), name);
                Assert.Equal(domainVal, appVal);
            }
        }

        [Fact]
        public void AppEnumMirror_MatchesDomain_TrangThaiAnhChiSo()
        {
            var appNames = Enum.GetNames(typeof(AppTrangThaiAnhChiSo));
            var domainNames = Enum.GetNames(typeof(TrangThaiXuLyAnhChiSo));

            Assert.Equal(domainNames.Length, appNames.Length);

            foreach (var name in domainNames)
            {
                Assert.Contains(name, appNames);
                var domainVal = (int)Enum.Parse(typeof(TrangThaiXuLyAnhChiSo), name);
                var appVal = (int)Enum.Parse(typeof(AppTrangThaiAnhChiSo), name);
                Assert.Equal(domainVal, appVal);
            }
        }

        [Theory]
        [InlineData(typeof(AppTrangThaiYeuCauThanhToan), typeof(TrangThaiYeuCauThanhToan))]
        [InlineData(typeof(AppTrangThaiMinhChungThanhToan), typeof(TrangThaiMinhChungThanhToan))]
        public void AppEnumMirror_MatchesDomain_ThanhToanHoaDon(Type appEnum, Type domainEnum)
        {
            var appNames = Enum.GetNames(appEnum);
            var domainNames = Enum.GetNames(domainEnum);

            Assert.Equal(domainNames.Length, appNames.Length);

            foreach (var name in domainNames)
            {
                Assert.Contains(name, appNames);
                Assert.Equal((int)Enum.Parse(domainEnum, name), (int)Enum.Parse(appEnum, name));
            }
        }
    }
}
