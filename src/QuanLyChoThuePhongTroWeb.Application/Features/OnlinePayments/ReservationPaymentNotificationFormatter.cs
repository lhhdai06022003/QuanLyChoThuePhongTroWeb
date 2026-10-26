using System.Globalization;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Soạn thông báo trong app, tin Telegram (HTML) và email khi tiền giữ chỗ được ghi tự động (spec giữ chỗ §8).
// Thuần: không DI, không đọc DB. Mọi chuỗi động trong tin Telegram đi qua EscapeHtml; giờ hiển thị theo giờ Việt Nam (dữ liệu lưu UTC).
public static class ReservationPaymentNotificationFormatter
{
    // ThongBao.TieuDe tối đa 255 ký tự.
    private const int MaxTitleLength = 255;
    private const string VietnamDateTimeFormat = "dd/MM/yyyy HH:mm";
    private const string DangGiuChoStatus = "DangGiuCho";

    // "VNPay" cho IPN VNPay, còn lại là "SePay".
    public static string ChannelName(IncomingBankTransfer transfer)
    {
        return transfer.Kenh == OnlinePaymentChannel.VnPay ? "VNPay" : "SePay";
    }

    // Nhãn tiếng Việt của tên enum TrangThaiYeuCauGiuCho; tên lạ giữ nguyên.
    public static string StatusLabel(string trangThai)
    {
        return trangThai switch
        {
            "ChoThanhToan" => "Chờ thanh toán",
            "ChoXacNhanTien" => "Chờ xác nhận tiền",
            "DangGiuCho" => "Đang giữ chỗ",
            "DaChuyenHopDong" => "Đã chuyển hợp đồng",
            "TuChoi" => "Từ chối",
            "HetHan" => "Hết hạn",
            "DaHuy" => "Đã hủy",
            _ => trangThai
        };
    }

    public static string StaffTitle(TransferOutcome outcome)
    {
        var giuCho = Require(outcome);
        var title = $"Đã nhận tiền giữ chỗ {Vnd(outcome.SoTienGhiNhan)}đ qua {ChannelName(outcome.Transfer)} — phòng {giuCho.SoPhong}";
        return Truncate(title, MaxTitleLength);
    }

    public static string StaffContent(TransferOutcome outcome)
    {
        var giuCho = Require(outcome);
        var content =
            $"Giữ chỗ #{giuCho.YeuCauGiuChoId} ({giuCho.MaYeuCau}): đã nhận {Vnd(giuCho.DaNhanSau)}/{Vnd(giuCho.SoTienGiuCho)}đ, còn lại {Vnd(outcome.ConLai)}đ.";

        if (giuCho.VuaCoHieuLuc)
        {
            content += " Giữ chỗ đã có hiệu lực.";
        }

        if (giuCho.CanHoanTien)
        {
            content += " Khoản này về khi giữ chỗ đã quá hạn hoặc không còn hiệu lực, cần hoàn cho khách.";
        }

        if (outcome.TienThua > 0)
        {
            content += $" Khách chuyển dư {Vnd(outcome.TienThua)}đ, cần hoàn cho khách.";
        }

        if (giuCho.ConMinhChungChoXacNhan)
        {
            content += " Giữ chỗ còn minh chứng chờ xác nhận, kiểm tra trùng trước khi xác nhận.";
        }

        return content;
    }

