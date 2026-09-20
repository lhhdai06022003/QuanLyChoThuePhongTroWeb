using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence
{
    public interface INguoiDungStore
    {
        Task<NguoiDung?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<NguoiDung?> GetActiveWithTenantByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NguoiDung>> GetAllActiveWithTenantAsync(CancellationToken cancellationToken = default);
        Task<bool> ExistsActiveUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<bool> ExistsActiveTenantAccountAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<bool> ExistsActiveTenantAccountAsync(int nguoiThueId, int excludeUserId, CancellationToken cancellationToken = default);
        Task<NguoiDung?> GetSoftDeletedByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<NguoiDung?> GetSoftDeletedByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<NguoiDung?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task AddAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default);
        void Update(NguoiDung nguoiDung);
    }
}
