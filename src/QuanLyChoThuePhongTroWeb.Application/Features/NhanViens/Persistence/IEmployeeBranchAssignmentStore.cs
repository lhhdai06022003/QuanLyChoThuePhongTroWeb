using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence
{
    public interface IEmployeeBranchAssignmentStore
    {
        // SELECT * FROM nguoi_dung WHERE "NguoiDungId" = {id} FOR UPDATE; AsNoTracking; trả cả tài khoản đã xóa mềm (IsDeleted = true); null nếu không có hàng
        Task<EmployeeAccountSnapshotDto?> LockEmployeeAsync(int nguoiDungId, CancellationToken ct = default);

        // chi_nhanh tồn tại và !IsDeleted
        Task<bool> IsBranchAssignableAsync(int chiNhanhId, CancellationToken ct = default);

        // TRACKED, theo cặp (NguoiDungId, ChiNhanhId), không lọc IsActive
        Task<NhanVienChiNhanh?> GetAssignmentAsync(int nguoiDungId, int chiNhanhId, CancellationToken ct = default);

        Task AddAssignmentAsync(NhanVienChiNhanh assignment, CancellationToken ct = default);

        // NhanVien chưa xóa (gồm tài khoản khóa); chiNhanhId có giá trị => chỉ nhân viên có dòng đang hiệu lực tại chi nhánh đó; sắp xếp TenDangNhap
        Task<IReadOnlyList<EmployeeBranchSummaryDto>> GetEmployeeSummariesAsync(int? chiNhanhId, CancellationToken ct = default);

        // null nếu không phải NhanVien chưa xóa; một dòng cho mỗi chi nhánh chưa xóa + chi nhánh đã xóa còn dòng phân công; sắp ChiNhanhDaXoa rồi TenChiNhanh
        Task<EmployeeBranchDetailDto?> GetEmployeeDetailAsync(int nguoiDungId, CancellationToken ct = default);

        // chi nhánh chưa xóa, sắp TenChiNhanh
        Task<IReadOnlyList<BranchOptionDto>> GetBranchOptionsAsync(CancellationToken ct = default);
    }
}
