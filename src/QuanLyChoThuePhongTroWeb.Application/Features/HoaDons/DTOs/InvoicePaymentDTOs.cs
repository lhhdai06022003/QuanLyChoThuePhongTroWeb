using System;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public class YeuCauThanhToanRes
    {
        public int YeuCauThanhToanHoaDonId { get; set; }
        public int HoaDonId { get; set; }
        public string MaYeuCau { get; set; } = string.Empty;
        public decimal SoTien { get; set; }
        public string NoiDungChuyenKhoan { get; set; } = string.Empty;
        public DateTime? HanThanhToan { get; set; }
        public TrangThaiYeuCauThanhToan TrangThai { get; set; }
        public DateTime NgayTao { get; set; }
    }

    public class MinhChungThanhToanRes
    {
        public int MinhChungThanhToanHoaDonId { get; set; }
        public int YeuCauThanhToanHoaDonId { get; set; }
        public string HinhAnhUrl { get; set; } = string.Empty;
        public string? PublicId { get; set; }
        public string? MaGiaoDichNganHang { get; set; }
        public decimal SoTienKhaiBao { get; set; }
        public DateTime NgayChuyenKhaiBao { get; set; }
        public TrangThaiMinhChungThanhToan TrangThaiDoiChieu { get; set; }
        public string? LyDoTuChoi { get; set; }
    }

    public class XacNhanThanhToanRes
    {
        public int LichSuThanhToanId { get; set; }
        public int HoaDonId { get; set; }
        public string MaGiaoDich { get; set; } = string.Empty;
        public decimal SoTienThanhToan { get; set; }
        public int? MinhChungThanhToanHoaDonId { get; set; }
        public TrangThaiHoaDon TrangThaiHoaDon { get; set; }
        public DateTime NgayThanhToan { get; set; }
    }
}
