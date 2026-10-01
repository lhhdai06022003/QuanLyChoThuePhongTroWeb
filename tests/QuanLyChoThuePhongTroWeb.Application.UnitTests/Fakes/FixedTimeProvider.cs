using System;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }
}
