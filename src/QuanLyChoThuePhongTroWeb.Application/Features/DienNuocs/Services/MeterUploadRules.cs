using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public sealed record MeterUploadFacts(
        MeterPeriod Ky,
        DateTime UtcNow,
        bool LaKhachThue,
        bool CoKySau,
        bool CoHoaDonDaQuaNhap,
        bool KyDaDuyet,
        bool CoHopDongThangTruoc,
        bool KyTruocDaDuyet);

    public sealed record MeterUploadDecision(bool DuocPhep, string? LyDo);

    public static class MeterUploadRules
    {
        public const string ReasonRule1Future = "Không thể gửi ảnh cho kỳ chưa tới.";
        public const string ReasonRule2TenantWindow = "Khách thuê chỉ được gửi ảnh cho tháng hiện tại hoặc tháng trước.";
        public const string ReasonRule3SubsequentPeriod = "Kỳ sau đã được chốt hoặc đã có hóa đơn phát hành, không thể tải thêm ảnh cho kỳ này.";
        public const string ReasonRule4LockedInvoice = "Kỳ này đã có hóa đơn đã phát hành, không thể chỉnh sửa.";
        public const string ReasonRule5PeriodApproved = "Kỳ này đã được chốt, không thể gửi thêm ảnh.";
        public static string FormatPreviousPeriodNotApprovedReason(int prevThang, int prevNam, int currThang, int currNam)
            => $"Kỳ {prevThang:00}/{prevNam} của phòng chưa được chốt, chưa thể gửi ảnh kỳ {currThang:00}/{currNam}.";

        public static MeterUploadDecision Evaluate(MeterUploadFacts f)
        {
            if (MeterPeriodPolicy.IsFuture(f.Ky, f.UtcNow))
            {
                return new MeterUploadDecision(false, ReasonRule1Future);
            }

            if (f.LaKhachThue && !MeterPeriodPolicy.IsTenantUploadWindow(f.Ky, f.UtcNow))
            {
                return new MeterUploadDecision(false, ReasonRule2TenantWindow);
            }

            if (f.CoKySau)
            {
                return new MeterUploadDecision(false, ReasonRule3SubsequentPeriod);
            }

            if (f.CoHoaDonDaQuaNhap)
            {
                return new MeterUploadDecision(false, ReasonRule4LockedInvoice);
            }

            if (f.LaKhachThue && f.KyDaDuyet)
            {
                return new MeterUploadDecision(false, ReasonRule5PeriodApproved);
            }

            if (f.CoHopDongThangTruoc && !f.KyTruocDaDuyet)
            {
                var prev = MeterPeriodPolicy.Previous(f.Ky);
                return new MeterUploadDecision(false, FormatPreviousPeriodNotApprovedReason(prev.Thang, prev.Nam, f.Ky.Thang, f.Ky.Nam));
            }

            return new MeterUploadDecision(true, null);
        }
    }
}
