using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures
{
    public class PostgreSqlFixture : IAsyncLifetime
    {
        private string _connectionString = string.Empty;
        public string ConnectionString => _connectionString;

        public async Task InitializeAsync()
        {
            _connectionString = ResolveConnectionString();
            ValidateDatabaseSafety(_connectionString);

            // Apply all EF Core migrations to ensure schema is 100% up to date.
            // Any connection failure or migration error will fail the test fixture immediately.
            var options = CreateDbContextOptions();
            await using var context = new ApplicationDbContext(options);
            await context.Database.MigrateAsync();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public DbContextOptions<ApplicationDbContext> CreateDbContextOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(_connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            });
            return optionsBuilder.Options;
        }

        public ApplicationDbContext CreateDbContext()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("PostgreSQL test fixture is not initialized.");
            }
            return new ApplicationDbContext(CreateDbContextOptions());
        }

        public static string ResolveConnectionString(Func<string, string?>? getEnvironmentVariable = null)
        {
            var getter = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
            var env = getter("QLCTPT_TEST_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(env))
            {
                throw new InvalidOperationException(
                    "Environment variable 'QLCTPT_TEST_CONNECTION_STRING' is required for PostgreSQL integration tests. " +
                    "Tests fail closed when this variable is missing to prevent false-positive test results.");
            }

            return env;
        }

        public static void ValidateDatabaseSafety(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var dbName = builder.Database;

            if (string.IsNullOrWhiteSpace(dbName) ||
                (!dbName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) &&
                 !dbName.EndsWith("_integration_test", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"[SAFETY GUARD] Integration tests can ONLY run against a database ending with '_test' or '_integration_test'. Current target: '{dbName}'");
            }
        }
    }

    [CollectionDefinition("PostgreSqlCollection")]
    public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
    {
    }
}
