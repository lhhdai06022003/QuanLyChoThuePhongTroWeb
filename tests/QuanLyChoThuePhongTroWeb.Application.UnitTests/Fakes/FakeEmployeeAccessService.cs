using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeEmployeeAccessService : IEmployeeAccessService
    {
        public HashSet<(int ActorId, int BranchId, string Action)> Permissions { get; } = new();
        public EmployeeAccessScope? ScopeToReturn { get; set; }

        public Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Permissions.Contains((actorId, chiNhanhId, action)));
        }

        public Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ScopeToReturn);
        }
    }
}
