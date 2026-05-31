namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.Responses
{
    public class ThanhVienHopDongRes
    {
        public int ChiTietThanhVienHopDongId { get; set; }
        public int NguoiThueId { get; set; }
        public string HoVaTen { get; set; }
        public string SoDienThoai { get; set; }
        public string CCCD { get; set; }
        public DateTime NgayVao { get; set; }
        public DateTime? NgayChuyenDi { get; set; }
        public bool IsChuHopDong { get; set; } // Để biết ai là người đứng tên
    }
}
