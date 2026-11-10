using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public class InvoiceStatusHistoryRes
    {
        public string ThoiGian { get; set; } = string.Empty;
        public string PhatHanhTu { get; set; } = string.Empty;
        public string PhatHanhDen { get; set; } = string.Empty;
        public string ThanhToanTu { get; set; } = string.Empty;
        public string ThanhToanDen { get; set; } = string.Empty;
        public string NguoiThucHien { get; set; } = string.Empty;
        public string LyDo { get; set; } = string.Empty;
    }

    public class InvoiceListFilter
    {
        // TrangThaiThanhToan = -2: chưa thanh toán hoặc thanh toán một phần ("chưa thu đủ"). -1 = tất cả; 0, 1, 2 = từng trạng thái.
        public const int ChuaThuDu = -2;

        public int ChiNhanhId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public int TrangThaiThanhToan { get; set; } = -1;
        public int TrangThaiPhatHanh { get; set; } = -1;
        // Chỉ hóa đơn DaGui còn nợ của hợp đồng DaKetThuc mà khách chưa gia hạn sang hợp đồng mới.
        public bool HopDongDaKetThucConNo { get; set; }
    }
}
