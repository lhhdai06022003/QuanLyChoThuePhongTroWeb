using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs
{
    public enum EmployeeBranchState
    {
        ChuaPhanCong = 0,
        DangPhuTrach = 1,
        DaThuHoi = 2
    }

    public class EmployeeBranchRefDto
    {
        public int ChiNhanhId { get; set; }
        public string MaChiNhanh { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
    }

    public class EmployeeBranchSummaryDto
    {
        public int NguoiDungId { get; set; }
        public string TenDangNhap { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public IReadOnlyList<EmployeeBranchRefDto> ChiNhanhDangPhuTrach { get; set; } = new List<EmployeeBranchRefDto>();
        public DateTime? LanThayDoiGanNhatUtc { get; set; }
    }

    public class EmployeeBranchRowDto
    {
        public int ChiNhanhId { get; set; }
        public string MaChiNhanh { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public bool ChiNhanhDaXoa { get; set; }
        public EmployeeBranchState TrangThai { get; set; }
        public DateTime? NgayPhanCongUtc { get; set; }
        public string? NguoiPhanCong { get; set; }
        public DateTime? NgayThuHoiUtc { get; set; }
        public string? NguoiThuHoi { get; set; }
        public string? LyDo { get; set; }
    }

    public class EmployeeBranchDetailDto
    {
        public int NguoiDungId { get; set; }
        public string TenDangNhap { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public IReadOnlyList<EmployeeBranchRowDto> ChiNhanhs { get; set; } = new List<EmployeeBranchRowDto>();
    }

    public class BranchOptionDto
    {
        public int ChiNhanhId { get; set; }
        public string MaChiNhanh { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
    }

    public class AssignEmployeeBranchRequest
    {
        public int NguoiDungId { get; set; }
        public int ChiNhanhId { get; set; }
        public string? LyDo { get; set; }
    }

    public class RevokeEmployeeBranchRequest
    {
        public int NguoiDungId { get; set; }
        public int ChiNhanhId { get; set; }
        public string LyDo { get; set; } = string.Empty;
    }

    public class EmployeeAccountSnapshotDto
    {
        public int NguoiDungId { get; set; }
        public Role Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
    }
}
