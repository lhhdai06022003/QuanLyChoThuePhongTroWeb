using QuanLyChoThuePhongTroWeb.Models;
using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests
{
    public class HopDongReq
    {
        public int HopDongId { get; set; }

        [Required(ErrorMessage = "Mã hợp đồng không được để trống.")]
        [StringLength(50, ErrorMessage = "Mã hợp đồng tối đa 50 ký tự.")]
        public string MaHopDong { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phòng trọ.")]
        public int PhongTroId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn người đại diện thuê phòng.")]
        public int NguoiThueId { get; set; }

        [Required(ErrorMessage = "Thời điểm bắt đầu không được để trống.")]
        public DateTime ThoiDiemBatDau { get; set; }

        public DateTime? ThoiDiemKetThuc { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tiền cọc phòng phải lớn hơn hoặc bằng 0.")]
        public double TienCocPhong { get; set; }

        [Range(1, double.MaxValue, ErrorMessage = "Tiền thuê phòng phải lớn hơn 0.")]
        public double TienThuePhong { get; set; }

        public TrangThaiHopDong TrangThaiHopDong { get; set; } = TrangThaiHopDong.DangHoatDong;

        // Danh sách những người ở ghép (nếu có)
        public List<int>? ThanhVienKhacIds { get; set; }
    }
}
