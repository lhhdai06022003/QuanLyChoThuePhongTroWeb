using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.ChatModel;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private static readonly string TestConnectionString = ResolveTestConnectionString();
        public TestLogSink LogSink { get; } = new();
        public FakeEmailService FakeEmailServiceInstance { get; } = new();
        public FakeMeterImageStorageService FakeMeterImageStorageInstance { get; } = new();
        public FakeMeterOcrService FakeMeterOcrServiceInstance { get; } = new();
        public FakeAiChatModel FakeAiChatModelInstance { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTests");
            builder.UseSetting("BackgroundJobs:Enabled", "false");

            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(new TestLoggerProvider(LogSink));
                logging.AddFilter<TestLoggerProvider>(null, LogLevel.Trace);
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                    ["BackgroundJobs:Enabled"] = "false",
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["Logging:LogLevel:Microsoft"] = "Warning",
                    ["Logging:TestLogger:LogLevel:Default"] = "Trace"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(TestConnectionString, npgsql =>
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

                // Thay thế EmailService thật bằng test fake (một instance singleton để test có thể quan sát/định cấu hình).
                services.RemoveAll<IEmailService>();
                services.AddSingleton(FakeEmailServiceInstance);
                services.AddSingleton<IEmailService>(FakeEmailServiceInstance);

                // Thay thế ImageStorageService thật bằng test fake
                services.RemoveAll<IImageStorageService>();
                services.AddScoped<IImageStorageService, FakeImageStorageService>();

                // Thay thế State Stores thật bằng in-memory để không ghi file vật lý vào source
                services.RemoveAll<IInvoiceReminderStateStore>();
                services.AddSingleton<IInvoiceReminderStateStore, InMemoryInvoiceReminderStateStore>();

                services.RemoveAll<IContractExpiryAlertStateStore>();
                services.AddSingleton<IContractExpiryAlertStateStore, InMemoryContractExpiryAlertStateStore>();

                // Thay thế MeterImageStorageService thật bằng test fake
                services.RemoveAll<IMeterImageStorageService>();
                services.AddSingleton(FakeMeterImageStorageInstance);
                services.AddSingleton<IMeterImageStorageService>(FakeMeterImageStorageInstance);

                // Thay thế MeterOcrService thật bằng test fake
                services.RemoveAll<IMeterOcrService>();
                services.AddSingleton(FakeMeterOcrServiceInstance);
                services.AddSingleton<IMeterOcrService>(FakeMeterOcrServiceInstance);

                // Thay thế mô hình AI thật (Gemini) bằng test fake: test không bao giờ gọi mạng.
                services.RemoveAll<IAiChatModel>();
                services.AddSingleton<IAiChatModel>(FakeAiChatModelInstance);
            });
        }

        private static string ResolveTestConnectionString()
        {
            var env = Environment.GetEnvironmentVariable("QLCTPT_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(env))
            {
                throw new InvalidOperationException(
                    "Environment variable 'QLCTPT_TEST_CONNECTION_STRING' is required for Web integration tests. " +
                    "Tests fail closed when this variable is missing.");
            }

            ValidateDatabaseName(env);
            return env;
        }

        private static void ValidateDatabaseName(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var dbName = builder.Database;
            if (string.IsNullOrWhiteSpace(dbName) ||
                (!dbName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) &&
                 !dbName.EndsWith("_integration_test", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"[SAFETY GUARD] Web integration test host can ONLY connect to a database ending with '_test' or '_integration_test'. Attempted: '{dbName}'");
            }
        }

        public void AssertSuiteLogIntegrity(Func<TestLogEntry, bool>? allowListPredicate = null)
        {
            LogSink.AssertNoUnexpectedErrors(allowListPredicate);

            var bgJobLogs = LogSink.Entries.Where(e =>
                e.Category.Contains("ContractAutoCloseJob", StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains("ContractExpiryAlertJob", StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains("InvoiceReminderJob", StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains("InvoiceReminderUseCase", StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains("ContractExpiryAlertUseCase", StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains("ContractAutoCloseUseCase", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (bgJobLogs.Count > 0)
            {
                var details = string.Join(Environment.NewLine, bgJobLogs.Select(e => $"[{e.Level}] {e.Category}: {e.Message}"));
                throw new InvalidOperationException($"Background job activity detected in Web test host:{Environment.NewLine}{details}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    AssertSuiteLogIntegrity();
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
            else
            {
                base.Dispose(disposing);
            }
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                AssertSuiteLogIntegrity();
            }
            finally
            {
                await base.DisposeAsync();
            }
        }

        public class FakeEmailService : IEmailService
        {
            public record InvoiceEmailCall(string ToEmail, string MaHoaDon, string HanThanhToan);

            public ConcurrentQueue<InvoiceEmailCall> InvoiceEmailCalls { get; } = new();

            /// <summary>
            /// Cấu hình kết quả theo địa chỉ email cụ thể: true = thành công, false = trả lỗi an toàn.
            /// Không có trong dictionary => mặc định thành công.
            /// </summary>
            public ConcurrentDictionary<string, (bool IsSuccess, string ErrorMessage)> ResultByAddress { get; } = new();

            /// <summary>Địa chỉ trong tập này sẽ khiến SendInvoiceEmailAsync ném exception thay vì trả lỗi.</summary>
            public ConcurrentDictionary<string, bool> ThrowForAddress { get; } = new();

            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
                InvoiceEmailCalls.Enqueue(new InvoiceEmailCall(toEmail, hoaDon.MaHoaDon, hoaDon.HanThanhToan));

                if (ThrowForAddress.ContainsKey(toEmail))
                {
                    throw new InvalidOperationException("Fake SMTP exception for test address: " + toEmail);
                }

                if (ResultByAddress.TryGetValue(toEmail, out var configured))
                {
                    return Task.FromResult(configured);
                }

                return Task.FromResult((true, string.Empty));
            }

            public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
            {
                return Task.FromResult((true, string.Empty));
            }
        }

        public class FakeImageStorageService : IImageStorageService
        {
            public Task<string> UploadImageAsync(UploadFile file, string folderName)
            {
                return Task.FromResult("https://fake.storage/test-image.png");
            }
        }

        public class FakeMeterImageStorageService : IMeterImageStorageService
        {
            public int DeleteCallCount { get; private set; }
            public System.Func<UploadFile, string, Task<MeterImageUploadResult>>? OnUpload { get; set; }

            public Task<MeterImageUploadResult> UploadAsync(UploadFile file, string folder, CancellationToken cancellationToken = default)
            {
                if (OnUpload != null) return OnUpload(file, folder);
                var guid = System.Guid.NewGuid().ToString("N");
                return Task.FromResult(new MeterImageUploadResult($"https://example.test/meter/{guid}.jpg", $"meter_{guid}"));
            }

            public Task<MeterImageReadResult> ReadAsync(string publicId, string url, CancellationToken cancellationToken = default)
            {
                var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
                return Task.FromResult(new MeterImageReadResult(bytes, "image/jpeg"));
            }

            public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
            {
                DeleteCallCount++;
                return Task.CompletedTask;
            }

            public void Reset()
            {
                DeleteCallCount = 0;
                OnUpload = null;
            }
        }

        public class FakeMeterOcrService : IMeterOcrService
        {
            public bool ShouldThrow { get; set; }
            public bool ShouldReturnUnreadable { get; set; }
            public string? UnreadableReason { get; set; }
            public decimal DefaultSuggestedValue { get; set; } = 123.4m;
            public double DefaultConfidence { get; set; } = 0.9;

            public Task<MeterOcrResult> ProcessImageAsync(byte[] imageBytes, string contentType, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default)
            {
                if (ShouldThrow)
                {
                    throw new System.InvalidOperationException("Simulated OCR exception.");
                }

                if (ShouldReturnUnreadable)
                {
                    return Task.FromResult(MeterOcrResult.Unreadable(UnreadableReason ?? "Không đọc được chỉ số"));
                }

                return Task.FromResult(MeterOcrResult.Readable(DefaultSuggestedValue, DefaultConfidence));
            }

            public void Reset()
            {
                ShouldThrow = false;
                ShouldReturnUnreadable = false;
                UnreadableReason = null;
                DefaultSuggestedValue = 123.4m;
                DefaultConfidence = 0.9;
            }
        }

        public class FakeAiChatModel : IAiChatModel
        {
            private readonly object _lock = new();
            private readonly List<AiModelRequest> _requests = new();

            public IReadOnlyList<AiModelRequest> Requests
            {
                get { lock (_lock) return _requests.ToList(); }
            }

            // Tham số int là lần gọi trong test hiện tại, bắt đầu từ 1.
            public System.Func<AiModelRequest, int, AiModelResult> Responder { get; set; } = (_, _) => AiModelResult.Ok("Xin chào");

            public Task<AiModelResult> GenerateAsync(AiModelRequest request, CancellationToken cancellationToken = default)
            {
                int count;
                lock (_lock)
                {
                    _requests.Add(request);
                    count = _requests.Count;
                }
                return Task.FromResult(Responder(request, count));
            }

            public void Reset()
            {
                lock (_lock) _requests.Clear();
                Responder = (_, _) => AiModelResult.Ok("Xin chào");
            }
        }

        public class InMemoryInvoiceReminderStateStore : IInvoiceReminderStateStore
        {
            private readonly HashSet<int> _sentIds = new();
            private readonly object _lock = new();

            public Task<IReadOnlySet<int>> GetSentInvoiceIdsAsync(CancellationToken cancellationToken = default)
            {
                lock (_lock)
                {
                    return Task.FromResult<IReadOnlySet<int>>(new HashSet<int>(_sentIds));
                }
            }

            public Task<bool> HasBeenSentAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                lock (_lock)
                {
                    return Task.FromResult(_sentIds.Contains(hoaDonId));
                }
            }

            public Task MarkAsSentAsync(int hoaDonId, CancellationToken cancellationToken = default)
            {
                lock (_lock)
                {
                    _sentIds.Add(hoaDonId);
                }
                return Task.CompletedTask;
            }
        }

        public class InMemoryContractExpiryAlertStateStore : IContractExpiryAlertStateStore
        {
            private readonly HashSet<string> _sentKeys = new(StringComparer.OrdinalIgnoreCase);
            private readonly object _lock = new();

            public Task<IReadOnlySet<string>> GetSentAlertKeysAsync(CancellationToken cancellationToken = default)
            {
                lock (_lock)
                {
                    return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(_sentKeys, StringComparer.OrdinalIgnoreCase));
                }
            }

            public Task<bool> HasBeenSentAsync(string alertKey, CancellationToken cancellationToken = default)
            {
                lock (_lock)
                {
                    return Task.FromResult(_sentKeys.Contains(alertKey));
                }
            }

            public Task MarkAsSentAsync(string alertKey, CancellationToken cancellationToken = default)
            {
                lock (_lock)
                {
                    _sentKeys.Add(alertKey);
                }
                return Task.CompletedTask;
            }
        }
    }

    public class TestLogSink
    {
        private readonly ConcurrentBag<TestLogEntry> _entries = new();

        public void Add(LogLevel level, string category, string message, System.Exception? exception)
        {
            _entries.Add(new TestLogEntry(level, category, message, exception));
        }

        public IReadOnlyCollection<TestLogEntry> Entries => _entries.ToList();

        public void AssertNoUnexpectedErrors(System.Func<TestLogEntry, bool>? allowListPredicate = null)
        {
            var errors = _entries.Where(e => e.Level >= LogLevel.Error);
            if (allowListPredicate != null)
            {
                errors = errors.Where(e => !allowListPredicate(e));
            }

            var unallowed = errors.ToList();
            if (unallowed.Count > 0)
            {
                var details = string.Join(System.Environment.NewLine, unallowed.Select(e => $"[{e.Level}] {e.Category}: {e.Message} (Ex: {e.Exception?.Message})"));
                throw new System.InvalidOperationException($"Unallowed Error/Critical logs captured in Web test host:{System.Environment.NewLine}{details}");
            }
        }
    }

    public record TestLogEntry(LogLevel Level, string Category, string Message, System.Exception? Exception);

    [ProviderAlias("TestLogger")]
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly TestLogSink _sink;
        public TestLoggerProvider(TestLogSink sink) => _sink = sink;

        public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, _sink);
        public void Dispose() { }
    }

    public class TestLogger : ILogger
    {
        private readonly string _category;
        private readonly TestLogSink _sink;

        public TestLogger(string category, TestLogSink sink)
        {
            _category = category;
            _sink = sink;
        }

        public System.IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception, System.Func<TState, System.Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _sink.Add(logLevel, _category, message, exception);
        }
    }
}
