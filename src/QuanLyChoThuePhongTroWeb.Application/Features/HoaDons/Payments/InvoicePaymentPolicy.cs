using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    public enum RecordingKind
    {
        // Thực nhận đúng số lượt (hoặc thu thủ công không vượt nợ).
        Exact,
        // Thực nhận khác số lượt nhưng không vượt số còn nợ (spec §15).
        Variance,
        // Admin ghi nhận khoản vượt số còn nợ: ghi đủ nợ, phần thừa đánh dấu thẻ (spec §16.2).
        Overpay,
        // Nhân viên gặp khoản vượt số còn nợ: phải chuyển Admin (spec §16.1).
        NeedsAdmin
    }

    // SoTienGhiNhan là số ghi vào ledger; ChenhLech là thực nhận − số lượt (Variance)
    // hoặc thực nhận − số còn nợ (Overpay, NeedsAdmin).
    public readonly record struct RecordingDecision(RecordingKind Kind, decimal SoTienGhiNhan, decimal ChenhLech);

    // Quy tắc số tiền thuần, không đọc DB (spec §9, §10, §13.2, §18).
    public static class InvoicePaymentPolicy
    {
        public const int MaxLyDoLength = 500;
        public const int MaxMaGiaoDichLength = 100;

        public static string? ValidateWholeAmount(decimal soTien, string tenTruong)
        {
            return soTien <= 0 || !InvoiceMoney.IsWholeVnd(soTien)
                ? $"{tenTruong} phải là số nguyên VND lớn hơn 0."
                : null;
        }

        // Số tiền khách được tạo cho lượt (spec §10, mục 3–6). soTienYeuCau null nghĩa là trả đúng số còn lại.
        public static (decimal SoTien, string? Error) ResolveRequestAmount(
            decimal conLai,
            bool choPhepMotPhan,
            decimal? soTienToiThieu,
            decimal? soTienYeuCau)
        {
            if (conLai <= 0)
            {
                return (0, "Hóa đơn không còn số tiền cần thanh toán.");
            }

            if (!soTienYeuCau.HasValue || soTienYeuCau.Value == conLai)
            {
                return (conLai, null);
            }

            var soTien = soTienYeuCau.Value;
            var amountError = ValidateWholeAmount(soTien, "Số tiền thanh toán");
            if (amountError != null)
            {
                return (0, amountError);
            }

            if (soTien > conLai)
            {
                return (0, $"Số tiền vượt số còn lại của hóa đơn ({FormatVnd(conLai)}đ).");
            }

            if (!choPhepMotPhan)
            {
                return (0, "Hóa đơn này không cho phép thanh toán một phần, vui lòng thanh toán đúng số còn lại.");
            }

            var toiThieu = soTienToiThieu ?? 0;
            if (conLai < toiThieu)
            {
                return (0, $"Số còn lại nhỏ hơn mức tối thiểu nên phải thanh toán đúng {FormatVnd(conLai)}đ.");
            }

            if (soTien < toiThieu)
            {
                return (0, $"Số tiền thanh toán tối thiểu là {FormatVnd(toiThieu)}đ.");
            }

            return (soTien, null);
        }

        // Bảng rẽ nhánh spec §13.2. soTienLuot null dùng cho thu thủ công (§18), khi đó không có chênh lệch.
        // Kiểm vượt nợ trước để không bao giờ ghi ledger vượt TongTien.
        public static RecordingDecision DecideRecording(decimal thucNhan, decimal? soTienLuot, decimal conLai, bool isAdmin)
        {
            if (thucNhan > conLai)
            {
                return isAdmin
                    ? new RecordingDecision(RecordingKind.Overpay, conLai, thucNhan - conLai)
                    : new RecordingDecision(RecordingKind.NeedsAdmin, 0, thucNhan - conLai);
            }

            if (!soTienLuot.HasValue || thucNhan == soTienLuot.Value)
            {
                return new RecordingDecision(RecordingKind.Exact, thucNhan, 0);
            }

            return new RecordingDecision(RecordingKind.Variance, thucNhan, thucNhan - soTienLuot.Value);
        }

        public static string? ValidateNotFuture(DateTime thoiDiemUtc, DateTime nowUtc, int dungSaiPhut, string tenTruong)
        {
            return thoiDiemUtc > nowUtc.AddMinutes(dungSaiPhut)
                ? $"{tenTruong} không được ở tương lai."
                : null;
        }

        public static string? ValidateLyDo(string? lyDo, string tenTruong)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                return $"{tenTruong} là bắt buộc.";
            }

            if (lyDo.Trim().Length > MaxLyDoLength)
            {
                return $"{tenTruong} tối đa {MaxLyDoLength} ký tự.";
            }

            return ValidateNoReservedTag(lyDo, tenTruong);
        }

        public static string? ValidateGhiChuTuyChon(string? ghiChu)
        {
            if (ghiChu != null && ghiChu.Trim().Length > MaxLyDoLength)
            {
                return $"Ghi chú tối đa {MaxLyDoLength} ký tự.";
            }

            return ValidateNoReservedTag(ghiChu, "Ghi chú");
        }

        // Thẻ hệ thống trong GhiChu/LyDo được dùng để lọc và gắn nhãn, nên người dùng không được tự gõ.
        private static string? ValidateNoReservedTag(string? text, string tenTruong)
        {
            return PaymentNoteTags.ContainsReservedTag(text)
                ? $"{tenTruong} không được chứa thẻ hệ thống như {PaymentNoteTags.TienThuaPrefix}...] hoặc {PaymentNoteTags.DaHuyPrefix}...]."
                : null;
        }

        // Cắt khoảng trắng; chuỗi rỗng thành null.
        public static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static string? ValidateMaGiaoDich(string? maGiaoDich)
        {
            return maGiaoDich != null && maGiaoDich.Length > MaxMaGiaoDichLength
                ? $"Mã giao dịch tối đa {MaxMaGiaoDichLength} ký tự."
                : null;
        }

        // Định dạng tiền kiểu Việt Nam (1.900.000), không phụ thuộc culture của máy chủ.
        public static string FormatVnd(decimal soTien)
        {
            return soTien.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        }
    }
}
