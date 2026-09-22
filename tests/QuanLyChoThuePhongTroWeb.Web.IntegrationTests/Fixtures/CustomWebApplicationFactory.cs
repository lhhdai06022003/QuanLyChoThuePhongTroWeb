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
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private static readonly string TestConnectionString = ResolveTestConnectionString();
        public TestLogSink LogSink { get; } = new();

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

                // Thay thế EmailService thật bằng test fake
                services.RemoveAll<IEmailService>();
                services.AddScoped<IEmailService, FakeEmailService>();

                // Thay thế ImageStorageService thật bằng test fake
                services.RemoveAll<IImageStorageService>();
                services.AddScoped<IImageStorageService, FakeImageStorageService>();

                // Thay thế State Stores thật bằng in-memory để không ghi file vật lý vào source
                services.RemoveAll<IInvoiceReminderStateStore>();
                services.AddSingleton<IInvoiceReminderStateStore, InMemoryInvoiceReminderStateStore>();

                services.RemoveAll<IContractExpiryAlertStateStore>();
                services.AddSingleton<IContractExpiryAlertStateStore, InMemoryContractExpiryAlertStateStore>();
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
            public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            {
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
