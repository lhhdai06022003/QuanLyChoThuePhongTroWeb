using System;
using QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.Persistence
{
    public class PostgreSqlConfigurationTests
    {
        [Theory]
        [InlineData("Host=localhost;Database=production_db;Username=postgres")]
        [InlineData("Host=localhost;Database=QuanLyPhongTroDb;Username=postgres")]
        [InlineData("Host=localhost;Database=testing_app;Username=postgres")]
        [InlineData("Host=localhost;Database=;Username=postgres")]
        public void SafetyGuard_ShouldReject_NonTestDatabaseNames(string invalidConnectionString)
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                PostgreSqlFixture.ValidateDatabaseSafety(invalidConnectionString);
            });

            Assert.Contains("[SAFETY GUARD]", ex.Message);
        }

        [Theory]
        [InlineData("Host=localhost;Database=quanlyphongtro_test;Username=postgres")]
        [InlineData("Host=localhost;Database=quanlyphongtro_integration_test;Username=postgres")]
        [InlineData("Host=localhost;Database=MY_APP_TEST;Username=postgres")]
        public void SafetyGuard_ShouldAccept_TestDatabaseNames(string validConnectionString)
        {
            // Safety guard should accept valid database names without throwing
            PostgreSqlFixture.ValidateDatabaseSafety(validConnectionString);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveConnectionString_Throws_WhenEnvironmentVariableIsMissingOrEmpty(string? missingOrEmptyValue)
        {
            // Pure function test: supply a custom getter that returns missing/empty value
            // Zero mutation to process-global Environment variables, completely race-free for parallel test runs
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = PostgreSqlFixture.ResolveConnectionString(_ => missingOrEmptyValue);
            });

            Assert.Contains("QLCTPT_TEST_CONNECTION_STRING", ex.Message);
        }

        [Fact]
        public void ResolveConnectionString_ReturnsValue_WhenEnvironmentVariableIsPresent()
        {
            const string expected = "Host=localhost;Database=isolated_test;Username=postgres";
            var actual = PostgreSqlFixture.ResolveConnectionString(key => key == "QLCTPT_TEST_CONNECTION_STRING" ? expected : null);

            Assert.Equal(expected, actual);
        }
    }
}

