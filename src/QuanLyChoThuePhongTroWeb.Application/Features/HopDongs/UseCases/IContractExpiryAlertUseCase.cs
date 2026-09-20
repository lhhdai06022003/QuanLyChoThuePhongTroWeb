using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.UseCases
{
    public interface IContractExpiryAlertUseCase
    {
        Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
