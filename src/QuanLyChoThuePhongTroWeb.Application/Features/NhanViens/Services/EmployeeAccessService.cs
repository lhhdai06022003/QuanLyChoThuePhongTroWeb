using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services
{
    public class EmployeeAccessService : IEmployeeAccessService
    {
        private readonly IEmployeeBranchStore _store;

        // Service đăng ký Scoped nên mỗi request có một instance: nhớ phân công của người dùng trong request để các lần
        // kiểm tra quyền/phạm vi sau không truy vấn lại (trước đây một trang có thể đọc 2-3 lần). Request kế tiếp đọc lại
        // từ DB nên thu hồi phân công hay khóa tài khoản vẫn có hiệu lực ngay ở request sau.
        private readonly Dictionary<int, EmployeeBranchActorDto?> _actors = new();

        public EmployeeAccessService(IEmployeeBranchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        private async Task<EmployeeBranchActorDto?> GetActorAsync(int actorId, CancellationToken cancellationToken)
        {
            if (_actors.TryGetValue(actorId, out var cached))
            {
                return cached;
            }

            var actor = await _store.GetActorWithActiveBranchesAsync(actorId, cancellationToken);
            _actors[actorId] = actor;
            return actor;
        }

        public async Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default)
        {
            if (actorId <= 0 || !EmployeeActionCodes.IsValid(action))
            {
                return false;
            }

            var actor = await GetActorAsync(actorId, cancellationToken);
            if (actor == null || !actor.IsActive)
            {
                return false;
            }

            // Admin is allowed on all branches for any valid action
            if (actor.Role == Role.Admin)
            {
                return true;
            }

            // Only Admin can perform Admin-only actions (chốt, trả lại hóa đơn)
            if (EmployeeActionCodes.IsAdminOnly(action))
            {
                return false;
            }

            // Staff can only perform staff actions if actively assigned to the specific branch
            if (actor.Role == Role.NhanVien)
            {
                if (chiNhanhId <= 0)
                {
                    return false;
                }

                return actor.ActiveBranchIds != null && actor.ActiveBranchIds.Contains(chiNhanhId);
            }

            // Other roles (KhachThue, KhachVangLai, etc.) cannot access employee actions
            return false;
        }

        public async Task<EmployeeAccessScope?> GetScopeAsync(int actorId, CancellationToken cancellationToken = default)
        {
            if (actorId <= 0)
            {
                return null;
            }

            var actor = await GetActorAsync(actorId, cancellationToken);
            if (actor == null || !actor.IsActive)
            {
                return null;
            }

            if (actor.Role == Role.Admin)
            {
                return new EmployeeAccessScope(actorId, isAdmin: true);
            }

            if (actor.Role == Role.NhanVien)
            {
                return new EmployeeAccessScope(actorId, isAdmin: false, actor.ActiveBranchIds);
            }

            return null;
        }
    }
}
