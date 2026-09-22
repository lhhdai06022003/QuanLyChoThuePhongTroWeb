using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs
{
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessageDto> History { get; set; } = new();
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } = string.Empty; // "user" hoặc "model"
        public string Message { get; set; } = string.Empty;
    }

    public class PhongTroChuaChotRes
    {
        public int PhongTroId { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public int TangLau { get; set; }
        public string ChiNhanhTen { get; set; } = string.Empty;
        public decimal GiaThue { get; set; }
    }

    public class HoaDonChuaThanhToanRes
    {
        public int HoaDonId { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public string SoPhong { get; set; } = string.Empty;
        public string KhachThueTen { get; set; } = string.Empty;
        public decimal TongTien { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
    }

    public class PhongTrongRes
    {
        public string SoPhong { get; set; } = string.Empty;
        public int TangLau { get; set; }
        public string ChiNhanhTen { get; set; } = string.Empty;
        public decimal GiaThue { get; set; }
    }

    public class HopDongSapHetHanRes
    {
        public string SoPhong { get; set; } = string.Empty;
        public string KhachThueTen { get; set; } = string.Empty;
        public string NgayKetThuc { get; set; } = string.Empty;
        public int SoNgayConLai { get; set; }
    }

    public class ThongTinKhachThueRes
    {
        public string SoPhong { get; set; } = string.Empty;
        public string HoVaTen { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
        public string TrangThai { get; set; } = string.Empty;
    }

    public class DoanhThuChiNhanhRes
    {
        public string ChiNhanhTen { get; set; } = string.Empty;
        public decimal TongDoanhThu { get; set; }
    }

    public class ChiSoDienNuocRes
    {
        public string SoPhong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal TieuThuDien { get; set; }
        public decimal TieuThuNuoc { get; set; }
    }
}
