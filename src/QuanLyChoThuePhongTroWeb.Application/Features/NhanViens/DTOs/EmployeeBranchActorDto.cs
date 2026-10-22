using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NhanViens.DTOs
{
    public class EmployeeBranchActorDto
    {
        public int NguoiDungId { get; set; }
        public Role Role { get; set; }
        public bool IsActive { get; set; }
        public IReadOnlyList<int> ActiveBranchIds { get; set; } = new List<int>();
    }
}
