using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers;
using QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class StartupTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public StartupTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void ServiceProvider_ShouldResolve_ControllersAndDependencies()
        {
            using var scope = _factory.Services.CreateScope();
            var sp = scope.ServiceProvider;

            // Resolve sample controllers to verify dependency injection configuration
            var nguoiDungController = ActivatorUtilities.CreateInstance<QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers.NguoiDungController>(sp);
            Assert.NotNull(nguoiDungController);

            var phongTrosController = ActivatorUtilities.CreateInstance<QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Controllers.PhongTrosController>(sp);
            Assert.NotNull(phongTrosController);

            var lichSuThanhToanController = ActivatorUtilities.CreateInstance<QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers.LichSuThanhToanController>(sp);
            Assert.NotNull(lichSuThanhToanController);

            var hopDongKhachThueController = ActivatorUtilities.CreateInstance<QuanLyChoThuePhongTroWeb.Areas.KhachThue.Controllers.HopDongController>(sp);
            Assert.NotNull(hopDongKhachThueController);
        }

        [Fact]
        public void BackgroundJobs_ShouldNotBeRegistered_WhenDisabledInConfiguration()
        {
            // Automated assertion verifying that the 3 production background hosted jobs are NOT registered in test host
            var hostedServices = _factory.Services.GetServices<IHostedService>().ToList();

            Assert.DoesNotContain(hostedServices, s => s.GetType().Name == "ContractAutoCloseJob");
            Assert.DoesNotContain(hostedServices, s => s.GetType().Name == "ContractExpiryAlertJob");
            Assert.DoesNotContain(hostedServices, s => s.GetType().Name == "InvoiceReminderJob");
        }

        [Fact]
        public void ExternalServicesAndStores_ShouldResolveToTestDoubles_NotProductionImplementations()
        {
            using var scope = _factory.Services.CreateScope();
            var sp = scope.ServiceProvider;

            var emailService = sp.GetRequiredService<IEmailService>();
            var storageService = sp.GetRequiredService<IImageStorageService>();
            var invoiceStore = sp.GetRequiredService<IInvoiceReminderStateStore>();
            var contractStore = sp.GetRequiredService<IContractExpiryAlertStateStore>();

            Assert.IsType<CustomWebApplicationFactory.FakeEmailService>(emailService);
            Assert.IsType<CustomWebApplicationFactory.FakeImageStorageService>(storageService);
            Assert.IsType<CustomWebApplicationFactory.InMemoryInvoiceReminderStateStore>(invoiceStore);
            Assert.IsType<CustomWebApplicationFactory.InMemoryContractExpiryAlertStateStore>(contractStore);
        }

        [Fact]
        public void LogSink_ShouldValidateErrorLevels_AllowList_AndEnforceSuiteIntegrity()
        {
            // 1. Focused assertions for TestLogSink behavior:
            var testSink = new TestLogSink();

            // Sink không throw khi không có error (chỉ có Info và Warning)
            testSink.Add(Microsoft.Extensions.Logging.LogLevel.Information, "InfoCat", "Info msg", null);
            testSink.Add(Microsoft.Extensions.Logging.LogLevel.Warning, "WarnCat", "Warn msg", null);
            testSink.AssertNoUnexpectedErrors();

            // Sink throw khi có Error
            var errorSink = new TestLogSink();
            errorSink.Add(Microsoft.Extensions.Logging.LogLevel.Error, "ErrorCat", "Error msg", null);
            Assert.Throws<InvalidOperationException>(() => errorSink.AssertNoUnexpectedErrors());

            // Sink throw khi có Critical
            var criticalSink = new TestLogSink();
            criticalSink.Add(Microsoft.Extensions.Logging.LogLevel.Critical, "CritCat", "Critical msg", null);
            Assert.Throws<InvalidOperationException>(() => criticalSink.AssertNoUnexpectedErrors());

            // Allow-list chỉ loại đúng log được cho phép
            var allowedErrorSink = new TestLogSink();
            allowedErrorSink.Add(Microsoft.Extensions.Logging.LogLevel.Error, "AllowedCategory", "Harmless known error", null);
            allowedErrorSink.AssertNoUnexpectedErrors(entry => entry.Category == "AllowedCategory");

            // Allow-list không khớp vẫn throw
            Assert.Throws<InvalidOperationException>(() =>
                allowedErrorSink.AssertNoUnexpectedErrors(entry => entry.Category == "OtherCategory"));

            // 2. Integration assertion: Verify shared Web test host currently satisfies suite integrity
            _factory.AssertSuiteLogIntegrity();
        }

        [Fact]
        public void CustomWebApplicationFactory_Dispose_ShouldThrow_WhenErrorLogInSink()
        {
            var factory = new CustomWebApplicationFactory();
            factory.LogSink.Add(LogLevel.Error, "TestLifecycleCategory", "Simulated unallowed error", null);

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Dispose());
            Assert.Contains("Unallowed Error/Critical logs captured in Web test host", ex.Message);
        }

        [Fact]
        public async Task CustomWebApplicationFactory_DisposeAsync_ShouldThrow_WhenCriticalLogInSink()
        {
            var factory = new CustomWebApplicationFactory();
            factory.LogSink.Add(LogLevel.Critical, "TestLifecycleCategory", "Simulated critical failure", null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.DisposeAsync());
            Assert.Contains("Unallowed Error/Critical logs captured in Web test host", ex.Message);
        }

        [Theory]
        [InlineData("QuanLyChoThuePhongTroWeb.Web.BackgroundJobs.ContractAutoCloseJob")]
        [InlineData("QuanLyChoThuePhongTroWeb.Web.BackgroundJobs.ContractExpiryAlertJob")]
        [InlineData("QuanLyChoThuePhongTroWeb.Web.BackgroundJobs.InvoiceReminderJob")]
        [InlineData("QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases.InvoiceReminderUseCase")]
        [InlineData("QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases.ContractExpiryAlertUseCase")]
        [InlineData("QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases.ContractAutoCloseUseCase")]
        public void CustomWebApplicationFactory_Dispose_ShouldThrow_WhenBackgroundJobLogsAtInformation(string category)
        {
            var factory = new CustomWebApplicationFactory();
            factory.LogSink.Add(LogLevel.Information, category, "Job cycle executed successfully", null);

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Dispose());
            Assert.Contains("Background job activity detected in Web test host", ex.Message);
        }

        [Fact]
        public void CustomWebApplicationFactory_Dispose_ShouldSucceed_WhenNoErrorsOrBackgroundActivity()
        {
            var factory = new CustomWebApplicationFactory();
            factory.LogSink.Add(LogLevel.Information, "BenignCategory", "Benign information log", null);
            factory.LogSink.Add(LogLevel.Warning, "BenignCategory", "Benign warning log", null);

            var exception = Record.Exception(() => factory.Dispose());
            Assert.Null(exception);
        }

        [Fact]
        public async Task CustomWebApplicationFactory_DisposeAsync_ShouldSucceed_WhenNoErrorsOrBackgroundActivity()
        {
            var factory = new CustomWebApplicationFactory();
            factory.LogSink.Add(LogLevel.Information, "BenignCategory", "Benign information log", null);
            factory.LogSink.Add(LogLevel.Warning, "BenignCategory", "Benign warning log", null);

            var exception = await Record.ExceptionAsync(async () => await factory.DisposeAsync());
            Assert.Null(exception);
        }

        [Fact]
        public void TestLoggerProvider_ShouldCapture_InformationAndTraceLogs_DespiteGlobalWarningFilter()
        {
            using var scope = _factory.Services.CreateScope();
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("TestDiagnosticsCategory");

            logger.LogInformation("Diagnostic information log message for provider filter verification");
            logger.LogTrace("Diagnostic trace log message for provider filter verification");

            var infoEntry = _factory.LogSink.Entries
                .FirstOrDefault(e => e.Category == "TestDiagnosticsCategory" && e.Level == LogLevel.Information);
            var traceEntry = _factory.LogSink.Entries
                .FirstOrDefault(e => e.Category == "TestDiagnosticsCategory" && e.Level == LogLevel.Trace);

            Assert.NotNull(infoEntry);
            Assert.NotNull(traceEntry);
            Assert.Contains("Diagnostic information log message", infoEntry.Message);
            Assert.Contains("Diagnostic trace log message", traceEntry.Message);
        }
    }
}

