namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels
{
    /// <summary>
    /// DTO hiển thị thông tin phòng dạng Card trên sơ đồ phòng trực quan.
    /// </summary>
    public class PhongCardRes
    {
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; }
        public int TangLau { get; set; }
        public double GiaThue { get; set; }
        public double DienTich { get; set; }
        public int SoNguoiToiDa { get; set; }

        /// <summary>Trạng thái phòng: 0=Trống, 1=Đã thuê, 2=Bảo trì</summary>
        public int TrangThai { get; set; }
        public string TrangThaiText { get; set; }

        // Thông tin chi nhánh
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; }

        // Thông tin hợp đồng đang hoạt động (nếu có)
        public int? HopDongId { get; set; }
        public string TenNguoiThue { get; set; }
        public string SdtNguoiThue { get; set; }
        public DateTime? NgayHetHan { get; set; }

        // Cảnh báo
        /// <summary>Phòng có hóa đơn chưa thanh toán</summary>
        public bool CoNoTien { get; set; }
        /// <summary>Tổng tiền nợ chưa thu</summary>
        public double SoTienNo { get; set; }
        /// <summary>Hợp đồng hết hạn trong vòng 15 ngày tới</summary>
        public bool SapHetHan { get; set; }
        /// <summary>Số ngày còn lại của hợp đồng</summary>
        public int? SoNgayConLai { get; set; }
    }
}
