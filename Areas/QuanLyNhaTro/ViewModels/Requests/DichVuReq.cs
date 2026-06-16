using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests
{
    public class DichVuReq
    {
        public int DichVuId { get; set; }

        [Required(ErrorMessage = "Tên dịch vụ không được để trống.")]
        [StringLength(100, ErrorMessage = "Tên dịch vụ tối đa 100 ký tự.")]
        public string TenDichVu { get; set; }

        [Required(ErrorMessage = "Đơn vị tính không được để trống.")]
        [StringLength(50, ErrorMessage = "Đơn vị tính tối đa 50 ký tự.")]
        public string DonVi { get; set; }

        public string GhiChu { get; set; }

        public bool MacDinh { get; set; } = false;
    }

    public class DichVuRes
    {
        public int DichVuId { get; set; }
        public string TenDichVu { get; set; }
        public string DonVi { get; set; }
        public string GhiChu { get; set; }
        public bool MacDinh { get; set; }
    }
}
