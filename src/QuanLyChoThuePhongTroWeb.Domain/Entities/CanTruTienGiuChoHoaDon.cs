using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Domain.Entities;

[Table("can_tru_tien_giu_cho_hoa_don")]
public sealed class CanTruTienGiuChoHoaDon
{
    public int CanTruTienGiuChoHoaDonId { get; set; }
    public int ApDungTienGiuChoVaoTienCocId { get; set; }
    public ApDungTienGiuChoVaoTienCoc ApDungTienGiuChoVaoTienCoc { get; set; } = null!;
    public int HoaDonId { get; set; }
    public HoaDon HoaDon { get; set; } = null!;
    public decimal SoTienCanTru { get; set; }
    public DateTime NgayCanTru { get; set; } = DateTime.UtcNow;
}
