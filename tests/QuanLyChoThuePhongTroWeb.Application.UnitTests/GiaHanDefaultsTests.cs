using System;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class GiaHanDefaultsTests
    {
        // 01/11/2025 00:00 VN
        private static readonly DateTime Start12Months = new(2025, 10, 31, 17, 0, 0, DateTimeKind.Utc);
        // 31/10/2026 23:59:59 VN
        private static readonly DateTime EndOct = new(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc);

        [Fact]
        public void BeforeExpiry_StartsNextMonth_AndKeepsContractLength()
        {
            var now = new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc);

            var r = GiaHanDefaults.Tinh(Start12Months, EndOct, now);

            Assert.Equal((11, 2026), (r.Thang, r.Nam));
            Assert.Equal(new DateOnly(2027, 10, 31), r.NgayKetThuc);
        }

        [Fact]
        public void AlreadyExpired_StartsMonthAfterCurrentMonth()
        {
            var now = new DateTime(2026, 11, 4, 3, 0, 0, DateTimeKind.Utc);

            var r = GiaHanDefaults.Tinh(Start12Months, EndOct, now);

            Assert.Equal((12, 2026), (r.Thang, r.Nam));
            Assert.Equal(new DateOnly(2027, 11, 30), r.NgayKetThuc);
        }

        [Fact]
        public void DecemberWraps_ToNextYear()
        {
            var end = new DateTime(2026, 12, 31, 16, 59, 59, DateTimeKind.Utc);

            var r = GiaHanDefaults.Tinh(new DateTime(2026, 6, 30, 17, 0, 0, DateTimeKind.Utc), end, new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.Equal((1, 2027), (r.Thang, r.Nam));
            Assert.Equal(new DateOnly(2027, 6, 30), r.NgayKetThuc);
        }

        [Fact]
        public void NoEndDate_UsesNextMonthAndTwelveMonths()
        {
            var now = new DateTime(2026, 11, 4, 3, 0, 0, DateTimeKind.Utc);

            var r = GiaHanDefaults.Tinh(Start12Months, null, now);

            Assert.Equal((12, 2026), (r.Thang, r.Nam));
            Assert.Equal(new DateOnly(2027, 11, 30), r.NgayKetThuc);
        }

        [Fact]
        public void ShortContract_HasAtLeastOneMonth()
        {
            var start = new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 10, 10, 16, 59, 59, DateTimeKind.Utc);

            var r = GiaHanDefaults.Tinh(start, end, new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc));

            Assert.Equal((11, 2026), (r.Thang, r.Nam));
            Assert.Equal(new DateOnly(2026, 11, 30), r.NgayKetThuc);
        }

        [Fact]
        public void VietnamBoundaries_AreSeventeenHoursBefore()
        {
            Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), GiaHanDefaults.DauThangVnUtc(11, 2026));
            Assert.Equal(DateTimeKind.Utc, GiaHanDefaults.DauThangVnUtc(11, 2026).Kind);
            Assert.Equal(new DateTime(2026, 10, 31, 16, 59, 59, DateTimeKind.Utc), GiaHanDefaults.CuoiNgayVnUtc(new DateOnly(2026, 10, 31)));
        }
    }
}
