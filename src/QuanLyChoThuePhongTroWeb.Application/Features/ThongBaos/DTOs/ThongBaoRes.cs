using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThongBaos.DTOs
{
    public class ThongBaoRes
    {
        public int Id { get; set; }
        public int? NguoiDungId { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string NoiDung { get; set; } = string.Empty;
        public string? LinhVuc { get; set; }
        public string? LinkDieuHuong { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
