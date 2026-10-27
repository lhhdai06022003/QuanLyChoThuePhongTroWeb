using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.ViewModels.PublicRooms;

// Trang kết quả sau khi khách thanh toán giữ chỗ ở VNPay (spec giữ chỗ §5.3, §15). Chỉ hiển thị, không ghi sổ.
public sealed class ReservationVnPayKetQuaViewModel
{
    private const string DanhSachYeuCauUrl = "/tai-khoan/yeu-cau-phong";

    public string TieuDe { get; set; } = string.Empty;
    public string? ThongDiep { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string MauIcon { get; set; } = string.Empty;
    public string? SoTien { get; set; }
    public string QuayLaiUrl { get; set; } = DanhSachYeuCauUrl;
    public string QuayLaiText { get; set; } = "Về danh sách yêu cầu phòng";
    public string? DatCocUrl { get; set; }
    public bool DangGhiNhan { get; set; }
    public int ReservationId { get; set; }
    public bool CanHoan { get; set; }

    public static ReservationVnPayKetQuaViewModel From(ReservationVnPayReturnResult result)
    {
        var model = new ReservationVnPayKetQuaViewModel
        {
            ReservationId = result.YeuCauGiuChoId ?? 0,
            CanHoan = result.CanHoan
        };

        if (result.YeuCauGiuChoId is int resId && resId > 0)
        {
            model.DatCocUrl = $"/tai-khoan/yeu-cau-phong/giu-cho/{resId}/dat-coc";
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
                model.TieuDe = result.CanHoan
                    ? "Đã ghi nhận thanh toán (cần hoàn lại)"
                    : "Đã ghi nhận thanh toán giữ chỗ";
                model.SoTien = result.SoTien is decimal soTien ? VndFormat.Money(soTien) : null;
                model.ThongDiep = result.CanHoan
                    ? "Yêu cầu giữ chỗ đã kết thúc hoặc chuyển sang hợp đồng. Khoản tiền đã được ghi nhận và chủ trọ sẽ liên hệ hoàn trả cho bạn."
                    : (result.SoTienVnPayBao is decimal vnPayBao
                        ? $"VNPay báo bạn đã trả {VndFormat.Money(vnPayBao)}"
                        : "Tiền giữ chỗ đã được ghi nhận thành công.");
                model.Icon = result.CanHoan ? "ti-alert-circle" : "ti-circle-check";
                model.MauIcon = result.CanHoan ? "text-warning" : "text-success";
                break;

            case VnPayReturnStatus.DangGhiNhan:
                model.TieuDe = "VNPay báo thanh toán thành công, hệ thống đang ghi nhận.";
                model.Icon = "ti-hourglass";
                model.MauIcon = "text-info";
                model.DangGhiNhan = true;
                break;

            case VnPayReturnStatus.KhongGhiNhan:
                model.TieuDe = "VNPay báo đã thanh toán nhưng yêu cầu không còn nhận khoản này. Chủ trọ sẽ kiểm tra và liên hệ với bạn.";
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
}
