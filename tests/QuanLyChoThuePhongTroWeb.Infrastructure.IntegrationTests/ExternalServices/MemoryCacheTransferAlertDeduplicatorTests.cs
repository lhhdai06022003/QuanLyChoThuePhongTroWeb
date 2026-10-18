using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không cần DB: chỉ dùng MemoryCache trong process.
    public class MemoryCacheTransferAlertDeduplicatorTests
    {
        // Ghi lại các mục được tạo để kiểm khóa và thời hạn mà không phải chờ 24 giờ.
        private sealed class RecordingMemoryCache : IMemoryCache
        {
            private readonly MemoryCache _inner = new(new MemoryCacheOptions());

            public List<ICacheEntry> Entries { get; } = new();

            public ICacheEntry CreateEntry(object key)
            {
                var entry = _inner.CreateEntry(key);
                Entries.Add(entry);
                return entry;
            }

            public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);

            public void Remove(object key) => _inner.Remove(key);

            public void Dispose() => _inner.Dispose();
        }

        private static MemoryCacheTransferAlertDeduplicator Create(IMemoryCache? cache = null)
            => new(cache ?? new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public void SameKey_IsRegisteredOnce()
        {
            var deduplicator = Create();

            Assert.True(deduplicator.TryRegister("SEPAY-FT26ABC"));
            Assert.False(deduplicator.TryRegister("SEPAY-FT26ABC"));
            Assert.False(deduplicator.TryRegister("SEPAY-FT26ABC"));
        }

        [Fact]
        public void DifferentKeys_AreRegisteredIndependently()
        {
            var deduplicator = Create();

            Assert.True(deduplicator.TryRegister("SEPAY-FT-A"));
            Assert.True(deduplicator.TryRegister("SEPAY-FT-B"));
            Assert.True(deduplicator.TryRegister("SEPAY-ID-92704"));
            Assert.False(deduplicator.TryRegister("SEPAY-FT-A"));
        }

        [Fact]
        public void KeysAreCaseSensitive_BecauseDedupKeysAreAlreadyNormalized()
        {
            var deduplicator = Create();

            Assert.True(deduplicator.TryRegister("SEPAY-FT26ABC"));
            Assert.True(deduplicator.TryRegister("sepay-ft26abc"));
        }

        [Fact]
        public void TwoInstancesOverTheSameCache_ShareTheirMemory()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());

            Assert.True(Create(cache).TryRegister("SEPAY-FT26ABC"));
            Assert.False(Create(cache).TryRegister("SEPAY-FT26ABC"));
        }

        [Fact]
        public void TheEntry_IsPrefixedAndKeptFor24Hours_AndASecondRegistrationCreatesNoNewEntry()
        {
            var cache = new RecordingMemoryCache();
            var deduplicator = Create(cache);

            deduplicator.TryRegister("SEPAY-FT26ABC");
            deduplicator.TryRegister("SEPAY-FT26ABC");

            var entry = Assert.Single(cache.Entries);
            Assert.Equal("sepay-alert:SEPAY-FT26ABC", entry.Key);
            Assert.Equal(TimeSpan.FromHours(24), entry.AbsoluteExpirationRelativeToNow);
        }

        [Fact]
        public async Task ConcurrentRegistrations_OfTheSameKey_OnlyOneWins()
        {
            var deduplicator = Create();

            var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => deduplicator.TryRegister("SEPAY-FT-DUA"))));

            Assert.Equal(1, results.Count(r => r));
        }
    }
}
