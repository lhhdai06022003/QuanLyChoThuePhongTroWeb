namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices
{
    // Schema không có cột đánh dấu thao tác tự động, nên lịch sử hóa đơn ghi bằng thẻ văn bản cố định.
    public static class AutoInvoiceHistoryTags
    {
        public const string TuDong = "[TU_DONG]";
        public const string DieuChinh = "[DIEU_CHINH]";

        public static string TaoLyDo(string nhan, string noiDung) => $"{nhan} {noiDung.Trim()}";

        // Lý do hiển thị cho người dùng (ví dụ lý do hủy ở cổng khách): bỏ thẻ nội bộ ở đầu chuỗi.
        public static string BoNhan(string? lyDo)
        {
            var text = (lyDo ?? string.Empty).Trim();
            foreach (var nhan in new[] { TuDong, DieuChinh })
            {
                if (text.StartsWith(nhan, System.StringComparison.Ordinal))
                {
                    return text.Substring(nhan.Length).TrimStart();
                }
            }

            return text;
        }
    }
}
