using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public readonly record struct MeterPeriod(int Thang, int Nam);

    public static class MeterPeriodPolicy
    {
        public static MeterPeriod CurrentVn(DateTime utcNow)
        {
            EnsureUtc(utcNow);
            var vnTime = utcNow.AddHours(7);
            return new MeterPeriod(vnTime.Month, vnTime.Year);
        }

        public static MeterPeriod Previous(MeterPeriod p)
        {
            if (p.Thang <= 1)
            {
                return new MeterPeriod(12, p.Nam - 1);
            }
            return new MeterPeriod(p.Thang - 1, p.Nam);
        }

        public static int Compare(MeterPeriod a, MeterPeriod b)
        {
            if (a.Nam != b.Nam)
            {
                return a.Nam.CompareTo(b.Nam);
            }
            return a.Thang.CompareTo(b.Thang);
        }

        public static bool IsFuture(MeterPeriod p, DateTime utcNow)
        {
            EnsureUtc(utcNow);
            var current = CurrentVn(utcNow);
            return Compare(p, current) > 0;
        }

        public static bool IsTenantUploadWindow(MeterPeriod p, DateTime utcNow)
        {
            EnsureUtc(utcNow);
            var current = CurrentVn(utcNow);
            var previous = Previous(current);
            return p == current || p == previous;
        }

        public static (DateTime StartUtc, DateTime EndExclusiveUtc) MonthRangeUtc(MeterPeriod p)
        {
            var startVn = new DateTime(p.Nam, p.Thang, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var startUtc = DateTime.SpecifyKind(startVn.AddHours(-7), DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(startVn.AddMonths(1).AddHours(-7), DateTimeKind.Utc);
            return (startUtc, endUtc);
        }

        public static IReadOnlyList<MeterPeriod> RecentPeriods(int count, DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            EnsureUtc(now);
            if (count <= 0)
            {
                return Array.Empty<MeterPeriod>();
            }
            var list = new System.Collections.Generic.List<MeterPeriod>(count);
            var cur = CurrentVn(now);
            for (int i = 0; i < count; i++)
            {
                list.Add(cur);
                cur = Previous(cur);
            }
            return list;
        }

        private static void EnsureUtc(DateTime dt)
        {
            if (dt.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("DateTime must have DateTimeKind.Utc", nameof(dt));
            }
        }
    }
}
