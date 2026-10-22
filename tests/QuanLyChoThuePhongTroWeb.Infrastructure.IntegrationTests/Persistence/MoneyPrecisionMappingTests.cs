using System;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    public class MoneyPrecisionMappingTests
    {
        private readonly ApplicationDbContext _context;

        public MoneyPrecisionMappingTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=test_dummy_db;Username=postgres")
                .Options;
            _context = new ApplicationDbContext(options);
        }

        [Theory]
        [InlineData(typeof(PhongTro), nameof(PhongTro.GiaThue), 18, 2)]
        [InlineData(typeof(HopDong), nameof(HopDong.TienCocPhong), 18, 2)]
        [InlineData(typeof(HopDong), nameof(HopDong.TienThuePhong), 18, 2)]
        [InlineData(typeof(HoaDon), nameof(HoaDon.TongTien), 18, 2)]
        [InlineData(typeof(ChiTietHoaDon), nameof(ChiTietHoaDon.DonGia), 18, 2)]
        [InlineData(typeof(ChiTietHoaDon), nameof(ChiTietHoaDon.TongTien), 18, 2)]
        [InlineData(typeof(ChiTietHoaDon), nameof(ChiTietHoaDon.SoLuong), 18, 3)]
        [InlineData(typeof(DichVuChiNhanh), nameof(DichVuChiNhanh.GiaDichVu), 18, 2)]
        [InlineData(typeof(DichVuDienNuocCuaPhong), nameof(DichVuDienNuocCuaPhong.DonGiaDien), 18, 2)]
        [InlineData(typeof(DichVuDienNuocCuaPhong), nameof(DichVuDienNuocCuaPhong.DonGiaNuoc), 18, 2)]
        [InlineData(typeof(DichVuDienNuocCuaPhong), nameof(DichVuDienNuocCuaPhong.ChiSoDienCu), 18, 3)]
        [InlineData(typeof(DichVuDienNuocCuaPhong), nameof(DichVuDienNuocCuaPhong.ChiSoDienMoi), 18, 3)]
        [InlineData(typeof(DichVuDienNuocCuaPhong), nameof(DichVuDienNuocCuaPhong.ChiSoNuocCu), 18, 3)]
        [InlineData(typeof(DichVuDienNuocCuaPhong), nameof(DichVuDienNuocCuaPhong.ChiSoNuocMoi), 18, 3)]
        [InlineData(typeof(LichSuThanhToan), nameof(LichSuThanhToan.SoTienThanhToan), 18, 2)]
        [InlineData(typeof(YeuCauSuCo), nameof(YeuCauSuCo.ChiPhiSuaChua), 18, 2)]
        public void Property_ShouldHaveExpectedPrecisionAndScale(Type entityType, string propertyName, int expectedPrecision, int expectedScale)
        {
            var entity = _context.Model.FindEntityType(entityType);
            Assert.NotNull(entity);

            var property = entity.FindProperty(propertyName);
            Assert.NotNull(property);

            Assert.Equal(expectedPrecision, property.GetPrecision());
            Assert.Equal(expectedScale, property.GetScale());
        }
    }
}
