using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    /// <summary>
    /// Fake IEmployeeBranchStore riêng cho EmployeeBranchAssignmentServiceTests, tránh trùng
    /// tên với FakeEmployeeBranchStore private trong EmployeeAccessTests.
    /// </summary>
    public class FakeEmployeeBranchStoreForAssignment : IEmployeeBranchStore
    {
        public Dictionary<int, EmployeeBranchActorDto> Actors { get; } = new();

        public Task<EmployeeBranchActorDto?> GetActorWithActiveBranchesAsync(int actorId, CancellationToken cancellationToken = default)
        {
            Actors.TryGetValue(actorId, out var actor);
            return Task.FromResult(actor);
        }

        public Task<bool> HasActiveBranchAssignmentAsync(int actorId, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            if (Actors.TryGetValue(actorId, out var actor) && actor.IsActive)
            {
                return Task.FromResult(actor.ActiveBranchIds != null && actor.ActiveBranchIds.Contains(chiNhanhId));
            }
            return Task.FromResult(false);
        }
    }
}
