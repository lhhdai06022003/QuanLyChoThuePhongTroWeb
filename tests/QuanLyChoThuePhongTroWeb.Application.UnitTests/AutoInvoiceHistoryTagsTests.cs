using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AutoInvoiceHistoryTagsTests
    {
        [Theory]
        [InlineData("[DIEU_CHINH] Điều chỉnh hóa đơn", "Điều chỉnh hóa đơn")]
        [InlineData("[TU_DONG] Tạo hóa đơn nháp tự động", "Tạo hóa đơn nháp tự động")]
        [InlineData("  [DIEU_CHINH]   Sai chỉ số ", "Sai chỉ số")]
        [InlineData("Khách trả phòng", "Khách trả phòng")]
        [InlineData("Lý do có [DIEU_CHINH] ở giữa", "Lý do có [DIEU_CHINH] ở giữa")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void BoNhan_RemovesLeadingInternalTagOnly(string? lyDo, string expected)
        {
            Assert.Equal(expected, AutoInvoiceHistoryTags.BoNhan(lyDo));
        }
    }
}
