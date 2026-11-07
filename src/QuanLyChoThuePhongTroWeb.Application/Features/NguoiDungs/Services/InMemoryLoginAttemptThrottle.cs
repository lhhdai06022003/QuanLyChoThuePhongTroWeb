using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services
{
    // Bộ đếm đăng nhập sai trong bộ nhớ (spec bảo mật xác thực §2.6): một tiến trình, mất khi khởi động lại.
    // Khóa "u:<tên>" theo tên đăng nhập, "ip:<key>" theo địa chỉ máy khách. Cửa sổ tính từ lần sai đầu tiên.
    public class InMemoryLoginAttemptThrottle : ILoginAttemptThrottle
    {
        private const int DoDaiTenToiDa = 100;

        private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly LoginThrottleOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<InMemoryLoginAttemptThrottle> _logger;

        public InMemoryLoginAttemptThrottle(
            LoginThrottleOptions options,
            TimeProvider timeProvider,
            ILogger<InMemoryLoginAttemptThrottle> logger)
        {
            _options = options;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        // Số khóa đang giữ; dùng để kiểm trần bộ nhớ.
        public int TrackedKeyCount => _entries.Count;

        public TimeSpan? GetLockRemaining(string tenDangNhap, string? clientKey)
        {
            var now = _timeProvider.GetUtcNow();
            var theoTen = Remaining(UserKey(tenDangNhap), now);
            var theoMay = DemTheoMay(clientKey) ? Remaining(ClientKey(clientKey!), now) : null;

            if (theoTen.HasValue && theoMay.HasValue)
            {
                return theoTen.Value > theoMay.Value ? theoTen : theoMay;
            }

            return theoTen ?? theoMay;
        }

        public void RecordFailure(string tenDangNhap, string? clientKey)
        {
            var now = _timeProvider.GetUtcNow();
            var tenChuanHoa = NormalizeUsername(tenDangNhap);
            Fail("u:" + tenChuanHoa, _options.MaxFailuresPerUsername, tenChuanHoa, now);

            if (DemTheoMay(clientKey))
            {
                Fail(ClientKey(clientKey!), _options.MaxFailuresPerClient, "địa chỉ " + clientKey, now);
            }
        }

        public void RecordSuccess(string tenDangNhap)
        {
            _entries.TryRemove(UserKey(tenDangNhap), out _);
        }

        private bool DemTheoMay(string? clientKey) =>
            !string.IsNullOrWhiteSpace(clientKey) && _options.MaxFailuresPerClient > 0;

        private static string NormalizeUsername(string? tenDangNhap)
        {
            var ten = (tenDangNhap ?? string.Empty).Trim().ToLowerInvariant();
            return ten.Length > DoDaiTenToiDa ? ten.Substring(0, DoDaiTenToiDa) : ten;
        }

        private static string UserKey(string? tenDangNhap) => "u:" + NormalizeUsername(tenDangNhap);

        private static string ClientKey(string clientKey) => "ip:" + clientKey.Trim();

        private TimeSpan? Remaining(string key, DateTimeOffset now)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            lock (entry)
            {
                if (entry.LockedUntil is { } until && until > now)
                {
                    return until - now;
                }
            }

            return null;
        }

        private void Fail(string key, int maxFailures, string tenHienThi, DateTimeOffset now)
        {
            if (!_entries.ContainsKey(key) && _entries.Count >= _options.MaxTrackedKeys)
            {
                Cleanup(now);
            }

            var entry = _entries.GetOrAdd(key, _ => new Entry());
            lock (entry)
            {
                if (entry.LockedUntil is { } until)
                {
                    // Đang khóa thì không cộng thêm, không kéo dài thời gian khóa.
                    if (until > now)
                    {
                        return;
                    }

                    entry.LockedUntil = null;
                    entry.Count = 0;
                }

                if (entry.Count == 0 || now - entry.WindowStart >= Window)
                {
                    entry.Count = 0;
                    entry.WindowStart = now;
                }

                entry.Count++;
                entry.LastSeen = now;

                if (entry.Count >= maxFailures)
                {
                    entry.LockedUntil = now + LockDuration;
                    _logger.LogWarning("Tạm khóa đăng nhập cho {TenDangNhap} trong {Phut} phút", tenHienThi, _options.LockMinutes);
                }
            }
        }

        // Dọn mục hết hạn; vẫn đầy thì bỏ các mục cũ nhất, chừa chỗ để không phải dọn ở mỗi lượt.
        private void Cleanup(DateTimeOffset now)
        {
            foreach (var pair in _entries)
            {
                bool hetHan;
                lock (pair.Value)
                {
                    var conKhoa = pair.Value.LockedUntil is { } until && until > now;
                    hetHan = !conKhoa && now - pair.Value.WindowStart >= Window;
                }

                if (hetHan)
                {
                    _entries.TryRemove(pair.Key, out _);
                }
            }

            var mucTieu = _options.MaxTrackedKeys - Math.Max(1, _options.MaxTrackedKeys / 10);
            var canBo = _entries.Count - mucTieu;
            if (canBo <= 0)
            {
                return;
            }

            // Bỏ mục không bị khóa trước để chuỗi tên ngẫu nhiên khó đẩy một khóa đang hiệu lực ra khỏi bộ nhớ.
            var cuNhat = _entries
                .Select(pair =>
                {
                    lock (pair.Value)
                    {
                        var conKhoa = pair.Value.LockedUntil is { } until && until > now;
                        return (pair.Key, ConKhoa: conKhoa, pair.Value.LastSeen);
                    }
                })
                .OrderBy(x => x.ConKhoa)
                .ThenBy(x => x.LastSeen)
                .Take(canBo)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in cuNhat)
            {
                _entries.TryRemove(key, out _);
            }
        }

        private TimeSpan Window => TimeSpan.FromMinutes(_options.WindowMinutes);

        private TimeSpan LockDuration => TimeSpan.FromMinutes(_options.LockMinutes);

        private sealed class Entry
        {
            public int Count;
            public DateTimeOffset WindowStart;
            public DateTimeOffset LastSeen;
            public DateTimeOffset? LockedUntil;
        }
    }
}
