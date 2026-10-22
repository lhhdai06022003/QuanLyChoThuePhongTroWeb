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

        // Nhãn trạng thái trên danh sách: hóa đơn đã hủy hiện "Đã hủy", còn lại theo trạng thái thanh toán.
        public static string HienThi(TrangThaiPhatHanhHoaDon phatHanh, TrangThaiHoaDon thanhToan)
        {
            return phatHanh == TrangThaiPhatHanhHoaDon.DaHuy ? "Đã hủy" : ThanhToan(thanhToan);
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
