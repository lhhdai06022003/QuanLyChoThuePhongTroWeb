using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Mặc định mọi người thuê đều nhìn thấy được; đưa id vào Hidden để giả lập người thuê của chi nhánh khác.
    public class FakeTenantVisibilityStore : ITenantVisibilityStore
    {
        public HashSet<int> Hidden { get; } = new();

        public Task<bool> AreAllVisibleAsync(IReadOnlyCollection<int> nguoiThueIds, IReadOnlyCollection<int> allowedBranchIds, CancellationToken cancellationToken = default)
            => Task.FromResult(!nguoiThueIds.Any(Hidden.Contains));
    }
}
