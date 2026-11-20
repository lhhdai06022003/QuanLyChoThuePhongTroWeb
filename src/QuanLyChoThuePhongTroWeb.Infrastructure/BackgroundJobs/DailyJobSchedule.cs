using System;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    // Lịch chạy của job nhắc hóa đơn và job cảnh báo hợp đồng: mỗi ngày đúng một lượt lúc 8:00 giờ Việt Nam,
    // không chạy ngay khi web khởi động. Nhờ vậy deploy hay khởi động lại không gửi trùng mà không cần sổ "đã gửi".
    // Lượt lỗi (DB, mạng) được thử lại mỗi 10 phút, tối đa tới 10:00; quá thì bỏ lượt hôm đó.
    public static class DailyJobSchedule
    {
        public static readonly TimeSpan GioChayVn = TimeSpan.FromHours(8);
        public static readonly TimeSpan KhoangThuLai = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan HanThuLai = TimeSpan.FromHours(2);

        private static readonly TimeSpan LechVn = TimeSpan.FromHours(7);

        // 8:00 giờ VN đầu tiên sau nowUtc. lanTruocUtc là lượt vừa chạy: không trả lại lượt đó hay lượt sớm hơn,
        // phòng khi bộ hẹn giờ đánh thức sớm vài mili giây khiến một ngày chạy hai lần.
        public static DateTimeOffset NextRunUtc(DateTimeOffset nowUtc, DateTimeOffset? lanTruocUtc = null)
        {
            var gioVn = nowUtc.UtcDateTime + LechVn;
            var lanChayVn = gioVn.Date + GioChayVn;
            if (lanChayVn <= gioVn)
            {
                lanChayVn = lanChayVn.AddDays(1);
            }

            var lanChay = new DateTimeOffset(DateTime.SpecifyKind(lanChayVn - LechVn, DateTimeKind.Utc));
            while (lanTruocUtc.HasValue && lanChay <= lanTruocUtc.Value)
            {
                lanChay = lanChay.AddDays(1);
            }

            return lanChay;
        }

        // Thời gian chờ trước lần thử lại; null khi lần thử kế tiếp sẽ quá hạn thử lại của lượt.
        public static TimeSpan? RetryDelay(DateTimeOffset lanChayUtc, DateTimeOffset nowUtc)
        {
            return nowUtc + KhoangThuLai <= lanChayUtc + HanThuLai ? KhoangThuLai : null;
        }

        public static TimeSpan DelayUntil(DateTimeOffset targetUtc, DateTimeOffset nowUtc)
        {
            var delay = targetUtc - nowUtc;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }
    }
}
