using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    // Gửi email nhắc hóa đơn quá hạn mỗi sáng 8:00 (giờ VN); mốc nhắc do InvoiceReminderUseCase quyết định.
    public class InvoiceReminderJob : DailyBackgroundJob
    {
        public InvoiceReminderJob(
            IServiceScopeFactory scopeFactory,
            TimeProvider timeProvider,
            ILogger<InvoiceReminderJob> logger)
            : base(scopeFactory, timeProvider, logger)
        {
        }

        protected override string TenJob => "InvoiceReminderJob (nhắc hóa đơn quá hạn)";

        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
            => services.GetRequiredService<IInvoiceReminderUseCase>().ExecuteAsync(cancellationToken);
    }
}
