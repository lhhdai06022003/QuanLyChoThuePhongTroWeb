using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public class ChotDienNuocReq
    {
        public int ChiNhanhId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public List<DienNuocPhongReq> DanhSachPhong { get; set; } = new List<DienNuocPhongReq>();
    }

    public class DienNuocPhongReq
    {
        public int DichVuDienNuocCuaPhongId { get; set; }
        public int PhongTroId { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Chỉ số điện mới không hợp lệ.")]
        public double ChiSoDienMoi { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Chỉ số nước mới không hợp lệ.")]
        public double ChiSoNuocMoi { get; set; }
        
        public double ChiSoDienCu { get; set; }
        public double ChiSoNuocCu { get; set; }
    }
}
