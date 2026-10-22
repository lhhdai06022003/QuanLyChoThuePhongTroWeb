using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Payments;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không dùng PostgreSqlFixture và không gọi SePay thật: mọi request đi qua handler giả (spec thanh toán trực tuyến §4.3).
    public class SePayTransactionClientTests
    {
        private const string Token = "SECRET-sepay-api-token";
        private static readonly DateTime From = new(2026, 10, 1, 17, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new(2026, 10, 4, 16, 59, 59, DateTimeKind.Utc);

        private const string EmptyPage = "{\"data\":[],\"meta\":{\"pagination\":{\"has_more\":false}}}";

        private sealed record Sent(string Uri, string? Authorization, string Accept, long Timestamp);

        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            // Tham số int là lần gọi hiện tại, bắt đầu từ 1.
            public Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } =
                (_, _, _) => Task.FromResult(Json(EmptyPage));
            public List<Sent> Requests { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(new Sent(
                    request.RequestUri!.OriginalString,
                    request.Headers.Authorization?.ToString(),
                    string.Join(",", request.Headers.Accept.Select(a => a.ToString())),
                    Stopwatch.GetTimestamp()));
                return await Handler(Requests.Count, request, cancellationToken);
            }
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception), exception));
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static SePayOptions Options(string token = Token, string baseUrl = "https://userapi.sepay.vn/v2")
            => new() { ApiToken = token, ApiBaseUrl = baseUrl };

        private static SePayTransactionClient Create(FakeHttpMessageHandler handler, SePayOptions? options = null, ILogger<SePayTransactionClient>? logger = null)
            => new(new HttpClient(handler), options ?? Options(), logger ?? new CapturingLogger<SePayTransactionClient>());

        private static string QueryValue(string url, string key)
        {
            var query = new Uri(url).Query.TrimStart('?').Split('&');
            var pair = query.Single(p => p.StartsWith(key + "=", StringComparison.Ordinal));
            return Uri.UnescapeDataString(pair[(key.Length + 1)..]);
        }

        // ---------- Yêu cầu ----------

        [Fact]
        public async Task Request_IsAGet_ToTheTransactionsEndpoint_WithTheVietnamTimeRange_TheIncomingFilter_AndPaging()
        {
            var handler = new FakeHttpMessageHandler();

            var result = await Create(handler, Options(baseUrl: " https://userapi.sepay.vn/v2/ ")).GetIncomingPageAsync(From, To, 2);

            Assert.True(result.Success, result.Message);
            var sent = Assert.Single(handler.Requests);
            Assert.StartsWith("https://userapi.sepay.vn/v2/transactions?", sent.Uri);
            // 2026-10-01T17:00:00Z = 2026-10-02 00:00:00 giờ Việt Nam; 2026-10-04T16:59:59Z = 2026-10-04 23:59:59.
            Assert.Contains("transaction_date_from=2026-10-02%2000%3A00%3A00", sent.Uri);
            Assert.Contains("transaction_date_to=2026-10-04%2023%3A59%3A59", sent.Uri);
            Assert.Equal("2026-10-02 00:00:00", QueryValue(sent.Uri, "transaction_date_from"));
            Assert.Equal("2026-10-04 23:59:59", QueryValue(sent.Uri, "transaction_date_to"));
            Assert.Equal("in", QueryValue(sent.Uri, "transfer_type"));
            Assert.Equal("2", QueryValue(sent.Uri, "page"));
            Assert.Equal("100", QueryValue(sent.Uri, "per_page"));
        }

        [Fact]
        public async Task Request_CarriesTheTrimmedBearerToken_AndAcceptsJson()
        {
            var handler = new FakeHttpMessageHandler();
            var http = new HttpClient(handler);

            await new SePayTransactionClient(http, Options($"  {Token}  "), new CapturingLogger<SePayTransactionClient>())
                .GetIncomingPageAsync(From, To, 1);

            var sent = Assert.Single(handler.Requests);
            Assert.Equal($"Bearer {Token}", sent.Authorization);
            Assert.Equal("application/json", sent.Accept);
            // Token gắn vào từng request, không vào header mặc định của HttpClient dùng chung.
            Assert.Null(http.DefaultRequestHeaders.Authorization);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task WithoutAnApiToken_FailsWithoutSendingAnything(string token)
        {
            var handler = new FakeHttpMessageHandler();

            var result = await Create(handler, Options(token)).GetIncomingPageAsync(From, To, 1);

            Assert.False(result.Success);
            Assert.Equal("Chưa cấu hình SePay:ApiToken.", result.Message);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task TwoRequests_AreSpacedAtLeastAboutThreeHundredMilliseconds()
        {
            var handler = new FakeHttpMessageHandler();
            var client = Create(handler);

            await client.GetIncomingPageAsync(From, To, 1);
            await client.GetIncomingPageAsync(From, To, 2);

            Assert.Equal(2, handler.Requests.Count);
            var gap = Stopwatch.GetElapsedTime(handler.Requests[0].Timestamp, handler.Requests[1].Timestamp);
            Assert.True(gap >= TimeSpan.FromMilliseconds(300), $"Khoảng cách giữa hai request chỉ {gap.TotalMilliseconds:0} ms.");
        }

        // ---------- Đọc phản hồi ----------

        private const string SamplePage = """
            {
              "data": [
                {
                  "id": "7d3f1c3e-1111-4222-8333-444455556666",
                  "transaction_date": "2026-10-17 10:00:00",
                  "account_number": "0123456789",
                  "va": "VA001",
                  "transfer_type": "in",
                  "amount_in": 2000000,
                  "transaction_content": "Nguyen Van A TTPTHD7",
                  "reference_number": "FT26ABC",
                  "code": "TTPTHD7",
                  "bank_brand_name": "MBBank"
                },
                {
                  "id": 12345,
                  "transaction_date": "2026-10-17 10:05:00",
                  "transfer_type": "in",
                  "amount_in": "1500000.00",
                  "reference_number": null
                }
              ],
              "meta": { "pagination": { "has_more": true } }
            }
            """;

        [Fact]
        public async Task Parse_ReadsStringAndNumericIds_NumericAndStringAmounts_AndConvertsTheDateToUtc()
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => Task.FromResult(Json(SamplePage)) };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            var page = result.Data!;
            Assert.True(page.HasMore);
            Assert.Equal(0, page.SoGiaoDichSaiDinhDang);
            Assert.Equal(2, page.Items.Count);

            var first = page.Items[0];
            Assert.Equal("7d3f1c3e-1111-4222-8333-444455556666", first.Id);
            // 10:00 giờ Việt Nam = 03:00 UTC.
            Assert.Equal(new DateTime(2026, 10, 17, 3, 0, 0), first.TransactionDateUtc);
            Assert.Equal(DateTimeKind.Utc, first.TransactionDateUtc.Kind);
            Assert.Equal("0123456789", first.AccountNumber);
            Assert.Equal("VA001", first.SubAccount);
            Assert.Equal("in", first.TransferType);
            Assert.Equal(2_000_000m, first.AmountIn);
            Assert.Equal("Nguyen Van A TTPTHD7", first.Content);
            Assert.Equal("FT26ABC", first.ReferenceNumber);
            Assert.Equal("TTPTHD7", first.Code);
            Assert.Equal("MBBank", first.BankBrandName);

            var second = page.Items[1];
            Assert.Equal("12345", second.Id);
            Assert.Equal(new DateTime(2026, 10, 17, 3, 5, 0), second.TransactionDateUtc);
            Assert.Equal(1_500_000.00m, second.AmountIn);
            Assert.Null(second.ReferenceNumber);
            Assert.Null(second.AccountNumber);
            Assert.Null(second.Code);
            Assert.Null(second.Content);
        }

        [Fact]
        public async Task Parse_FieldsOfTheWrongTypeAreTreatedAsMissing_ForTheOptionalStrings()
        {
            const string body = """
                { "data": [ { "id": "a", "transaction_date": "2026-10-17 10:00:00", "amount_in": 1,
                  "reference_number": 123, "code": ["x"], "transaction_content": {"a":1}, "transfer_type": true } ] }
                """;
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => Task.FromResult(Json(body)) };

            var item = Assert.Single((await Create(handler).GetIncomingPageAsync(From, To, 1)).Data!.Items);

            Assert.Null(item.ReferenceNumber);
            Assert.Null(item.Code);
            Assert.Null(item.Content);
            Assert.Null(item.TransferType);
        }

        [Fact]
        public async Task Parse_ElementsMissingARequiredField_AreDropped_AndCounted()
        {
            const string body = """
                { "data": [
                  { "id": "ok", "transaction_date": "2026-10-17 10:00:00", "amount_in": 5 },
                  { "transaction_date": "2026-10-17 10:00:00", "amount_in": 5 },
                  { "id": "", "transaction_date": "2026-10-17 10:00:00", "amount_in": 5 },
                  { "id": "no-date", "amount_in": 5 },
                  { "id": "bad-date", "transaction_date": "2026-10-17T10:00:00", "amount_in": 5 },
                  { "id": "no-amount", "transaction_date": "2026-10-17 10:00:00" },
                  { "id": "bad-amount", "transaction_date": "2026-10-17 10:00:00", "amount_in": "abc" },
                  { "id": "null-amount", "transaction_date": "2026-10-17 10:00:00", "amount_in": null },
                  { "id": "year-1", "transaction_date": "0001-01-01 00:00:00", "amount_in": 5 },
                  7
                ] }
                """;
            var logger = new CapturingLogger<SePayTransactionClient>();
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => Task.FromResult(Json(body)) };

            var page = (await Create(handler, logger: logger).GetIncomingPageAsync(From, To, 1)).Data!;

            Assert.Equal("ok", Assert.Single(page.Items).Id);
            Assert.Equal(9, page.SoGiaoDichSaiDinhDang);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("9", entry.Message);
        }

        [Theory]
        [InlineData("{\"data\":[],\"meta\":{\"pagination\":{\"has_more\":true}}}", true)]
        [InlineData("{\"data\":[],\"meta\":{\"pagination\":{\"has_more\":false}}}", false)]
        [InlineData("{\"data\":[],\"meta\":{\"pagination\":{\"current_page\":1,\"last_page\":3}}}", true)]
        [InlineData("{\"data\":[],\"meta\":{\"pagination\":{\"current_page\":3,\"last_page\":3}}}", false)]
        [InlineData("{\"data\":[],\"meta\":{\"pagination\":{\"has_more\":\"true\",\"current_page\":1,\"last_page\":2}}}", true)]
        [InlineData("{\"data\":[],\"meta\":{\"pagination\":{\"has_more\":false,\"current_page\":1,\"last_page\":5}}}", false)]
        [InlineData("{\"data\":[],\"meta\":{\"pagination\":{}}}", false)]
        [InlineData("{\"data\":[],\"meta\":{}}", false)]
        [InlineData("{\"data\":[]}", false)]
        public async Task Parse_HasMore_PrefersTheFlag_ThenComparesPages_ThenIsFalse(string body, bool expected)
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => Task.FromResult(Json(body)) };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(expected, result.Data!.HasMore);
        }

        [Theory]
        [InlineData("không phải JSON")]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("{\"items\":[]}")]
        [InlineData("{\"data\":{}}")]
        [InlineData("{\"data\":null}")]
        [InlineData("{\"data\":[")]
        public async Task Parse_ABodyThatIsNotTheExpectedShape_Fails(string body)
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => Task.FromResult(Json(body)) };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.False(result.Success);
            Assert.Equal("Phản hồi SePay không đúng định dạng.", result.Message);
        }

        // ---------- Giới hạn tốc độ (429) ----------

        [Fact]
        public async Task TooManyRequests_ThenSuccess_RetriesAfterTheRetryAfterHeader()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (n, _, _) =>
                {
                    if (n == 1)
                    {
                        var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                        limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                        return Task.FromResult(limited);
                    }

                    return Task.FromResult(Json(SamplePage));
                }
            };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Equal(handler.Requests[0].Uri, handler.Requests[1].Uri);
            Assert.Equal(2, result.Data!.Items.Count);
        }

        [Fact]
        public async Task TooManyRequests_WithoutARetryAfterHeader_WaitsAboutOneSecondBeforeRetrying()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (n, _, _) => Task.FromResult(n == 1 ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : Json(EmptyPage))
            };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, handler.Requests.Count);
            var gap = Stopwatch.GetElapsedTime(handler.Requests[0].Timestamp, handler.Requests[1].Timestamp);
            Assert.True(gap >= TimeSpan.FromMilliseconds(900), $"Chỉ chờ {gap.TotalMilliseconds:0} ms.");
        }

        [Fact]
        public async Task TooManyRequests_WithAPastRetryAfterDate_RetriesWithoutWaitingLonger()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (n, _, _) =>
                {
                    if (n == 1)
                    {
                        var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                        limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-5));
                        return Task.FromResult(limited);
                    }

                    return Task.FromResult(Json(EmptyPage));
                }
            };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, handler.Requests.Count);
            // Ngày đã qua nghĩa là hết hạn chờ (kẹp về 0), chỉ còn khoảng cách tối thiểu giữa hai request.
            Assert.True(Stopwatch.GetElapsedTime(handler.Requests[0].Timestamp, handler.Requests[1].Timestamp) < TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task TooManyRequests_FiveTimes_FailsAfterExactlyFourRequests_AndLogsAWarning()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _, _) =>
                {
                    var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                    return Task.FromResult(limited);
                }
            };
            var logger = new CapturingLogger<SePayTransactionClient>();

            var result = await Create(handler, logger: logger).GetIncomingPageAsync(From, To, 1);

            Assert.False(result.Success);
            Assert.Equal("SePay đang giới hạn số yêu cầu, thử lại sau ít phút.", result.Message);
            Assert.Equal(4, handler.Requests.Count);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.DoesNotContain(Token, entry.Message);
        }

        // ---------- Lỗi ----------

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized, 401)]
        [InlineData(HttpStatusCode.Forbidden, 403)]
        public async Task AuthenticationFailure_ReturnsAMessageWithTheCode_ButNeverTheToken(HttpStatusCode status, int code)
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => Task.FromResult(Json($"{{\"error\":\"{Token}\"}}", status)) };
            var logger = new CapturingLogger<SePayTransactionClient>();

            var result = await Create(handler, logger: logger).GetIncomingPageAsync(From, To, 1);

            Assert.False(result.Success);
            Assert.Equal($"SePay từ chối API token (mã {code}). Kiểm tra SePay:ApiToken.", result.Message);
            Assert.DoesNotContain(Token, result.Message);
            Assert.Single(handler.Requests);
            Assert.All(logger.Entries, e => Assert.DoesNotContain(Token, e.Message));
        }

        [Fact]
        public async Task OtherErrorStatus_ReturnsTheCode_WithoutTheBody()
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => Task.FromResult(Json($"{{\"detail\":\"{Token}\"}}", HttpStatusCode.InternalServerError)) };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.False(result.Success);
            Assert.Equal("SePay trả mã 500.", result.Message);
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task ConnectionFailure_ReturnsAFriendlyMessage_AndNeverLeaksTheTokenOrTheUrl()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, request, _) => throw new HttpRequestException($"Lỗi khi gọi {request.RequestUri} với {Token}")
            };
            var logger = new CapturingLogger<SePayTransactionClient>();

            var result = await Create(handler, logger: logger).GetIncomingPageAsync(From, To, 1);

            Assert.False(result.Success);
            Assert.Equal("Không kết nối được SePay. Thử lại sau.", result.Message);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(nameof(HttpRequestException), entry.Message);
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Token, entry.Message);
            Assert.DoesNotContain("userapi.sepay.vn", entry.Message);
            Assert.DoesNotContain(Token, result.Message);
        }

        [Fact]
        public async Task Timeout_ReturnsAFriendlyMessage_BecauseTheCallerDidNotCancel()
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _, _) => throw new TaskCanceledException("HttpClient.Timeout") };

            var result = await Create(handler).GetIncomingPageAsync(From, To, 1);

            Assert.False(result.Success);
            Assert.Equal("Không kết nối được SePay. Thử lại sau.", result.Message);
        }

        [Fact]
        public async Task CancelledByTheCaller_Throws()
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _, ct) => Task.FromCanceled<HttpResponseMessage>(ct) };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(handler).GetIncomingPageAsync(From, To, 1, cts.Token));
        }

        [Fact]
        public async Task CancelledWhileWaitingToRetry_Throws()
        {
            using var cts = new CancellationTokenSource();
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _, _) =>
                {
                    var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
                    cts.CancelAfter(TimeSpan.FromMilliseconds(100));
                    return Task.FromResult(limited);
                }
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(handler).GetIncomingPageAsync(From, To, 1, cts.Token));
            Assert.Single(handler.Requests);
        }

        // ---------- Đăng ký trong AddInfrastructure ----------

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            public List<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

            public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Entries);

            public void Dispose()
            {
            }

            private sealed class Logger : ILogger
            {
                private readonly string _category;
                private readonly List<(string Category, LogLevel Level, string Message)> _entries;

                public Logger(string category, List<(string Category, LogLevel Level, string Message)> entries)
                {
                    _category = category;
                    _entries = entries;
                }

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    lock (_entries)
                    {
                        _entries.Add((_category, logLevel, formatter(state, exception)));
                    }
                }
            }
        }

        // Header Authorization chứa ApiToken. Client đăng ký trong AddInfrastructure phải gỡ logger của HttpClient,
        // nếu không "Sending HTTP request GET https://.../transactions?..." (và header) có thể bị in ra log.
        [Fact]
        public async Task TheRegisteredClient_NeverLogsTheRequest_ThroughHttpClientLogging()
        {
            var handler = new FakeHttpMessageHandler();
            var logs = new CapturingLoggerProvider();
            using var host = new HostBuilder()
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused_test;Username=unused;Password=unused",
                    ["BackgroundJobs:Enabled"] = "false",
                    ["SePay:ApiToken"] = Token
                }))
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration);
                    // Tên của typed client là tên kiểu dịch vụ; thay handler gửi thật bằng handler giả.
                    services.AddHttpClient(nameof(ISePayTransactionClient)).ConfigurePrimaryHttpMessageHandler(() => handler);
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(logs);
                        logging.SetMinimumLevel(LogLevel.Trace);
                    });
                })
                .Build();

            var result = await host.Services.GetRequiredService<ISePayTransactionClient>().GetIncomingPageAsync(From, To, 1);

            Assert.True(result.Success, result.Message);
            var sent = Assert.Single(handler.Requests);
            Assert.StartsWith("https://userapi.sepay.vn/v2/transactions?", sent.Uri);
            Assert.Equal($"Bearer {Token}", sent.Authorization);
            Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(Token));
            Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("userapi.sepay.vn"));
            Assert.DoesNotContain(logs.Entries, e => e.Category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal));
        }
    }
}
