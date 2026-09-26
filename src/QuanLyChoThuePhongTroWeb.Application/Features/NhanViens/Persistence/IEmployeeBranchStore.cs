using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence
{
    public interface IEmployeeBranchStore
    {
        Task<EmployeeBranchActorDto?> GetActorWithActiveBranchesAsync(int actorId, CancellationToken cancellationToken = default);
        Task<bool> HasActiveBranchAssignmentAsync(int actorId, int chiNhanhId, CancellationToken cancellationToken = default);
    }
}
