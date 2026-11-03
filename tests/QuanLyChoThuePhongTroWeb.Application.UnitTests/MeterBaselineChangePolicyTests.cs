using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterBaselineChangePolicyTests
    {
        private const string TheDoiDongHo = "[DOI_DONG_HO] Chỉ số cũ 4.520; số đầu 0; không có số cuối đồng hồ cũ; lý do: Thay";
        private const string TheQuayVong = "[QUAY_VONG] Chỉ số cũ 9.950; trần 10.000; phần trước mốc 50 kWh; lý do: Quay vòng";

        [Fact]
        public void NoPreviousPeriod_IsNotChanged_AndHasNoReminder()
        {
            Assert.False(MeterBaselineChangePolicy.DaDoiMoc(0m, null));
            Assert.Null(MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Dien, 0m, null, TheDoiDongHo));
        }

        [Fact]
        public void BaselineEqualToPreviousNew_IsNotChanged_AndHasNoReminder()
        {
            Assert.False(MeterBaselineChangePolicy.DaDoiMoc(4520m, 4520m));
            Assert.Null(MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Nuoc, 4520m, 4520m, null));
        }

        [Fact]
        public void ChangedBaseline_WithSwapTag_MentionsMeterSwapAndNote()
        {
            Assert.True(MeterBaselineChangePolicy.DaDoiMoc(0m, 4520m));

            var text = MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Dien, 0m, 4520m, TheDoiDongHo);

            Assert.Equal($"Kỳ này có đổi đồng hồ điện: {TheDoiDongHo}. Kiểm tra đã thêm dòng truy thu nếu cần thu.", text);
        }

        [Fact]
        public void ChangedBaseline_WithRolloverTag_SaysRollover()
        {
            var text = MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Nuoc, 0m, 9950m, TheQuayVong);

            Assert.Equal($"Kỳ này có quay vòng nước: {TheQuayVong}. Kiểm tra đã thêm dòng truy thu nếu cần thu.", text);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("[Sửa 150 → 180] Nhập nhầm")]
        public void ChangedBaseline_WithoutTag_UsesGenericSentence(string? note)
        {
            var text = MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Dien, 0m, 4520m, note);

            Assert.Equal("Kỳ này có đổi đồng hồ hoặc quay vòng điện (không còn ghi chú xác nhận). Kiểm tra đã thêm dòng truy thu nếu cần thu.", text);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("[Sửa 150 → 180] Nhập nhầm")]
        public void ChangedBaseline_LostNote_Water_SaysWaterInGenericSentence(string? note)
        {
            var text = MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Nuoc, 0m, 9990m, note);

            Assert.Equal("Kỳ này có đổi đồng hồ hoặc quay vòng nước (không còn ghi chú xác nhận). Kiểm tra đã thêm dòng truy thu nếu cần thu.", text);
        }

        [Fact]
        public void ChangedBaseline_TagNotAtStartOfNote_IsNotTrusted_UsesGenericSentence()
        {
            // Ghi chú bị ghi đè kiểu "[Sửa a → b] ... [DOI_DONG_HO] ..." không còn là ghi chú gốc của thao tác đổi mốc.
            var text = MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Dien, 0m, 4520m, "[Sửa 80 → 85,5] Đọc lại [DOI_DONG_HO]");

            Assert.Contains("(không còn ghi chú xác nhận)", text);
        }

        [Fact]
        public void ChangedBaseline_NoteWithSurroundingSpaces_IsTrimmedAndStillRecognized()
        {
            var text = MeterBaselineChangePolicy.BuildReminder(LoaiDongHo.Dien, 0m, 4520m, "  " + TheDoiDongHo + "  ");

            Assert.Equal($"Kỳ này có đổi đồng hồ điện: {TheDoiDongHo}. Kiểm tra đã thêm dòng truy thu nếu cần thu.", text);
        }

        [Theory]
        [InlineData(85.5, 85.50)]
        [InlineData(0, 0)]
        public void SameValueDifferentScale_IsNotChanged(double cu, double moiKyTruoc)
        {
            Assert.False(MeterBaselineChangePolicy.DaDoiMoc((decimal)cu, (decimal)moiKyTruoc));
        }

        [Fact]
        public void BaselineOffByThousandth_IsChanged()
        {
            Assert.True(MeterBaselineChangePolicy.DaDoiMoc(85.5m, 85.501m));
        }

        [Theory]
        [InlineData("Truy thu điện đồng hồ cũ")]
        [InlineData("Truy thu nước đồng hồ cũ")]
        [InlineData("Truy thu quay vòng điện")]
        public void TruyThuLines_AreClassifiedAsOther_SoDraftRecalculationKeepsThem(string name)
        {
            Assert.Equal(InvoiceLineKind.Khac, InvoiceMeterLine.Classify(null, name));
        }
    }
}
