using System;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterPeriodPolicyTests
    {
        [Fact]
        public void CurrentVn_BeforeBoundary_ReturnsPreviousMonth()
        {
            var utcNow = new DateTime(2026, 9, 30, 16, 59, 59, DateTimeKind.Utc);
            var period = MeterPeriodPolicy.CurrentVn(utcNow);
            Assert.Equal(9, period.Thang);
            Assert.Equal(2026, period.Nam);
        }

        [Fact]
        public void CurrentVn_AtBoundary_ReturnsNewMonth()
        {
            var utcNow = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);
            var period = MeterPeriodPolicy.CurrentVn(utcNow);
            Assert.Equal(10, period.Thang);
            Assert.Equal(2026, period.Nam);
        }

        [Fact]
        public void CurrentVn_YearBoundary_ReturnsNewYear()
        {
            var utcNow = new DateTime(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc);
            var period = MeterPeriodPolicy.CurrentVn(utcNow);
            Assert.Equal(1, period.Thang);
            Assert.Equal(2027, period.Nam);
        }

        [Fact]
        public void Previous_MonthOne_ReturnsMonthTwelvePreviousYear()
        {
            var p = new MeterPeriod(1, 2026);
            var prev = MeterPeriodPolicy.Previous(p);
            Assert.Equal(12, prev.Thang);
            Assert.Equal(2025, prev.Nam);
        }

        [Fact]
        public void Previous_MidYear_ReturnsPreviousMonth()
        {
            var p = new MeterPeriod(10, 2026);
            var prev = MeterPeriodPolicy.Previous(p);
            Assert.Equal(9, prev.Thang);
            Assert.Equal(2026, prev.Nam);
        }

        [Fact]
        public void MonthRangeUtc_October2026_ReturnsExpectedBoundaries()
        {
            var p = new MeterPeriod(10, 2026);
            var (startUtc, endExclusiveUtc) = MeterPeriodPolicy.MonthRangeUtc(p);

            var expectedStart = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);
            var expectedEnd = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc);

            Assert.Equal(expectedStart, startUtc);
            Assert.Equal(expectedEnd, endExclusiveUtc);
            Assert.Equal(DateTimeKind.Utc, startUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, endExclusiveUtc.Kind);
        }

        [Fact]
        public void IsTenantUploadWindow_CurrentAndPreviousMonth_ReturnsTrue()
        {
            var utcNow = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc); // 10/2026 VN
            Assert.True(MeterPeriodPolicy.IsTenantUploadWindow(new MeterPeriod(10, 2026), utcNow));
            Assert.True(MeterPeriodPolicy.IsTenantUploadWindow(new MeterPeriod(9, 2026), utcNow));
        }

        [Fact]
        public void IsTenantUploadWindow_TwoMonthsAgoOrFuture_ReturnsFalse()
        {
            var utcNow = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc); // 10/2026 VN
            Assert.False(MeterPeriodPolicy.IsTenantUploadWindow(new MeterPeriod(8, 2026), utcNow));
            Assert.False(MeterPeriodPolicy.IsTenantUploadWindow(new MeterPeriod(11, 2026), utcNow));
        }

        [Fact]
        public void NonUtcKind_ThrowsArgumentException()
        {
            var localNow = DateTime.SpecifyKind(DateTime.Parse("2026-10-15T10:00:00"), DateTimeKind.Local);
            var unspecifiedNow = DateTime.SpecifyKind(DateTime.Parse("2026-10-15T10:00:00"), DateTimeKind.Unspecified);

            Assert.Throws<ArgumentException>(() => MeterPeriodPolicy.CurrentVn(localNow));
            Assert.Throws<ArgumentException>(() => MeterPeriodPolicy.CurrentVn(unspecifiedNow));
            Assert.Throws<ArgumentException>(() => MeterPeriodPolicy.IsFuture(new MeterPeriod(10, 2026), localNow));
            Assert.Throws<ArgumentException>(() => MeterPeriodPolicy.IsTenantUploadWindow(new MeterPeriod(10, 2026), unspecifiedNow));
        }

        [Fact]
        public void Compare_WorksCorrectly()
        {
            var p1 = new MeterPeriod(9, 2026);
            var p2 = new MeterPeriod(10, 2026);
            var p3 = new MeterPeriod(1, 2027);

            Assert.True(MeterPeriodPolicy.Compare(p1, p2) < 0);
            Assert.True(MeterPeriodPolicy.Compare(p2, p1) > 0);
            Assert.Equal(0, MeterPeriodPolicy.Compare(p1, new MeterPeriod(9, 2026)));
            Assert.True(MeterPeriodPolicy.Compare(p2, p3) < 0);
        }

        [Fact]
        public void RecentPeriods_ReturnsDescendingPeriods()
        {
            var utcNow = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc);
            var periods = MeterPeriodPolicy.RecentPeriods(3, utcNow);

            Assert.Equal(3, periods.Count);
            Assert.Equal(new MeterPeriod(10, 2026), periods[0]);
            Assert.Equal(new MeterPeriod(9, 2026), periods[1]);
            Assert.Equal(new MeterPeriod(8, 2026), periods[2]);
        }
    }
}
