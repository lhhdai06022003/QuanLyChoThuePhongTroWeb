namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices
{
    // Schema không có cột đánh dấu thao tác tự động, nên lịch sử hóa đơn ghi bằng thẻ văn bản cố định.
    public static class AutoInvoiceHistoryTags
    {
        public const string TuDong = "[TU_DONG]";
        public const string DieuChinh = "[DIEU_CHINH]";

        public static string TaoLyDo(string nhan, string noiDung) => $"{nhan} {noiDung.Trim()}";
    }
}
