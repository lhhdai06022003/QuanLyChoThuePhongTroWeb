using System;
using QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    // Lịch 8:00 giờ VN (01:00 UTC) mỗi ngày: không có sổ "đã gửi" nên mỗi ngày phải chạy đúng một lượt.
    public class DailyJobScheduleTests
    {
        private static DateTimeOffset Utc(int day, int hour, int minute = 0, int second = 0) =>
            new(2026, 11, day, hour, minute, second, TimeSpan.Zero);

        [Theory]
        [InlineData(5, 0, 30, 5)]   // 07:30 VN: chạy 8:00 cùng ngày
        [InlineData(5, 1, 0, 6)]    // đúng 08:00 VN lúc khởi động: không chạy ngay, chờ hôm sau
        [InlineData(5, 3, 0, 6)]    // 10:00 VN: hôm sau
        [InlineData(4, 18, 0, 5)]   // 01:00 VN ngày 5/11 (18:00 UTC ngày 4/11): 8:00 sáng 5/11
        public void NextRunUtc_IsNextEightAmVietnam(int day, int hour, int minute, int expectedDay)
        {
            var next = DailyJobSchedule.NextRunUtc(Utc(day, hour, minute));

            Assert.Equal(Utc(expectedDay, 1), next);
        }

        [Fact]
        public void NextRunUtc_AfterRun_NeverReturnsSameRunAgain()
        {
            // Bộ hẹn giờ đánh thức sớm vài mili giây trước 8:00: vẫn không được chạy lượt 8:00 lần nữa.
            var lanTruoc = Utc(5, 1);
            var next = DailyJobSchedule.NextRunUtc(lanTruoc.AddMilliseconds(-5), lanTruoc);

            Assert.Equal(Utc(6, 1), next);
        }

        [Theory]
        [InlineData(1, 0, true)]    // 08:00 lỗi: thử lại lúc 08:10
        [InlineData(2, 50, true)]   // 09:50: lần thử 10:00 vẫn trong hạn
        [InlineData(2, 51, false)]  // 09:51: lần thử 10:01 quá hạn, bỏ lượt
        public void RetryDelay_StopsAtTenAm(int hour, int minute, bool expectRetry)
        {
            var retry = DailyJobSchedule.RetryDelay(Utc(5, 1), Utc(5, hour, minute));

            Assert.Equal(expectRetry ? DailyJobSchedule.KhoangThuLai : null, retry);
        }

        [Fact]
        public void DelayUntil_PastTarget_IsZero()
        {
            Assert.Equal(TimeSpan.Zero, DailyJobSchedule.DelayUntil(Utc(5, 1), Utc(5, 2)));
        }
    }
}
