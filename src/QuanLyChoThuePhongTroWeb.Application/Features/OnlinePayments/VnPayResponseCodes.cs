namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Mô tả hiển thị cho khách theo bảng vnp_ResponseCode của VNPay (spec VNPay §5.5; đối chiếu tài liệu sandbox pay.html ngày 26/10/2026).
// Chỉ gọi cho mã khác "00". Mã không có trong bảng thì hiện kèm chính mã đó.
public static class VnPayResponseCodes
{
    public static string Describe(string? code)
    {
        var trimmed = code?.Trim();
        return trimmed switch
        {
            // Tài liệu VNPay: "Trừ tiền thành công. Giao dịch bị nghi ngờ". Trang kết quả lấy tiêu đề "chưa thành công"
            // nên mô tả nói rõ việc cần làm; IPN không ghi sổ mã này (người dùng chọn giữ nguyên, spec §5.2).
            "07" => "Giao dịch đang được VNPay kiểm tra (mã 07). Vui lòng liên hệ chủ trọ trước khi thanh toán lại.",
            "09" => "Thẻ/tài khoản chưa đăng ký dịch vụ InternetBanking tại ngân hàng.",
            "10" => "Xác thực thông tin thẻ/tài khoản không đúng quá 3 lần.",
            "11" => "Đã hết hạn chờ thanh toán. Vui lòng thực hiện lại giao dịch.",
            "12" => "Thẻ/tài khoản bị khóa.",
            "13" => "Nhập sai mật khẩu xác thực giao dịch (OTP). Vui lòng thực hiện lại giao dịch.",
            "24" => "Khách hàng hủy giao dịch.",
            "51" => "Tài khoản không đủ số dư để thực hiện giao dịch.",
            "65" => "Tài khoản đã vượt quá hạn mức giao dịch trong ngày.",
            "75" => "Ngân hàng thanh toán đang bảo trì.",
            "79" => "Nhập sai mật khẩu thanh toán quá số lần quy định. Vui lòng thực hiện lại giao dịch.",
            "99" => "Lỗi khác từ VNPay.",
            null or "" => "Giao dịch không thành công.",
            _ => $"Giao dịch không thành công (mã {trimmed})"
        };
    }
}
