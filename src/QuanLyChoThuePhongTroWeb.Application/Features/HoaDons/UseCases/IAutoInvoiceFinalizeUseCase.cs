using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.UseCases
{
    public interface IAutoInvoiceFinalizeUseCase
    {
        // Trả về số hóa đơn đã tự chốt.
        Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
