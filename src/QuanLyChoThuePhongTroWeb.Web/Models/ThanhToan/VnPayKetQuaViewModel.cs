using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Models.ThanhToan
{
    // Trang kết quả sau khi khách thanh toán ở VNPay (spec VNPay §4.3). Chỉ hiển thị, không ghi sổ.
    // Trạng thái không xác thực không mang số tiền hay mã hóa đơn (HoaDonId = 0).
    public class VnPayKetQuaViewModel
    {
        private const string DanhSachHoaDonUrl = "/KhachThue/HoaDon";

        public string TieuDe { get; set; } = string.Empty;
        public string? ThongDiep { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string MauIcon { get; set; } = string.Empty;
        // Đã định dạng; chỉ có khi đã ghi nhận.
        public string? SoTien { get; set; }
        public string? BienLaiUrl { get; set; }
        public string QuayLaiUrl { get; set; } = DanhSachHoaDonUrl;
        public string QuayLaiText { get; set; } = "Về danh sách hóa đơn";
        // Trang tự cập nhật khi IPN ghi sổ.
        public bool DangGhiNhan { get; set; }
        public int HoaDonId { get; set; }

        public static VnPayKetQuaViewModel From(VnPayReturnResult result)
        {
            var model = new VnPayKetQuaViewModel
            {
                HoaDonId = result.HoaDonId ?? 0
            };

            if (result.HoaDonId is int hoaDonId)
            {
                model.QuayLaiUrl = $"{DanhSachHoaDonUrl}?hoaDonId={hoaDonId}";
                model.QuayLaiText = "Quay lại hóa đơn";
            }

            switch (result.TrangThai)
            {
                case VnPayReturnStatus.DaHuy:
                    model.TieuDe = "Bạn đã hủy thanh toán.";
                    model.Icon = "ti-circle-x";
                    model.MauIcon = "text-secondary";
                    break;

                case VnPayReturnStatus.KhongThanhCong:
                    model.TieuDe = "Thanh toán chưa thành công";
                    model.ThongDiep = result.MoTaLoi;
                    model.Icon = "ti-alert-triangle";
                    model.MauIcon = "text-warning";
                    break;

                case VnPayReturnStatus.DaGhiNhan:
                    model.TieuDe = "Đã ghi nhận thanh toán";
                    model.SoTien = result.SoTien is decimal soTien ? Money(soTien) : null;
                    // Khách trả nhiều hơn số còn lại lúc ghi (tiền thừa): cho khách thấy cả số VNPay báo.
                    model.ThongDiep = result.SoTienVnPayBao is decimal vnPayBao
                        ? $"VNPay báo bạn đã trả {Money(vnPayBao)}"
                        : null;
                    model.Icon = "ti-circle-check";
                    model.MauIcon = "text-success";
                    model.BienLaiUrl = result.LichSuThanhToanId is int id ? $"/KhachThue/HoaDon/BienLai/{id}" : null;
                    break;

                case VnPayReturnStatus.DangGhiNhan:
                    model.TieuDe = "VNPay báo thanh toán thành công, hệ thống đang ghi nhận.";
                    model.Icon = "ti-hourglass";
                    model.MauIcon = "text-info";
                    model.DangGhiNhan = true;
                    break;

                case VnPayReturnStatus.KhongGhiNhan:
                    model.TieuDe = "VNPay báo đã thanh toán nhưng hóa đơn không còn nhận khoản này. Chủ trọ sẽ kiểm tra và liên hệ với bạn.";
                    model.Icon = "ti-alert-circle";
                    model.MauIcon = "text-warning";
                    break;

                default:
                    model.TieuDe = "Không xác thực được kết quả từ VNPay.";
                    model.Icon = "ti-shield-x";
                    model.MauIcon = "text-danger";
                    break;
            }

            return model;
        }

        private static string Money(decimal soTien) => InvoicePaymentPolicy.FormatVnd(soTien) + "đ";
    }
}
