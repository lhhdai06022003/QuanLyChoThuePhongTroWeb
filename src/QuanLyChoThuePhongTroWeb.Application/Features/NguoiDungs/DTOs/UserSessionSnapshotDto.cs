using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs
{
    public class UserSessionSnapshotDto
    {
        public int NguoiDungId { get; set; }
        public Role Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
    }
}
