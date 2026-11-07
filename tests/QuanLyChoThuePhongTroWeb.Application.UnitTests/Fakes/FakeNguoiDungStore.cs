using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Kho tài khoản trong bộ nhớ cho NguoiDungService; điều kiện lọc giống NguoiDungStore (chưa xóa mềm).
    public class FakeNguoiDungStore : INguoiDungStore
    {
        public List<NguoiDung> Users { get; } = new();
        public List<NguoiDung> Added { get; } = new();
        public int UpdateCount { get; private set; }

        public Task<NguoiDung?> GetActiveByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(x => x.TenDangNhap == username && !x.IsDeleted));

        public Task<NguoiDung?> GetActiveWithTenantByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(x => x.NguoiDungId == id && !x.IsDeleted));

        public Task<IReadOnlyList<NguoiDung>> GetAllActiveWithTenantAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NguoiDung>>(Users.Where(x => !x.IsDeleted).ToList());

        public Task<bool> ExistsActiveUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(x => !x.IsDeleted && x.TenDangNhap == username));

        public Task<bool> ExistsActiveTenantAccountAsync(int nguoiThueId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(x => !x.IsDeleted && x.NguoiThueId == nguoiThueId));

        public Task<bool> ExistsActiveTenantAccountAsync(int nguoiThueId, int excludeUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(x => x.NguoiDungId != excludeUserId && !x.IsDeleted && x.NguoiThueId == nguoiThueId));

        public Task<NguoiDung?> GetSoftDeletedByTenantIdAsync(int nguoiThueId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(x => x.IsDeleted && x.NguoiThueId == nguoiThueId));

        public Task<NguoiDung?> GetSoftDeletedByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(x => x.IsDeleted && x.TenDangNhap == username));

        public Task<NguoiDung?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(x => x.NguoiDungId == id));

        public Task AddAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default)
        {
            if (nguoiDung.NguoiDungId == 0)
            {
                nguoiDung.NguoiDungId = Users.Count == 0 ? 1 : Users.Max(x => x.NguoiDungId) + 1;
            }

            Users.Add(nguoiDung);
            Added.Add(nguoiDung);
            return Task.CompletedTask;
        }

        public void Update(NguoiDung nguoiDung)
        {
            UpdateCount++;
        }
    }
}
