using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Bổ sung cho SePayTransactionClientTests: chặn trên của thời gian chờ khi SePay trả Retry-After quá lớn, và thời gian chờ tổng
    // khi bị giới hạn liên tục. Không DB, không gọi SePay thật.
    public class SePayTransactionClientBackoffTests
    {
        private const string Token = "SECRET-sepay-api-token";
        private static readonly DateTime From = new(2026, 10, 1, 17, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new(2026, 10, 4, 16, 59, 59, DateTimeKind.Utc);
        private const string EmptyPage = "{\"data\":[],\"meta\":{\"pagination\":{\"has_more\":false}}}";

        private sealed class ScriptedHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<long> Timestamps { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Timestamps.Add(Stopwatch.GetTimestamp());
                return Task.FromResult(respond(Timestamps.Count));
            }
        }

        private sealed class NullLog<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        private static HttpResponseMessage Limited(TimeSpan retryAfter)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            return response;
        }

        private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new StringContent(EmptyPage, Encoding.UTF8, "application/json") };

        private static SePayTransactionClient Client(ScriptedHandler handler)
            => new(new HttpClient(handler), new SePayOptions { ApiToken = Token, ApiBaseUrl = "https://userapi.sepay.vn/v2" }, new NullLog<SePayTransactionClient>());

        [Fact]
        public async Task AHugeRetryAfter_IsCappedAtAboutTenSeconds_SoOneSyncCannotHangForHours()
        {
            var handler = new ScriptedHandler(n => n == 1 ? Limited(TimeSpan.FromHours(1)) : Ok());

            var result = await Client(handler).GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, handler.Timestamps.Count);
            var waited = Stopwatch.GetElapsedTime(handler.Timestamps[0], handler.Timestamps[1]);
            Assert.InRange(waited.TotalSeconds, 9.0, 13.0);
        }

        [Fact]
        public async Task ANegativeRetryAfterDelta_DoesNotThrow_AndRetriesWithoutLongWaiting()
        {
            var handler = new ScriptedHandler(n => n == 1 ? Limited(TimeSpan.FromSeconds(-30)) : Ok());

            var result = await Client(handler).GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, handler.Timestamps.Count);
            Assert.True(Stopwatch.GetElapsedTime(handler.Timestamps[0], handler.Timestamps[1]) < TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task EachRetryAfter429_CarriesTheBearerTokenAgainOnTheNewRequest()
        {
            var tokens = new List<string?>();
            var handler = new ScriptedHandler(n => n < 3 ? Limited(TimeSpan.Zero) : Ok());
            var http = new HttpClient(new RecordingTokenHandler(handler, tokens));
            var client = new SePayTransactionClient(
                http, new SePayOptions { ApiToken = Token, ApiBaseUrl = "https://userapi.sepay.vn/v2" }, new NullLog<SePayTransactionClient>());

            var result = await client.GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(3, tokens.Count);
            Assert.All(tokens, t => Assert.Equal($"Bearer {Token}", t));
        }

        private sealed class RecordingTokenHandler(HttpMessageHandler inner, List<string?> tokens) : DelegatingHandler(inner)
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                tokens.Add(request.Headers.Authorization?.ToString());
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
