using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests
{
    public class DichVuChiNhanhReq
    {
        public int DichVuChiNhanhId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chi nhánh.")]
        public int ChiNhanhId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn dịch vụ.")]
        public int DichVuId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá dịch vụ phải lớn hơn hoặc bằng 0.")]
        public double GiaDichVu { get; set; }
    }

    public class DichVuChiNhanhRes
    {
        public int DichVuChiNhanhId { get; set; }
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; }
        public int DichVuId { get; set; }
        public string TenDichVu { get; set; }
        public double GiaDichVu { get; set; }
    }
}
