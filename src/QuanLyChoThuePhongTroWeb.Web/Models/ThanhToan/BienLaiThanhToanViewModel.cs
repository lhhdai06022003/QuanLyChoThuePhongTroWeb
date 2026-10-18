using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Models.ThanhToan
{
    // Biên lai thanh toán đã định dạng để view chỉ việc hiển thị (spec thanh toán trực tuyến §3.4).
    // Dùng chung cho cổng khách thuê và cổng quản lý; giờ hiển thị theo giờ Việt Nam, dữ liệu lưu UTC.
    public class BienLaiThanhToanViewModel
    {
        public string HuyHieu { get; set; } = string.Empty;
        public bool DaThuDu { get; set; }
        public string MaHoaDon { get; set; } = string.Empty;
        public int HoaDonId { get; set; }
        // Chỉ có với dòng SePay ghi theo mã tham chiếu ngân hàng.
        public string? MaThamChieu { get; set; }
        public string MaGiaoDich { get; set; } = string.Empty;
        public string NgayThanhToan { get; set; } = string.Empty;
        public string PhuongThuc { get; set; } = string.Empty;
        public string TenChiNhanh { get; set; } = string.Empty;
        public string DiaChiChiNhanh { get; set; } = string.Empty;
        public string TenPhong { get; set; } = string.Empty;
        public string TenNguoiThanhToan { get; set; } = string.Empty;
        public string KyHoaDon { get; set; } = string.Empty;
        public string TongTien { get; set; } = string.Empty;
        public string SoTienLanNay { get; set; } = string.Empty;
        public string LuyKeDaThanhToan { get; set; } = string.Empty;
        public string ConNo { get; set; } = string.Empty;
        // Null khi không có tiền thừa.
        public string? TienThua { get; set; }
        public string QuayLaiUrl { get; set; } = string.Empty;

        public static BienLaiThanhToanViewModel From(InvoiceReceiptDto dto, string quayLaiUrl)
        {
            return new BienLaiThanhToanViewModel
            {
                HuyHieu = dto.DaThuDu ? "ĐÃ THU TIỀN" : $"XÁC NHẬN THU ĐỢT {dto.DotThu}",
                DaThuDu = dto.DaThuDu,
                MaHoaDon = dto.MaHoaDon,
                HoaDonId = dto.HoaDonId,
                MaThamChieu = dto.MaThamChieu,
                MaGiaoDich = dto.MaGiaoDich,
                NgayThanhToan = VietnamTime.Format(dto.NgayThanhToanUtc, "dd/MM/yyyy HH:mm:ss"),
                PhuongThuc = dto.PhuongThuc,
                TenChiNhanh = dto.TenChiNhanh,
                DiaChiChiNhanh = dto.DiaChiChiNhanh,
                TenPhong = dto.TenPhong,
                TenNguoiThanhToan = dto.TenNguoiThanhToan,
                KyHoaDon = $"Tháng {dto.Thang}/{dto.Nam}",
                TongTien = Money(dto.TongTien),
                SoTienLanNay = Money(dto.SoTienLanNay),
                LuyKeDaThanhToan = Money(dto.LuyKeDaThanhToan),
                ConNo = Money(dto.ConNo),
                TienThua = dto.TienThua > 0 ? Money(dto.TienThua) : null,
                QuayLaiUrl = quayLaiUrl
            };
        }

        private static string Money(decimal soTien) => InvoicePaymentPolicy.FormatVnd(soTien) + "đ";
    }
}
