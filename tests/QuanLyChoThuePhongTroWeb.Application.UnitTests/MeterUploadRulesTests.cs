using System;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterUploadRulesTests
    {
        private readonly DateTime _utcNow = DateTime.SpecifyKind(DateTime.Parse("2026-10-15T10:00:00Z"), DateTimeKind.Utc); // 10/2026 VN

        [Fact]
        public void Evaluate_FuturePeriod_RejectedWithRule1()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(11, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Không thể gửi ảnh cho kỳ chưa tới.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_TenantOutsideUploadWindow_RejectedWithRule2()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(8, 2026),
                UtcNow: _utcNow,
                LaKhachThue: true,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Khách thuê chỉ được gửi ảnh cho tháng hiện tại hoặc tháng trước.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_StaffOutsideUploadWindow_NotRejectedByRule2()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(8, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.True(decision.DuocPhep);
            Assert.Null(decision.LyDo);
        }

        [Fact]
        public void Evaluate_SubsequentPeriodExists_RejectedWithRule3()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(9, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: true,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể tải thêm ảnh cho kỳ này.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_LockedInvoiceExists_RejectedWithRule4()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(10, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: false,
                CoHoaDonDaQuaNhap: true,
                KyDaDuyet: false,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Kỳ này đã có hóa đơn đã phát hành, không thể chỉnh sửa.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_TenantApprovedPeriod_RejectedWithRule5()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(10, 2026),
                UtcNow: _utcNow,
                LaKhachThue: true,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: true,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Kỳ này đã được chốt, không thể gửi thêm ảnh.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_StaffApprovedPeriod_AllowedByRule5()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(10, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: true,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.True(decision.DuocPhep);
            Assert.Null(decision.LyDo);
        }

        [Fact]
        public void Evaluate_PreviousPeriodNotApproved_WithContract_RejectedWithRule6()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(10, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: true,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Kỳ 09/2026 của phòng chưa được chốt, chưa thể gửi ảnh kỳ 10/2026.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_PreviousPeriodNotApproved_MonthOne_ChecksDecemberPreviousYear()
        {
            var janNow = DateTime.SpecifyKind(DateTime.Parse("2027-01-10T10:00:00Z"), DateTimeKind.Utc);
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(1, 2027),
                UtcNow: janNow,
                LaKhachThue: false,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: true,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Kỳ 12/2026 của phòng chưa được chốt, chưa thể gửi ảnh kỳ 01/2027.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_NoContractPreviousMonth_Rule6Skipped()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(10, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: false,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.True(decision.DuocPhep);
            Assert.Null(decision.LyDo);
        }

        [Fact]
        public void Evaluate_Priority_FutureBeatsSubsequentPeriod()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(11, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: true,
                CoHoaDonDaQuaNhap: true,
                KyDaDuyet: true,
                CoHopDongThangTruoc: true,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Không thể gửi ảnh cho kỳ chưa tới.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_Priority_SubsequentPeriodBeatsPreviousPeriodNotApproved()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(9, 2026),
                UtcNow: _utcNow,
                LaKhachThue: false,
                CoKySau: true,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: true,
                KyTruocDaDuyet: false);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.False(decision.DuocPhep);
            Assert.Equal("Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể tải thêm ảnh cho kỳ này.", decision.LyDo);
        }

        [Fact]
        public void Evaluate_AllPass_Allowed()
        {
            var facts = new MeterUploadFacts(
                Ky: new MeterPeriod(10, 2026),
                UtcNow: _utcNow,
                LaKhachThue: true,
                CoKySau: false,
                CoHoaDonDaQuaNhap: false,
                KyDaDuyet: false,
                CoHopDongThangTruoc: true,
                KyTruocDaDuyet: true);

            var decision = MeterUploadRules.Evaluate(facts);

            Assert.True(decision.DuocPhep);
            Assert.Null(decision.LyDo);
        }
    }
}
