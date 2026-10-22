using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class EmployeeBranchStore : IEmployeeBranchStore
    {
        private readonly ApplicationDbContext _context;

        public EmployeeBranchStore(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<EmployeeBranchActorDto?> GetActorWithActiveBranchesAsync(int actorId, CancellationToken cancellationToken = default)
        {
            if (actorId <= 0)
            {
                return null;
            }

            return await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.NguoiDungId == actorId && !u.IsDeleted && u.IsActive)
                .Select(u => new EmployeeBranchActorDto
                {
                    NguoiDungId = u.NguoiDungId,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    ActiveBranchIds = u.NhanVienChiNhanhs
                        .Where(nv => nv.IsActive && nv.NgayThuHoi == null)
                        .Select(nv => nv.ChiNhanhId)
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> HasActiveBranchAssignmentAsync(int actorId, int chiNhanhId, CancellationToken cancellationToken = default)
        {
            if (actorId <= 0 || chiNhanhId <= 0)
            {
                return false;
            }

            return await _context.NhanVienChiNhanhs
                .AsNoTracking()
                .AnyAsync(nv => nv.NguoiDungId == actorId &&
                                nv.ChiNhanhId == chiNhanhId &&
                                nv.IsActive &&
                                nv.NgayThuHoi == null &&
                                nv.NguoiDung.IsActive &&
                                !nv.NguoiDung.IsDeleted &&
                                nv.NguoiDung.Role == Role.NhanVien, cancellationToken);
        }
    }
}
