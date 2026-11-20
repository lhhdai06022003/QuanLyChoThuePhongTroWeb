using System;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.BackgroundJobs
{
    // Đồng hồ đứng yên ở 07:59:59.8 giờ VN: job nền chờ khoảng 200 ms rồi chạy lượt 8:00, sau đó chờ tới hôm sau.
    // Bộ hẹn giờ vẫn là của hệ thống (lớp gốc TimeProvider), nên test không phải chờ tới 8 giờ thật.
    internal sealed class SapToiGioChayTimeProvider : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 59, 59, 800, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
