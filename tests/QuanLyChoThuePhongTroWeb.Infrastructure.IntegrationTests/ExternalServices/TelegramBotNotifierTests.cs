using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Telegram;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không dùng PostgreSqlFixture và không gọi Telegram thật: mọi request đi qua handler giả.
    public class TelegramBotNotifierTests
    {
        private const string Token = "123456:SECRET-bot-token";
        private const string ChatId = "-100100";
        private const string Html = "<b>Thu tiền</b> &amp; 500.000 VNĐ";

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } =
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json") });
            public List<HttpRequestMessage> Requests { get; } = new();
            public List<string> Bodies { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
                return await Handler(request, cancellationToken);
            }
        }

        private class CapturingLogger<T> : ILogger<T>
        {
            public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception), exception));
        }

        private static TelegramOptions Configured() => new() { Enabled = true, BotToken = Token, DefaultChatId = ChatId };

        private static TelegramBotNotifier Create(FakeHttpMessageHandler handler, TelegramOptions? options = null, ILogger<TelegramBotNotifier>? logger = null)
            => new(new HttpClient(handler), options ?? Configured(), logger ?? NullLogger<TelegramBotNotifier>.Instance);

        [Fact]
        public async Task Send_PostsJsonToTheBotApi_WithChatIdTextAndHtmlParseMode()
        {
            var handler = new FakeHttpMessageHandler();

            var (isSuccess, errorMessage) = await Create(handler).SendMessageAsync(ChatId, Html);

            Assert.True(isSuccess);
            Assert.Equal(string.Empty, errorMessage);
            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"https://api.telegram.org/bot{Token}/sendMessage", request.RequestUri!.ToString());
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);

            using var body = JsonDocument.Parse(Assert.Single(handler.Bodies));
            Assert.Equal(ChatId, body.RootElement.GetProperty("chat_id").GetString());
            Assert.Equal(Html, body.RootElement.GetProperty("text").GetString());
            Assert.Equal("HTML", body.RootElement.GetProperty("parse_mode").GetString());
        }

        [Fact]
        public async Task Send_TrimsTheBotToken_InTheUrl()
        {
            var handler = new FakeHttpMessageHandler();
            var options = new TelegramOptions { Enabled = true, BotToken = $"  {Token}  ", DefaultChatId = ChatId };

            await Create(handler, options).SendMessageAsync(ChatId, Html);

            Assert.Equal($"https://api.telegram.org/bot{Token}/sendMessage", Assert.Single(handler.Requests).RequestUri!.ToString());
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, 400)]
        [InlineData(HttpStatusCode.Forbidden, 403)]
        [InlineData(HttpStatusCode.TooManyRequests, 429)]
        [InlineData(HttpStatusCode.InternalServerError, 500)]
        public async Task NonSuccessStatus_ReturnsFalseWithTheStatusCode_AndLogsAWarningWithoutSecrets(HttpStatusCode status, int code)
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent($"{{\"description\":\"{Token} {Html}\"}}") })
            };
            var logger = new CapturingLogger<TelegramBotNotifier>();

            var (isSuccess, errorMessage) = await Create(handler, logger: logger).SendMessageAsync(ChatId, Html);

            Assert.False(isSuccess);
            Assert.Equal($"Telegram trả mã {code}", errorMessage);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(code.ToString(), entry.Message);
            Assert.DoesNotContain(Token, entry.Message);
            Assert.DoesNotContain("SECRET", entry.Message);
            Assert.DoesNotContain("Thu tiền", entry.Message);
            Assert.DoesNotContain(Token, errorMessage);
        }

        [Fact]
        public async Task RequestFailure_ReturnsFalse_AndNeverLeaksTheTokenOrTheUrl()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (request, _) => throw new HttpRequestException($"Lỗi khi gọi {request.RequestUri}")
            };
            var logger = new CapturingLogger<TelegramBotNotifier>();

            var (isSuccess, errorMessage) = await Create(handler, logger: logger).SendMessageAsync(ChatId, Html);

            Assert.False(isSuccess);
            Assert.Equal("Không kết nối được Telegram.", errorMessage);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(nameof(HttpRequestException), entry.Message);
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Token, entry.Message);
            Assert.DoesNotContain("api.telegram.org", entry.Message);
            Assert.DoesNotContain("Thu tiền", entry.Message);
        }

        [Fact]
        public async Task Timeout_ReturnsFalse_BecauseTheCallerDidNotCancel()
        {
            var handler = new FakeHttpMessageHandler { Handler = (_, _) => throw new TaskCanceledException("HttpClient.Timeout") };

            var (isSuccess, errorMessage) = await Create(handler).SendMessageAsync(ChatId, Html);

            Assert.False(isSuccess);
            Assert.Equal("Không kết nối được Telegram.", errorMessage);
        }

        [Fact]
        public async Task CancelledByTheCaller_Throws()
        {
            var handler = new FakeHttpMessageHandler
            {
                Handler = (_, ct) => Task.FromCanceled<HttpResponseMessage>(ct)
            };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(handler).SendMessageAsync(ChatId, Html, cts.Token));
        }

        [Theory]
        [InlineData(false, Token, ChatId)]
        [InlineData(true, "", ChatId)]
        [InlineData(true, Token, "")]
        public async Task NotConfigured_SendsNothing(bool enabled, string token, string defaultChatId)
        {
            var handler = new FakeHttpMessageHandler();
            var options = new TelegramOptions { Enabled = enabled, BotToken = token, DefaultChatId = defaultChatId };

            var (isSuccess, errorMessage) = await Create(handler, options).SendMessageAsync(ChatId, Html);

            Assert.False(isSuccess);
            Assert.Equal("Telegram chưa được cấu hình.", errorMessage);
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task BlankChatId_SendsNothing(string chatId)
        {
            var handler = new FakeHttpMessageHandler();

            var (isSuccess, errorMessage) = await Create(handler).SendMessageAsync(chatId, Html);

            Assert.False(isSuccess);
            Assert.Equal("Telegram chưa được cấu hình.", errorMessage);
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task OptionsChangedAtRuntime_TakeEffectOnTheNextSend()
        {
            var handler = new FakeHttpMessageHandler();
            var options = Configured();
            var notifier = Create(handler, options);

            options.Enabled = false;
            var disabled = await notifier.SendMessageAsync(ChatId, Html);
            options.Enabled = true;
            var enabled = await notifier.SendMessageAsync(ChatId, Html);

            Assert.False(disabled.IsSuccess);
            Assert.True(enabled.IsSuccess);
            Assert.Single(handler.Requests);
        }

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

        // URL gọi Telegram chứa BotToken. Client đăng ký trong AddInfrastructure phải gỡ logger của HttpClient,
        // nếu không "Sending HTTP request POST https://api.telegram.org/bot{token}/sendMessage" bị in ra log.
        [Fact]
        public async Task TheRegisteredClient_NeverLogsTheRequestUrl_ThroughHttpClientLogging()
        {
            var handler = new FakeHttpMessageHandler();
            var logs = new CapturingLoggerProvider();
            using var host = new HostBuilder()
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused_test;Username=unused;Password=unused",
                    ["BackgroundJobs:Enabled"] = "false",
                    ["Telegram:Enabled"] = "true",
                    ["Telegram:BotToken"] = Token,
                    ["Telegram:DefaultChatId"] = ChatId
                }))
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration);
                    // Tên của typed client là tên kiểu dịch vụ; thay handler gửi thật bằng handler giả.
                    services.AddHttpClient(nameof(ITelegramNotifier)).ConfigurePrimaryHttpMessageHandler(() => handler);
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(logs);
                        logging.SetMinimumLevel(LogLevel.Trace);
                    });
                })
                .Build();

            var (isSuccess, _) = await host.Services.GetRequiredService<ITelegramNotifier>().SendMessageAsync(ChatId, Html);

            Assert.True(isSuccess);
            Assert.Equal($"https://api.telegram.org/bot{Token}/sendMessage", Assert.Single(handler.Requests).RequestUri!.ToString());
            Assert.DoesNotContain(logs.Entries, e => e.Message.Contains(Token));
            Assert.DoesNotContain(logs.Entries, e => e.Message.Contains("api.telegram.org"));
            Assert.DoesNotContain(logs.Entries, e => e.Category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal));
        }
    }
}
