using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Requests
{
    /// <summary>
    /// Thông tin một dịch vụ được chọn đăng ký cho phòng.
    /// </summary>
    public class DangKyDichVuItem
    {
        public int DichVuChiNhanhId { get; set; }
        public bool IsSelected { get; set; }
        public int SoLuong { get; set; } = 1;
        public DateTime? NgayBatDau { get; set; }
    }

    /// <summary>
    /// Request body gửi lên để lưu danh sách đăng ký dịch vụ của một phòng.
    /// </summary>
    public class DangKyDichVuReq
    {
        [Required(ErrorMessage = "Vui lòng chọn phòng trọ.")]
        public int PhongTroId { get; set; }

        public List<DangKyDichVuItem> DichVus { get; set; } = new();
    }

    /// <summary>
    /// Response ViewModel: thông tin dịch vụ chi nhánh + trạng thái đăng ký hiện tại của phòng.
    /// Dùng để render UI Partial View.
    /// </summary>
    public class DichVuChiNhanhWithDangKyRes
    {
        public int DichVuChiNhanhId { get; set; }
        public string TenDichVu { get; set; }
        public string DonVi { get; set; }
        public double GiaDichVu { get; set; }

        /// <summary>Dịch vụ mặc định (Điện, Nước) — auto-tick, ẩn ô số lượng</summary>
        public bool MacDinh { get; set; }

        // Trạng thái đăng ký hiện tại (nếu đã đăng ký)
        public bool IsSelected { get; set; }
        public int SoLuong { get; set; } = 1;
        public int? DangKyDichVuId { get; set; } // null nếu chưa đăng ký
        public DateTime? NgayBatDau { get; set; }
    }
}
