using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features
{
    public class EmployeeBranchAssignmentStore : IEmployeeBranchAssignmentStore
    {
        private readonly ApplicationDbContext _context;

        public EmployeeBranchAssignmentStore(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<EmployeeAccountSnapshotDto?> LockEmployeeAsync(int nguoiDungId, CancellationToken ct = default)
        {
            var rows = await _context.NguoiDungs
                .FromSqlInterpolated($"SELECT * FROM nguoi_dung WHERE \"NguoiDungId\" = {nguoiDungId} FOR UPDATE")
                .AsNoTracking()
                .Select(u => new EmployeeAccountSnapshotDto { NguoiDungId = u.NguoiDungId, Role = u.Role, IsActive = u.IsActive, IsDeleted = u.IsDeleted })
                .ToListAsync(ct);
            return rows.FirstOrDefault();
        }

        public Task<bool> IsBranchAssignableAsync(int chiNhanhId, CancellationToken ct = default)
        {
            return _context.ChiNhanhs.AsNoTracking().AnyAsync(c => c.ChiNhanhId == chiNhanhId && !c.IsDeleted, ct);
        }

        public Task<NhanVienChiNhanh?> GetAssignmentAsync(int nguoiDungId, int chiNhanhId, CancellationToken ct = default)
        {
            return _context.NhanVienChiNhanhs.FirstOrDefaultAsync(x => x.NguoiDungId == nguoiDungId && x.ChiNhanhId == chiNhanhId, ct);
        }

        public async Task AddAssignmentAsync(NhanVienChiNhanh assignment, CancellationToken ct = default)
        {
            await _context.NhanVienChiNhanhs.AddAsync(assignment, ct);
        }

        private sealed class AssignmentRow
        {
            public int NguoiDungId { get; set; }
            public int ChiNhanhId { get; set; }
            public bool IsActive { get; set; }
            public DateTime NgayPhanCong { get; set; }
            public string? NguoiPhanCong { get; set; }
            public DateTime? NgayThuHoi { get; set; }
            public string? NguoiThuHoi { get; set; }
            public string? LyDo { get; set; }
            public string ChiNhanhMaChiNhanh { get; set; } = string.Empty;
            public string ChiNhanhTenChiNhanh { get; set; } = string.Empty;
            public bool ChiNhanhIsDeleted { get; set; }
        }

        public async Task<IReadOnlyList<EmployeeBranchSummaryDto>> GetEmployeeSummariesAsync(int? chiNhanhId, CancellationToken ct = default)
        {
            var employeesQuery = _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.Role == Role.NhanVien && !u.IsDeleted);

            if (chiNhanhId.HasValue)
            {
                employeesQuery = employeesQuery.Where(u => u.NhanVienChiNhanhs.Any(nv => nv.ChiNhanhId == chiNhanhId.Value && nv.IsActive && nv.NgayThuHoi == null));
            }

            var employees = await employeesQuery
                .OrderBy(u => u.TenDangNhap)
                .Select(u => new { u.NguoiDungId, u.TenDangNhap, u.IsActive })
                .ToListAsync(ct);

            var employeeIds = employees.Select(e => e.NguoiDungId).ToList();

            var assignments = await _context.NhanVienChiNhanhs
                .AsNoTracking()
                .Where(nv => employeeIds.Contains(nv.NguoiDungId))
                .Select(nv => new AssignmentRow
                {
                    NguoiDungId = nv.NguoiDungId,
                    ChiNhanhId = nv.ChiNhanhId,
                    IsActive = nv.IsActive,
                    NgayPhanCong = nv.NgayPhanCong,
                    NgayThuHoi = nv.NgayThuHoi,
                    ChiNhanhMaChiNhanh = nv.ChiNhanh.MaChiNhanh,
                    ChiNhanhTenChiNhanh = nv.ChiNhanh.TenChiNhanh
                })
                .ToListAsync(ct);

            var assignmentsByEmployee = assignments.GroupBy(a => a.NguoiDungId).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<EmployeeBranchSummaryDto>();
            foreach (var emp in employees)
            {
                assignmentsByEmployee.TryGetValue(emp.NguoiDungId, out var rows);
                rows ??= new List<AssignmentRow>();

                var activeBranches = rows
                    .Where(r => r.IsActive && r.NgayThuHoi == null)
                    .Select(r => new EmployeeBranchRefDto { ChiNhanhId = r.ChiNhanhId, MaChiNhanh = r.ChiNhanhMaChiNhanh, TenChiNhanh = r.ChiNhanhTenChiNhanh })
                    .ToList();

                DateTime? lastChange = null;
                foreach (var r in rows)
                {
                    if (lastChange == null || r.NgayPhanCong > lastChange)
                    {
                        lastChange = r.NgayPhanCong;
                    }
                    if (r.NgayThuHoi.HasValue && (lastChange == null || r.NgayThuHoi.Value > lastChange))
                    {
                        lastChange = r.NgayThuHoi.Value;
                    }
                }

                result.Add(new EmployeeBranchSummaryDto
                {
                    NguoiDungId = emp.NguoiDungId,
                    TenDangNhap = emp.TenDangNhap,
                    IsActive = emp.IsActive,
                    ChiNhanhDangPhuTrach = activeBranches,
                    LanThayDoiGanNhatUtc = lastChange
                });
            }

            return result;
        }

        public async Task<EmployeeBranchDetailDto?> GetEmployeeDetailAsync(int nguoiDungId, CancellationToken ct = default)
        {
            var employee = await _context.NguoiDungs
                .AsNoTracking()
                .Where(u => u.NguoiDungId == nguoiDungId && u.Role == Role.NhanVien && !u.IsDeleted)
                .Select(u => new { u.NguoiDungId, u.TenDangNhap, u.IsActive })
                .FirstOrDefaultAsync(ct);

            if (employee == null)
            {
                return null;
            }

            var branches = await _context.ChiNhanhs
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Select(c => new { c.ChiNhanhId, c.MaChiNhanh, c.TenChiNhanh })
                .ToListAsync(ct);

            var assignments = await _context.NhanVienChiNhanhs
                .AsNoTracking()
                .Where(nv => nv.NguoiDungId == nguoiDungId)
                .Select(nv => new AssignmentRow
                {
                    ChiNhanhId = nv.ChiNhanhId,
                    IsActive = nv.IsActive,
                    NgayPhanCong = nv.NgayPhanCong,
                    NguoiPhanCong = nv.NguoiPhanCong.TenDangNhap,
                    NgayThuHoi = nv.NgayThuHoi,
                    NguoiThuHoi = nv.NguoiThuHoi != null ? nv.NguoiThuHoi.TenDangNhap : null,
                    LyDo = nv.LyDo,
                    ChiNhanhMaChiNhanh = nv.ChiNhanh.MaChiNhanh,
                    ChiNhanhTenChiNhanh = nv.ChiNhanh.TenChiNhanh,
                    ChiNhanhIsDeleted = nv.ChiNhanh.IsDeleted
                })
                .ToListAsync(ct);

            var assignmentByBranch = assignments.ToDictionary(a => a.ChiNhanhId);

            var rows = new List<EmployeeBranchRowDto>();

            foreach (var branch in branches)
            {
                assignmentByBranch.TryGetValue(branch.ChiNhanhId, out var a);
                rows.Add(BuildRow(branch.ChiNhanhId, branch.MaChiNhanh, branch.TenChiNhanh, false, a));
            }

            foreach (var a in assignments.Where(x => x.ChiNhanhIsDeleted))
            {
                rows.Add(BuildRow(a.ChiNhanhId, a.ChiNhanhMaChiNhanh, a.ChiNhanhTenChiNhanh, true, a));
            }

            var orderedRows = rows.OrderBy(r => r.ChiNhanhDaXoa).ThenBy(r => r.TenChiNhanh).ToList();

            return new EmployeeBranchDetailDto
            {
                NguoiDungId = employee.NguoiDungId,
                TenDangNhap = employee.TenDangNhap,
                IsActive = employee.IsActive,
                ChiNhanhs = orderedRows
            };
        }

        private static EmployeeBranchRowDto BuildRow(int chiNhanhId, string maChiNhanh, string tenChiNhanh, bool chiNhanhDaXoa, AssignmentRow? assignment)
        {
            if (assignment == null)
            {
                return new EmployeeBranchRowDto
                {
                    ChiNhanhId = chiNhanhId,
                    MaChiNhanh = maChiNhanh,
                    TenChiNhanh = tenChiNhanh,
                    ChiNhanhDaXoa = chiNhanhDaXoa,
                    TrangThai = EmployeeBranchState.ChuaPhanCong
                };
            }

            var state = assignment.IsActive && assignment.NgayThuHoi == null
                ? EmployeeBranchState.DangPhuTrach
                : EmployeeBranchState.DaThuHoi;

            return new EmployeeBranchRowDto
            {
                ChiNhanhId = chiNhanhId,
                MaChiNhanh = maChiNhanh,
                TenChiNhanh = tenChiNhanh,
                ChiNhanhDaXoa = chiNhanhDaXoa,
                TrangThai = state,
                NgayPhanCongUtc = assignment.NgayPhanCong,
                NguoiPhanCong = assignment.NguoiPhanCong,
                NgayThuHoiUtc = assignment.NgayThuHoi,
                NguoiThuHoi = assignment.NguoiThuHoi,
                LyDo = assignment.LyDo
            };
        }

        public async Task<IReadOnlyList<BranchOptionDto>> GetBranchOptionsAsync(CancellationToken ct = default)
        {
            return await _context.ChiNhanhs
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.TenChiNhanh)
                .Select(c => new BranchOptionDto { ChiNhanhId = c.ChiNhanhId, MaChiNhanh = c.MaChiNhanh, TenChiNhanh = c.TenChiNhanh })
                .ToListAsync(ct);
        }
    }
}
