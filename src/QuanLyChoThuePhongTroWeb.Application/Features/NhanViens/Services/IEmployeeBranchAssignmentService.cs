using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Services
{
    public interface IEmployeeBranchAssignmentService
    {
        Task<ServiceResult<IReadOnlyList<EmployeeBranchSummaryDto>>> GetEmployeesAsync(int? chiNhanhId, int actorId, CancellationToken ct = default);
        Task<ServiceResult<EmployeeBranchDetailDto>> GetEmployeeAsync(int nguoiDungId, int actorId, CancellationToken ct = default);
        Task<ServiceResult<IReadOnlyList<BranchOptionDto>>> GetBranchOptionsAsync(int actorId, CancellationToken ct = default);
        Task<ServiceResult> AssignAsync(AssignEmployeeBranchRequest request, int actorId, CancellationToken ct = default);
        Task<ServiceResult> RevokeAsync(RevokeEmployeeBranchRequest request, int actorId, CancellationToken ct = default);
    }
}
