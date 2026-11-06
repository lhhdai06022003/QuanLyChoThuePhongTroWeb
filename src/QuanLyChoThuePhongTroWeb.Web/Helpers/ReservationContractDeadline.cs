using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Hạn ký hợp đồng hiển thị cho khách (trang đặt cọc, yêu cầu của tôi, dashboard).
    // Lượt đặt cọc tự tạo lưu sẵn giờ tạo + 48 giờ làm giá trị tạm để bước nhận tiền tính hạn thật,
    // nên chỉ hiện ngày khi đã nhận đủ cọc; hủy/hết hạn trước đó thì không có hạn ký.
    // Giữ chỗ cũ do nhân viên duyệt hiện hạn đã lưu như trước.
    public static class ReservationContractDeadline
    {
        public static bool AwaitingSelfServicePayment(ReservationDto reservation) =>
            reservation.IsSelfService && reservation.Status is "ChoThanhToan" or "ChoXacNhanTien";

        public static DateTime? Shown(ReservationDto reservation) =>
            reservation.ContractDeadlineUtc is { } deadline &&
            reservation.Status != "KhachKhongThue" &&
            (!reservation.IsSelfService || reservation.Status is "DangGiuCho" or "DaChuyenHopDong")
                ? deadline
                : null;
    }
}
