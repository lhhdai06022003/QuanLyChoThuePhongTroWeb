using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public static class InvoiceStatusLabels
    {
        public static string PhatHanh(TrangThaiPhatHanhHoaDon? v)
        {
            if (!v.HasValue) return "—";
            return v.Value switch
            {
                TrangThaiPhatHanhHoaDon.Nhap => "Nháp",
                TrangThaiPhatHanhHoaDon.ChoDuyet => "Chờ duyệt",
                TrangThaiPhatHanhHoaDon.DaChot => "Đã chốt",
                TrangThaiPhatHanhHoaDon.DaGui => "Đã gửi",
                TrangThaiPhatHanhHoaDon.DaHuy => "Đã hủy",
                _ => "—"
            };
        }

        public static string ThanhToan(TrangThaiHoaDon? v)
        {
            if (!v.HasValue) return "—";
            return v.Value switch
            {
                TrangThaiHoaDon.ChuaThanhToan => "Chưa thanh toán",
                TrangThaiHoaDon.DaThanhToan => "Đã thanh toán",
                TrangThaiHoaDon.ThanhToanMotPhan => "Thanh toán một phần",
                _ => "—"
            };
        }
    }
}
