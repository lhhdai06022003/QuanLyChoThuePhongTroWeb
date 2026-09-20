using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ChiNhanhs.Persistence
{
    public interface IChiNhanhStore
    {
        Task<IReadOnlyList<ChiNhanh>> GetAllActiveAsync(CancellationToken cancellationToken = default);
        Task<ChiNhanh?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsMaAsync(string maChiNhanh, int excludeId = 0, CancellationToken cancellationToken = default);
        void Add(ChiNhanh chiNhanh);
        void Update(ChiNhanh chiNhanh);
    }
}
