using System;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence
{
    // ConNo = tổng (TongTien - sổ chưa xóa) của hóa đơn DaGui chưa xóa còn dương.
    public sealed class LichSuThueRow
    {
        public int HopDongId { get; set; }
        public string MaHopDong { get; set; } = string.Empty;
        public bool LaNguoiDungTen { get; set; }
        public string SoPhong { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public DateTime ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public int TrangThai { get; set; }
        public decimal ConNo { get; set; }
    }

    public sealed class NoCuRow
    {
        public string MaHoaDon { get; set; } = string.Empty;
        public string MaHopDong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal ConLai { get; set; }
    }
}
