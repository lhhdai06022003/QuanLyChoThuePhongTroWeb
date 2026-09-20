using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases
{
    public interface IInvoiceReminderUseCase
    {
        Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
