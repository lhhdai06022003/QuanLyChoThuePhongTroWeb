using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services
{
    public class EmployeeAccessService : IEmployeeAccessService
    {
        private readonly IEmployeeBranchStore _store;

        public EmployeeAccessService(IEmployeeBranchStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<bool> CanPerformAsync(int actorId, int chiNhanhId, string action, CancellationToken cancellationToken = default)
        {
            if (actorId <= 0 || !EmployeeActionCodes.IsValid(action))
            {
                return false;
            }

            var actor = await _store.GetActorWithActiveBranchesAsync(actorId, cancellationToken);
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

            var actor = await _store.GetActorWithActiveBranchesAsync(actorId, cancellationToken);
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
