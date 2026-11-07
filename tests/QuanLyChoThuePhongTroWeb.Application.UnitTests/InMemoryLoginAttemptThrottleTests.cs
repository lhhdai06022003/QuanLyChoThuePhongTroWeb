using System;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Bộ giới hạn đăng nhập sai (spec bảo mật xác thực §2.6).
    public class InMemoryLoginAttemptThrottleTests
    {
        private static readonly DateTimeOffset Start = new(2026, 11, 6, 3, 0, 0, TimeSpan.Zero);

        private readonly FixedTimeProvider _time = new(Start);
        private readonly CapturingLogger<InMemoryLoginAttemptThrottle> _logger = new();

        private InMemoryLoginAttemptThrottle Create(LoginThrottleOptions? options = null) =>
            new(options ?? new LoginThrottleOptions(), _time, _logger);

        private static void Fail(InMemoryLoginAttemptThrottle throttle, string ten, int times, string? ip = null)
        {
            for (var i = 0; i < times; i++)
            {
                throttle.RecordFailure(ten, ip);
            }
        }

        [Fact]
        public void FourFailures_DoNotLock()
        {
            var throttle = Create();

            Fail(throttle, "nhanvien", 4);

            Assert.Null(throttle.GetLockRemaining("nhanvien", null));
            Assert.Empty(_logger.Entries);
        }

        [Fact]
        public void FifthFailure_Locks_ForLockMinutes_AndLogsWarning()
        {
            var throttle = Create();

            Fail(throttle, "nhanvien", 5);

            Assert.Equal(TimeSpan.FromMinutes(15), throttle.GetLockRemaining("nhanvien", null));
            var entry = Assert.Single(_logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("nhanvien", entry.Message);
            Assert.Contains("15", entry.Message);
        }

        [Fact]
        public void Lock_Expires_AfterLockMinutes_AndCountingRestarts()
        {
            var throttle = Create();
            Fail(throttle, "nhanvien", 5);

            _time.SetUtcNow(Start.AddMinutes(14).AddSeconds(59));
            Assert.NotNull(throttle.GetLockRemaining("nhanvien", null));

            _time.SetUtcNow(Start.AddMinutes(15));
            Assert.Null(throttle.GetLockRemaining("nhanvien", null));

            // Hết khóa thì đếm lại từ đầu: một lần sai nữa chưa khóa.
            Fail(throttle, "nhanvien", 1);
            Assert.Null(throttle.GetLockRemaining("nhanvien", null));
        }

        [Fact]
        public void FailuresWhileLocked_DoNotExtendLock()
        {
            var throttle = Create();
            Fail(throttle, "nhanvien", 5);

            _time.SetUtcNow(Start.AddMinutes(10));
            Fail(throttle, "nhanvien", 3);

            Assert.Equal(TimeSpan.FromMinutes(5), throttle.GetLockRemaining("nhanvien", null));
        }

        [Fact]
        public void Window_Expired_RestartsCount()
        {
            var throttle = Create();
            Fail(throttle, "nhanvien", 4);

            _time.SetUtcNow(Start.AddMinutes(15));
            Fail(throttle, "nhanvien", 4);

            Assert.Null(throttle.GetLockRemaining("nhanvien", null));

            Fail(throttle, "nhanvien", 1);
            Assert.NotNull(throttle.GetLockRemaining("nhanvien", null));
        }

        [Fact]
        public void Success_ClearsUsernameCount_ButKeepsClientCount()
        {
            var throttle = Create(new LoginThrottleOptions { MaxFailuresPerClient = 5 });
            Fail(throttle, "nhanvien", 4, "10.0.0.1");

            throttle.RecordSuccess("nhanvien");

            // Tên đã được xóa đếm: 4 lần sai nữa chưa khóa theo tên.
            Fail(throttle, "nhanvien", 4);
            Assert.Null(throttle.GetLockRemaining("nhanvien", null));

            // IP vẫn giữ 4 lần sai cũ: thêm một lần là khóa theo IP, áp cho mọi tên từ IP đó.
            Fail(throttle, "khac", 1, "10.0.0.1");
            Assert.NotNull(throttle.GetLockRemaining("bat_ky", "10.0.0.1"));
            Assert.Null(throttle.GetLockRemaining("bat_ky", "10.0.0.2"));
        }

        [Fact]
        public void NullOrEmptyClientKey_IsNotCounted()
        {
            var throttle = Create(new LoginThrottleOptions { MaxFailuresPerUsername = 100, MaxFailuresPerClient = 2 });

            Fail(throttle, "a", 1, null);
            Fail(throttle, "b", 1, "");
            Fail(throttle, "c", 1, null);

            Assert.Null(throttle.GetLockRemaining("d", null));
            Assert.Equal(3, throttle.TrackedKeyCount);
        }

        [Fact]
        public void ClientCounting_Disabled_WhenMaxFailuresPerClientIsZero()
        {
            var throttle = Create(new LoginThrottleOptions { MaxFailuresPerUsername = 100, MaxFailuresPerClient = 0 });

            for (var i = 0; i < 50; i++)
            {
                throttle.RecordFailure("user" + i, "10.0.0.1");
            }

            Assert.Null(throttle.GetLockRemaining("moi", "10.0.0.1"));
        }

        [Fact]
        public void ClientKey_LocksAfterMaxFailuresPerClient()
        {
            var throttle = Create(new LoginThrottleOptions { MaxFailuresPerUsername = 100, MaxFailuresPerClient = 3 });

            throttle.RecordFailure("a", "10.0.0.1");
            throttle.RecordFailure("b", "10.0.0.1");
            Assert.Null(throttle.GetLockRemaining("c", "10.0.0.1"));

            throttle.RecordFailure("c", "10.0.0.1");
            Assert.NotNull(throttle.GetLockRemaining("moi", "10.0.0.1"));
        }

        [Fact]
        public void Username_IsCaseInsensitive_AndTrimmed()
        {
            var throttle = Create();

            throttle.RecordFailure("NhanVien", null);
            throttle.RecordFailure("nhanvien", null);
            throttle.RecordFailure(" NHANVIEN ", null);
            throttle.RecordFailure("nhanVIEN", null);
            throttle.RecordFailure("Nhanvien", null);

            Assert.NotNull(throttle.GetLockRemaining("nhanvien", null));
            Assert.NotNull(throttle.GetLockRemaining("NHANVIEN", null));
        }

        [Fact]
        public void LongUsernames_AreCutTo100Characters()
        {
            var throttle = Create();
            var prefix = new string('a', 100);

            for (var i = 0; i < 5; i++)
            {
                throttle.RecordFailure(prefix + i, null);
            }

            Assert.NotNull(throttle.GetLockRemaining(prefix + "khac", null));
        }

        [Fact]
        public void TrackedKeys_DoNotGrowBeyondMaxTrackedKeys()
        {
            var throttle = Create(new LoginThrottleOptions { MaxTrackedKeys = 100 });

            for (var i = 0; i < 1000; i++)
            {
                throttle.RecordFailure("ngau_nhien_" + i, null);
                Assert.True(throttle.TrackedKeyCount <= 100, "Số khóa vượt trần: " + throttle.TrackedKeyCount);
            }
        }

        [Fact]
        public void TrackedKeys_Cleanup_KeepsActiveLock()
        {
            var throttle = Create(new LoginThrottleOptions { MaxTrackedKeys = 100 });
            Fail(throttle, "admin", 5);

            for (var i = 0; i < 1000; i++)
            {
                throttle.RecordFailure("ngau_nhien_" + i, null);
            }

            Assert.NotNull(throttle.GetLockRemaining("admin", null));
        }

        [Fact]
        public void TrackedKeys_Cleanup_RemovesExpiredEntriesFirst()
        {
            var throttle = Create(new LoginThrottleOptions { MaxTrackedKeys = 100 });
            for (var i = 0; i < 100; i++)
            {
                throttle.RecordFailure("cu_" + i, null);
            }

            _time.SetUtcNow(Start.AddMinutes(30));
            throttle.RecordFailure("moi", null);

            Assert.Equal(1, throttle.TrackedKeyCount);
        }
    }
}
