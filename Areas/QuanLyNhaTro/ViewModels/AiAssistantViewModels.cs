using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels.AiAssistant
{
    public class ChatRequest
    {
        public string Message { get; set; }
        public List<ChatMessageDto> History { get; set; } = new List<ChatMessageDto>();
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } // "user" hoặc "model"
        public string Message { get; set; }
    }

    public class PhongTroChuaChotRes
    {
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; }
        public int TangLau { get; set; }
        public string ChiNhanhTen { get; set; }
        public double GiaThue { get; set; }
    }

    public class HoaDonChuaThanhToanRes
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; }
        public string SoPhong { get; set; }
        public string KhachThueTen { get; set; }
        public double TongTien { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
    }

    public class PhongTrongRes
    {
        public string SoPhong { get; set; }
        public int TangLau { get; set; }
        public string ChiNhanhTen { get; set; }
        public double GiaThue { get; set; }
    }

    public class HopDongSapHetHanRes
    {
        public string SoPhong { get; set; }
        public string KhachThueTen { get; set; }
        public string NgayKetThuc { get; set; }
        public int SoNgayConLai { get; set; }
    }

    public class ThongTinKhachThueRes
    {
        public string SoPhong { get; set; }
        public string HoVaTen { get; set; }
        public string SoDienThoai { get; set; }
        public string TrangThai { get; set; }
    }

    public class DoanhThuChiNhanhRes
    {
        public string ChiNhanhTen { get; set; }
        public double TongDoanhThu { get; set; }
    }

    public class ChiSoDienNuocRes
    {
        public string SoPhong { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public double TieuThuDien { get; set; }
        public double TieuThuNuoc { get; set; }
    }
}
