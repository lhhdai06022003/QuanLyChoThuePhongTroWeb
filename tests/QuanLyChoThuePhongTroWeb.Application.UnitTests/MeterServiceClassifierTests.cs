using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterServiceClassifierTests
    {
        [Theory]
        [InlineData(LoaiDichVu.Dien, "Bất kỳ", "", true)]
        [InlineData(LoaiDichVu.Nuoc, "Bất kỳ", "", true)]
        [InlineData(LoaiDichVu.Khac, "Điện", "kWh", true)]
        [InlineData(LoaiDichVu.Khac, "  NƯỚC ", "m3", true)]
        [InlineData(LoaiDichVu.Khac, "Tiền điện", "", true)]
        [InlineData(LoaiDichVu.Khac, "Tiền nước", null, true)]
        [InlineData(LoaiDichVu.Khac, "Dịch vụ khác", "kWh", true)]
        [InlineData(LoaiDichVu.Khac, "Dịch vụ khác", "m³", true)]
        [InlineData(LoaiDichVu.Khac, "Đổ rác", "tháng", false)]
        [InlineData(LoaiDichVu.Khac, "Internet", "tháng", false)]
        [InlineData(LoaiDichVu.Khac, "Điện thoại cố định", "tháng", false)]
        [InlineData(LoaiDichVu.Khac, "Nước uống", "bình", false)]
        [InlineData(LoaiDichVu.Khac, null, null, false)]
        public void IsMeterBased_ClassifiesServices(LoaiDichVu loai, string? ten, string? donVi, bool expected)
        {
            Assert.Equal(expected, MeterServiceClassifier.IsMeterBased(loai, ten, donVi));
        }
    }
}
