using QuanLyChoThuePhongTroWeb.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    public class HopDongRes
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; }
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; }
        public string TenChiNhanh { get; set; }

        public int NguoiThueId { get; set; }
        public string TenNguoiThue { get; set; }

        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public double TienCocPhong { get; set; }
        public double TienThuePhong { get; set; }
        public TrangThaiHopDong TrangThaiHopDong { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCapNhat { get; set; }
    }
}
