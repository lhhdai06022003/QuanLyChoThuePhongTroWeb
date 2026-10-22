using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments
{
    public sealed record PaymentNoteInfo(decimal? ChenhLech, decimal? TienThua, bool DaHuy, string NoiDung);

    // Schema không có cột cho chênh lệch, tiền thừa, "chờ Admin" và dấu vết hủy ghi nhận, nên các thông tin
    // này được ghi bằng thẻ văn bản cố định (spec §15, §16, §20, §34). Đây là nơi duy nhất định nghĩa định dạng;
    // truy vấn lọc ở Infrastructure dùng các hằng tiền tố bên dưới.
    public static class PaymentNoteTags
    {
        public const string ChenhLechPrefix = "[CHENH-LECH:";
        public const string TienThuaPrefix = "[TIEN-THUA:";
        public const string ChoAdminPrefix = "[CHO-ADMIN]";
        public const string DaHuyPrefix = "[DA-HUY ";

        private static readonly Regex ChenhLechRegex = new(@"\[CHENH-LECH:([+-]?\d+)\]\s*", RegexOptions.Compiled);
        private static readonly Regex TienThuaRegex = new(@"\[TIEN-THUA:(\d+)\]\s*", RegexOptions.Compiled);
        private static readonly Regex DaHuyRegex = new(@"\s*\|?\s*\[DA-HUY [^\]]*\].*$", RegexOptions.Compiled | RegexOptions.Singleline);

        public static string ChenhLech(decimal chenhLech, string ghiChu)
        {
            return $"{ChenhLechPrefix}{chenhLech.ToString("+0;-0;0", CultureInfo.InvariantCulture)}] {ghiChu.Trim()}";
        }

        public static string TienThua(decimal tienThua, string ghiChu)
        {
            return $"{TienThuaPrefix}{tienThua.ToString("0", CultureInfo.InvariantCulture)}] {ghiChu.Trim()}";
        }

        public static string ChoAdmin(decimal thucNhan, decimal conNo, string? ghiChu)
        {
            var text = $"{ChoAdminPrefix} Thực nhận {InvoicePaymentPolicy.FormatVnd(thucNhan)}, vượt số còn nợ {InvoicePaymentPolicy.FormatVnd(conNo)}.";
            return string.IsNullOrWhiteSpace(ghiChu) ? text : $"{text} {ghiChu.Trim()}";
        }

        // Nối dấu vết hủy ghi nhận vào ghi chú hiện có; giờ hiển thị theo giờ Việt Nam.
        public static string AppendDaHuy(string? ghiChu, DateTime nowUtc, int adminId, string lyDo)
        {
            var tag = $"{DaHuyPrefix}{nowUtc.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} bởi #{adminId}] {lyDo.Trim()}";
            return string.IsNullOrWhiteSpace(ghiChu) ? tag : $"{ghiChu.Trim()} | {tag}";
        }

        public static bool ContainsReservedTag(string? text)
        {
            return text != null
                && (text.Contains(ChenhLechPrefix, StringComparison.OrdinalIgnoreCase)
                    || text.Contains(TienThuaPrefix, StringComparison.OrdinalIgnoreCase)
                    || text.Contains(ChoAdminPrefix, StringComparison.OrdinalIgnoreCase)
                    || text.Contains(DaHuyPrefix, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsChoAdmin(string? lyDo)
        {
            return lyDo != null && lyDo.StartsWith(ChoAdminPrefix, StringComparison.Ordinal);
        }

        // Lượt "chờ Admin" khi dòng lịch sử mới nhất mang thẻ [CHO-ADMIN] (spec §16.1).
        public static bool IsWaitingAdmin(YeuCauThanhToanHoaDon yeuCau)
        {
            var latest = yeuCau.LichSuTrangThaiYeuCauThanhToanHoaDons
                .OrderByDescending(l => l.NgayThucHien)
                .ThenByDescending(l => l.LichSuTrangThaiYeuCauThanhToanHoaDonId)
                .FirstOrDefault();
            return IsChoAdmin(latest?.LyDo);
        }

        public static PaymentNoteInfo Parse(string? ghiChu)
        {
            if (string.IsNullOrWhiteSpace(ghiChu))
            {
                return new PaymentNoteInfo(null, null, false, string.Empty);
            }

            var text = ghiChu;
            var daHuy = DaHuyRegex.IsMatch(text);

            decimal? chenhLech = null;
            var chenhMatch = ChenhLechRegex.Match(text);
            if (chenhMatch.Success)
            {
                chenhLech = decimal.Parse(chenhMatch.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                text = ChenhLechRegex.Replace(text, string.Empty, 1);
            }

            decimal? tienThua = null;
            var thuaMatch = TienThuaRegex.Match(text);
            if (thuaMatch.Success)
            {
                tienThua = decimal.Parse(thuaMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                text = TienThuaRegex.Replace(text, string.Empty, 1);
            }

            return new PaymentNoteInfo(chenhLech, tienThua, daHuy, text.Trim());
        }

        // Ghi chú dạng đọc được cho màn không tách nhãn riêng (chi tiết hóa đơn, lịch sử của khách):
        // thay thẻ bằng nhãn, bỏ phần dấu vết hủy ghi nhận.
        public static string? ToDisplayText(string? ghiChu)
        {
            if (string.IsNullOrWhiteSpace(ghiChu))
            {
                return ghiChu;
            }

            var info = Parse(ghiChu);
            var noiDung = DaHuyRegex.Replace(info.NoiDung, string.Empty).Trim();
            var parts = Labels(info).ToList();
            if (noiDung.Length > 0)
            {
                parts.Add(noiDung);
            }

            return string.Join(" · ", parts);
        }

        public static IReadOnlyList<string> Labels(PaymentNoteInfo info)
        {
            var labels = new List<string>();
            if (info.ChenhLech.HasValue)
            {
                labels.Add(info.ChenhLech.Value < 0
                    ? $"Thiếu {InvoicePaymentPolicy.FormatVnd(-info.ChenhLech.Value)}đ so với lượt"
                    : $"Dư {InvoicePaymentPolicy.FormatVnd(info.ChenhLech.Value)}đ so với lượt");
            }

            if (info.TienThua.HasValue)
            {
                labels.Add($"Tiền thừa {InvoicePaymentPolicy.FormatVnd(info.TienThua.Value)}đ");
            }

            if (info.DaHuy)
            {
                labels.Add("Đã hủy ghi nhận");
            }

            return labels;
        }
    }
}
