using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs
{
    public interface IContractExpiryAlertStateStore
    {
        Task<IReadOnlySet<string>> GetSentAlertKeysAsync(CancellationToken cancellationToken = default);
        Task<bool> HasBeenSentAsync(string alertKey, CancellationToken cancellationToken = default);
        Task MarkAsSentAsync(string alertKey, CancellationToken cancellationToken = default);
    }
}
