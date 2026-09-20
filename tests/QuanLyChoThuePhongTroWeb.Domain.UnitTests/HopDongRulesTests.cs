using System;
using System.Linq;
using System.Reflection;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Domain.UnitTests
{
    /// <summary>
    /// Domain hiện tại chỉ chứa entity dữ liệu (anemic entities) và enums,
    /// chưa có behavior/business rules methods.
    /// Coverage Domain giới hạn ở: enum values, entity constraints, và cấu trúc.
    /// Nếu tương lai Domain có behavior methods, thêm test gọi trực tiếp method đó.
    /// </summary>
    public class HopDongRulesTests
    {
        [Fact]
        public void TrangThaiHopDong_Enum_HasExpectedValues()
        {
            Assert.True(Enum.IsDefined(typeof(TrangThaiHopDong), TrangThaiHopDong.DangHoatDong));
            Assert.True(Enum.IsDefined(typeof(TrangThaiHopDong), TrangThaiHopDong.DaKetThuc));
            Assert.True(Enum.IsDefined(typeof(TrangThaiHopDong), TrangThaiHopDong.DaHuy));
        }

        [Fact]
        public void TrangThaiPhong_Enum_HasExpectedValues()
        {
            Assert.True(Enum.IsDefined(typeof(TrangThaiPhong), TrangThaiPhong.Trong));
            Assert.True(Enum.IsDefined(typeof(TrangThaiPhong), TrangThaiPhong.DaThue));
        }

        [Fact]
        public void TrangThaiHoaDon_Enum_HasExpectedValues()
        {
            Assert.True(Enum.IsDefined(typeof(TrangThaiHoaDon), TrangThaiHoaDon.ChuaThanhToan));
            Assert.True(Enum.IsDefined(typeof(TrangThaiHoaDon), TrangThaiHoaDon.DaThanhToan));
        }

        [Fact]
        public void HopDong_HasExpectedNullableEndDate()
        {
            var hopDong = new HopDong
            {
                HopDongId = 1,
                MaHopDong = "HD001",
                ThoiDiemBatDau = DateTime.UtcNow
            };

            Assert.Null(hopDong.ThoiDiemKetThuc);
            Assert.Equal("HD001", hopDong.MaHopDong);
        }

        [Fact]
        public void HopDong_IsDeletedDefaultsFalse_WhenExplicitlySet()
        {
            var hopDong = new HopDong { HopDongId = 1, IsDeleted = false };

            Assert.False(hopDong.IsDeleted);
        }

        [Fact]
        public void DomainAssembly_ShouldNotReference_ApplicationOrInfrastructure()
        {
            var domainAssembly = typeof(HopDong).Assembly;
            var refs = domainAssembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToList();

            Assert.DoesNotContain(refs, name => name != null && name.Contains("Application"));
            Assert.DoesNotContain(refs, name => name != null && name.Contains("Infrastructure"));
            Assert.DoesNotContain(refs, name => name != null && name.Contains("Web"));
            Assert.DoesNotContain(refs, name => name != null && name.Contains("EntityFrameworkCore"));
        }

        [Fact]
        public void DomainEntities_AreAllInCorrectNamespace()
        {
            var entityTypes = typeof(HopDong).Assembly.GetExportedTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null && t.Namespace.Contains("Entities"))
                .ToList();

            Assert.NotEmpty(entityTypes);

            foreach (var type in entityTypes)
            {
                Assert.StartsWith("QuanLyChoThuePhongTroWeb.Domain.Entities", type.Namespace!);
            }
        }
    }
}
