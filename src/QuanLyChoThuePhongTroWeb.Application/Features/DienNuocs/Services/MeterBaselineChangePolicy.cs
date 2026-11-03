using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    // Nhận biết kỳ đã đổi đồng hồ hoặc quay vòng và dựng câu nhắc truy thu (thuần, không I/O).
    public static class MeterBaselineChangePolicy
    {
        private const string TheDoiDongHo = "[DOI_DONG_HO]";
        private const string TheQuayVong = "[QUAY_VONG]";

        // Đã đổi mốc khi Cũ của kỳ khác Mới của kỳ trước. Kỳ đầu tiên (chưa có kỳ trước) thì không tính.
        public static bool DaDoiMoc(decimal cu, decimal? moiKyTruoc)
            => moiKyTruoc.HasValue && cu != moiKyTruoc.Value;

        // null khi kỳ không đổi mốc.
        public static string? BuildReminder(LoaiDongHo loai, decimal cu, decimal? moiKyTruoc, string? ghiChuXacNhan)
        {
            if (!DaDoiMoc(cu, moiKyTruoc))
            {
                return null;
            }

            var ten = loai == LoaiDongHo.Dien ? "điện" : "nước";
            var ghiChu = ghiChuXacNhan?.Trim();
            var coThe = !string.IsNullOrEmpty(ghiChu)
                && (ghiChu.StartsWith(TheDoiDongHo) || ghiChu.StartsWith(TheQuayVong));

            if (!coThe)
            {
                return $"Kỳ này có đổi đồng hồ hoặc quay vòng {ten} (không còn ghi chú xác nhận). Kiểm tra đã thêm dòng truy thu nếu cần thu.";
            }

            var hanhDong = ghiChu!.StartsWith(TheQuayVong) ? "quay vòng" : "đổi đồng hồ";
            return $"Kỳ này có {hanhDong} {ten}: {ghiChu}. Kiểm tra đã thêm dòng truy thu nếu cần thu.";
        }
    }
}
