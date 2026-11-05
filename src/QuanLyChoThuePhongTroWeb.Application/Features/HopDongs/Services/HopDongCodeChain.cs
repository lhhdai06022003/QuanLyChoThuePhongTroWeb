using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    // Mã hợp đồng gia hạn có dạng {mã gốc}-GH{n}. Mã gốc không bao giờ lồng nhau (service luôn gia hạn từ mã gốc).
    public static class HopDongCodeChain
    {
        // Dùng chung cho Regex.IsMatch dịch sang SQL (Npgsql).
        public const string RenewalPattern = "-GH[1-9][0-9]*$";

        private static readonly Regex RenewalRegex = new(@"^(?<goc>.+)-GH(?<n>[1-9][0-9]*)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool IsRenewal(string maHopDong) => TryParse(maHopDong, out _, out _);

        public static string RootOf(string maHopDong) => TryParse(maHopDong, out var goc, out _) ? goc : maHopDong;

        public static int RenewalIndex(string maHopDong) => TryParse(maHopDong, out _, out var n) ? n : 0;

        public static string BuildRenewal(string maGoc, int n)
        {
            if (n < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "Số thứ tự gia hạn phải từ 1.");
            }

            return $"{maGoc}-GH{n}";
        }

        public static string LoaiText(string maHopDong) => TryParse(maHopDong, out _, out var n) ? $"Gia hạn lần {n}" : "Mới";

        public static bool BelongsTo(string maHopDong, string maGoc) => RootOf(maHopDong) == maGoc;

        private static bool TryParse(string? maHopDong, out string goc, out int n)
        {
            goc = maHopDong ?? string.Empty;
            n = 0;
            if (string.IsNullOrEmpty(maHopDong))
            {
                return false;
            }

            var match = RenewalRegex.Match(maHopDong);
            if (!match.Success || !int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out n))
            {
                n = 0;
                return false;
            }

            goc = match.Groups["goc"].Value;
            return true;
        }
    }
}
