using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Security
{
    public interface IEmployeeAccessService
    {
        Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default);
        Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default);
    }
}
