using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public sealed record GiaHanMacDinh(int Thang, int Nam, DateOnly NgayKetThuc);

    public static class GiaHanDefaults
    {
        private const int OffsetGio = 7;

        // Tháng bắt đầu: tháng sau tháng kết thúc cũ (giờ VN), nhưng không sớm hơn tháng sau tháng hiện tại khi đã quá hạn.
        // Ngày kết thúc mặc định = ngày bắt đầu mới + N tháng - 1 ngày, N = số tháng của hợp đồng cũ (tối thiểu 1; không có ngày kết thúc thì 12).
        public static GiaHanMacDinh Tinh(DateTime oldStartUtc, DateTime? oldEndUtc, DateTime nowUtc)
        {
            var nowVn = nowUtc.AddHours(OffsetGio);
            var nowIndex = nowVn.Year * 12 + nowVn.Month - 1;
            int startIndex;
            int soThang;

            if (oldEndUtc == null)
            {
                startIndex = nowIndex + 1;
                soThang = 12;
            }
            else
            {
                var endVn = oldEndUtc.Value.AddHours(OffsetGio);
                var endIndex = endVn.Year * 12 + endVn.Month - 1;
                startIndex = Math.Max(endIndex, nowIndex) + 1;

                var endPlusOne = endVn.AddDays(1);
                var startVn = oldStartUtc.AddHours(OffsetGio);
                soThang = Math.Max(1, (endPlusOne.Year * 12 + endPlusOne.Month - 1) - (startVn.Year * 12 + startVn.Month - 1));
            }

            var nam = startIndex / 12;
            var thang = startIndex % 12 + 1;
            var ngayKetThuc = new DateOnly(nam, thang, 1).AddMonths(soThang).AddDays(-1);
            return new GiaHanMacDinh(thang, nam, ngayKetThuc);
        }

        // 00:00 ngày 1 của tháng theo giờ VN, tính bằng UTC.
        public static DateTime DauThangVnUtc(int thang, int nam) =>
            DateTime.SpecifyKind(new DateTime(nam, thang, 1), DateTimeKind.Utc).AddHours(-OffsetGio);

        // 23:59:59 của ngày theo giờ VN, tính bằng UTC.
        public static DateTime CuoiNgayVnUtc(DateOnly ngay) =>
            DateTime.SpecifyKind(ngay.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Utc).AddHours(-OffsetGio);
    }
}
