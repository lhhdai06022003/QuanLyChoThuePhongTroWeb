using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs
{
    // Cảnh báo hợp đồng sắp hết hạn (email) và đã hết hạn (thông báo trong app) mỗi sáng 8:00 (giờ VN).
    public class ContractExpiryAlertJob : DailyBackgroundJob
    {
        public ContractExpiryAlertJob(
            IServiceScopeFactory scopeFactory,
            TimeProvider timeProvider,
            ILogger<ContractExpiryAlertJob> logger)
            : base(scopeFactory, timeProvider, logger)
        {
        }

        protected override string TenJob => "ContractExpiryAlertJob (cảnh báo hợp đồng hết hạn)";

        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
            => services.GetRequiredService<IContractExpiryAlertUseCase>().ExecuteAsync(cancellationToken);
    }
}
