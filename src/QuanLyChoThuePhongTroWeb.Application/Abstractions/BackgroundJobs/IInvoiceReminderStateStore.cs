using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.BackgroundJobs
{
    public interface IInvoiceReminderStateStore
    {
        Task<IReadOnlySet<int>> GetSentInvoiceIdsAsync(CancellationToken cancellationToken = default);
        Task<bool> HasBeenSentAsync(int hoaDonId, CancellationToken cancellationToken = default);
        Task MarkAsSentAsync(int hoaDonId, CancellationToken cancellationToken = default);
    }
}