    public static string FormatTelegram(TransferOutcome outcome)
    {
        var giuCho = Require(outcome);
        var transfer = outcome.Transfer;
        var canHoan = giuCho.CanHoanTien || outcome.TienThua > 0;

        var lines = new List<string>();
        if (canHoan)
        {
            lines.Add($"{PaymentNotificationFormatter.IconAlert} TIỀN GIỮ CHỖ CẦN HOÀN");
            lines.Add(giuCho.CanHoanTien
                ? $"Lý do: Giữ chỗ #{giuCho.YeuCauGiuChoId} đã quá hạn thanh toán hoặc không còn hiệu lực ({PaymentNotificationFormatter.EscapeHtml(StatusLabel(giuCho.TrangThaiGiuCho))})"
                : $"Lý do: Khách chuyển dư {Vnd(outcome.TienThua)} VNĐ so với số tiền giữ chỗ");
        }
        else if (giuCho.TrangThaiGiuCho == DangGiuChoStatus)
        {
            lines.Add($"{PaymentNotificationFormatter.IconSuccess} THU TIỀN GIỮ CHỖ — GIỮ CHỖ ĐÃ CÓ HIỆU LỰC");
        }
        else
        {
            lines.Add($"{PaymentNotificationFormatter.IconPartial} THU TIỀN GIỮ CHỖ (MỘT PHẦN)");
        }

        lines.Add($"{PaymentNotificationFormatter.IconBranch} Chi nhánh: {PaymentNotificationFormatter.EscapeHtml(giuCho.TenChiNhanh)}");
        lines.Add($"{PaymentNotificationFormatter.IconRoom} Phòng: {PaymentNotificationFormatter.EscapeHtml(giuCho.SoPhong)}");
        lines.Add($"{PaymentNotificationFormatter.IconTenant} Khách: {PaymentNotificationFormatter.EscapeHtml(giuCho.TenKhach)}");
        lines.Add($"{PaymentNotificationFormatter.IconReceived} Vừa nhận: +{Vnd(outcome.SoTienGhiNhan)} VNĐ");
        lines.Add($"{PaymentNotificationFormatter.IconProgress} Đã nhận: {Vnd(giuCho.DaNhanSau)}/{Vnd(giuCho.SoTienGiuCho)} VNĐ");
        lines.Add($"{PaymentNotificationFormatter.IconMissing} Còn lại: {Vnd(outcome.ConLai)} VNĐ");
        lines.Add($"{PaymentNotificationFormatter.IconChannel} Kênh: {PaymentNotificationFormatter.ChannelLabel(transfer)}");
        lines.Add($"{PaymentNotificationFormatter.IconReference} Mã GD: {PaymentNotificationFormatter.EscapeHtml(PaymentNotificationFormatter.TransferReference(transfer))}");
        lines.Add($"{PaymentNotificationFormatter.IconTime} {VietnamTime(transfer.TransactionDateUtc)}");

        if (canHoan)
        {
            lines.Add($"Gợi ý: Vào Giữ chỗ → Chi tiết #{giuCho.YeuCauGiuChoId} → Quyết định hoàn tiền");
        }

        return string.Join("\n", lines);
    }

    // Văn bản thuần gửi khách. Khoản về khi giữ chỗ không còn hiệu lực vẫn gửi, báo sẽ được hoàn.
    public static ReservationPaymentNoticeEmail BuildEmail(TransferOutcome outcome)
    {
        var giuCho = Require(outcome);
        var transfer = outcome.Transfer;
        var soTien = Vnd(outcome.SoTienGhiNhan);

        string tieuDe;
        string than;
        if (giuCho.CanHoanTien)
        {
            tieuDe = $"Đã nhận {soTien}đ cho giữ chỗ phòng {giuCho.SoPhong} — khoản này sẽ được hoàn";
            than = "Giữ chỗ đã quá hạn thanh toán hoặc không còn hiệu lực nên khoản này sẽ được hoàn lại. Nhân viên sẽ liên hệ với bạn.\n";
        }
        else if (giuCho.TrangThaiGiuCho == DangGiuChoStatus)
        {
            tieuDe = $"Giữ chỗ phòng {giuCho.SoPhong} đã có hiệu lực";
            var den = giuCho.HanKyHopDongUtc.HasValue ? " tới " + VietnamTime(giuCho.HanKyHopDongUtc.Value) : string.Empty;
            than = $"Giữ chỗ đã có hiệu lực{den}.\n";
            if (outcome.TienThua > 0)
            {
                than += $"Số tiền chuyển dư {Vnd(outcome.TienThua)}đ sẽ được nhân viên liên hệ hoàn lại.\n";
            }
        }
        else
        {
            tieuDe = $"Đã nhận {soTien}đ tiền giữ chỗ phòng {giuCho.SoPhong}";
            than = $"Đã nhận {Vnd(giuCho.DaNhanSau)}đ / {Vnd(giuCho.SoTienGiuCho)}đ; còn lại {Vnd(outcome.ConLai)}đ. " +
                $"Vui lòng chuyển nốt trước {VietnamTime(giuCho.HanThanhToanUtc)}.\n";
        }

        var noiDung =
            $"Xin chào {giuCho.TenKhach},\n\n" +
            $"Hệ thống đã nhận {soTien}đ qua {ChannelName(transfer)} cho giữ chỗ phòng {giuCho.SoPhong} ({giuCho.TenChiNhanh}) lúc {VietnamTime(transfer.TransactionDateUtc)}.\n" +
            than +
            $"\nMã giao dịch: {PaymentNotificationFormatter.TransferReference(transfer)}\n\n" +
            "Trân trọng,\nBan quản lý nhà trọ";

        return new ReservationPaymentNoticeEmail(giuCho.TenKhach, tieuDe, noiDung);
    }

    private static SystemReservationRecordResult Require(TransferOutcome outcome)
    {
        return outcome.GiuCho ?? throw new ArgumentException("Kết quả không có dữ liệu giữ chỗ.", nameof(outcome));
    }

    private static string VietnamTime(DateTime utc)
    {
        return utc.AddHours(7).ToString(VietnamDateTimeFormat, CultureInfo.InvariantCulture);
    }

    private static string Vnd(decimal soTien) => InvoicePaymentPolicy.FormatVnd(soTien);

    // Không cắt giữa cặp surrogate: chuỗi UTF-16 hỏng làm Npgsql ném lỗi khi ghi.
    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }
}
