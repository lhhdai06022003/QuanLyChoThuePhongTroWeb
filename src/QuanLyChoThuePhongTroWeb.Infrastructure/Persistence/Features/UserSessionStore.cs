using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class UserSessionStore : IUserSessionStore
    {
        private readonly ApplicationDbContext _context;

        public UserSessionStore(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserSessionSnapshotDto?> GetSessionSnapshotAsync(int nguoiDungId, CancellationToken ct = default)
        {
            return await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.NguoiDungId == nguoiDungId)
                .Select(u => new UserSessionSnapshotDto
                {
                    NguoiDungId = u.NguoiDungId,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    IsDeleted = u.IsDeleted
                })
                .FirstOrDefaultAsync(ct);
        }
    }
}
