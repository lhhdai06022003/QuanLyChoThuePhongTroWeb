using System.Globalization;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Soạn tin Telegram (HTML) và dữ liệu email biên lai (spec thanh toán trực tuyến §6.2, §6.3). Thuần: không DI, không đọc DB.
// Mọi chuỗi động trong tin Telegram đi qua EscapeHtml; giờ hiển thị theo giờ Việt Nam (dữ liệu lưu UTC).
public static class PaymentNotificationFormatter
{
    private const int MaxOriginalContentLength = 200;
    private const string VietnamDateTimeFormat = "dd/MM/yyyy HH:mm:ss";

    private const string IconSuccess = "✅";
    private const string IconPartial = "⚠️";
    private const string IconBranch = "\U0001F3E2";
    private const string IconRoom = "\U0001F3E0";
    private const string IconPeriod = "\U0001F4C5";
    private const string IconTenant = "\U0001F464";
    private const string IconReceived = "\U0001F4B0";
    private const string IconProgress = "\U0001F4CA";
    private const string IconStatus = "\U0001F4CC";
    private const string IconMissing = "❗";
    private const string IconChannel = "\U0001F4B3";
    private const string IconReference = "\U0001F9FE";
    private const string IconTime = "\U0001F552";
    private const string IconSurplus = "\U0001F4A1";
    private const string IconAlert = "\U0001F514";

    // Telegram HTML chỉ cần thay ba ký tự này; & phải thay trước.
    public static string EscapeHtml(string? value)
    {
        return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    // Dư tiền dùng mẫu "Đủ" kèm dòng tiền dư; còn thiếu dùng mẫu "Một phần".
    public static string FormatRecordedTelegram(TransferOutcome outcome, PaymentReceiptSnapshot receipt)
    {
        var transfer = outcome.Transfer;
        var daThuDu = outcome.TienThua > 0 || outcome.ConLai == 0;

        var gateway = transfer.Gateway?.Trim();
        var kenh = string.IsNullOrEmpty(gateway)
            ? "Chuyển khoản VietQR"
            : $"Chuyển khoản VietQR ({EscapeHtml(gateway)})";

        var lines = new List<string>
        {
            daThuDu ? $"{IconSuccess} THU TIỀN TRỌ THÀNH CÔNG" : $"{IconPartial} THU TIỀN TRỌ (ĐÓNG MỘT PHẦN)",
            $"{IconBranch} Chi nhánh: {EscapeHtml(receipt.TenChiNhanh)}",
            $"{IconRoom} Phòng: {EscapeHtml(receipt.TenPhong)}",
            $"{IconPeriod} Kỳ: Tháng {receipt.Thang.ToString("00", CultureInfo.InvariantCulture)}/{receipt.Nam.ToString(CultureInfo.InvariantCulture)}",
            $"{IconTenant} Khách thuê: {EscapeHtml(receipt.TenKhachThue)}",
            $"{IconReceived} Vừa nhận: +{Vnd(transfer.TransferAmount)} VNĐ",
            $"{IconProgress} Tiến độ {EscapeHtml(receipt.MaHoaDon)}: {Vnd(outcome.DaThu)}/{Vnd(outcome.TongTien)} VNĐ",
            daThuDu ? $"{IconStatus} Trạng thái: ĐÃ THANH TOÁN ĐỦ" : $"{IconMissing} Còn thiếu: {Vnd(outcome.ConLai)} VNĐ",
            $"{IconChannel} Kênh: {kenh}",
            $"{IconReference} Mã GD: {EscapeHtml(TransferReference(transfer))}",
            $"{IconTime} {VietnamTime(transfer.TransactionDateUtc)}"
        };

        if (outcome.TienThua > 0)
        {
            lines.Add($"{IconSurplus} Khách đóng dư +{Vnd(outcome.TienThua)} VNĐ (đã ghi chú, cần xử lý ngoài hệ thống)");
        }

        return string.Join("\n", lines);
    }

    // context có khi hóa đơn xác định được (chi nhánh, hóa đơn thay thế).
    public static string FormatAlertTelegram(TransferOutcome outcome, TransferAlertContext? context)
    {
        var transfer = outcome.Transfer;
        var lines = new[]
        {
            $"{IconAlert} CHUYỂN KHOẢN CẦN XỬ LÝ TAY",
            $"Lý do: {EscapeHtml(AlertReason(outcome, context))}",
            $"Số tiền: +{Vnd(transfer.TransferAmount)} VNĐ",
            $"Nội dung: {EscapeHtml(OriginalContent(transfer))}",
            $"Mã GD: {EscapeHtml(TransferReference(transfer))}",
            $"Thời gian: {VietnamTime(transfer.TransactionDateUtc)}",
            "Gợi ý: Vào Hóa đơn → Thu thủ công"
        };

        return string.Join("\n", lines);
    }

    // Số liệu "đợt", "lũy kế", "còn nợ" tính theo sổ lúc đọc snapshot. Chuỗi chưa mã hóa HTML: MailKit tự mã hóa khi dựng email.
    public static PaymentReceiptEmail BuildReceiptEmail(PaymentReceiptSnapshot receipt, string? maThamChieu, string? linkXemBienLai)
    {
        return new PaymentReceiptEmail
        {
            HoaDonId = receipt.HoaDonId,
            MaHoaDon = receipt.MaHoaDon,
            LichSuThanhToanId = receipt.LichSuThanhToanId,
            TenChiNhanh = receipt.TenChiNhanh,
            DiaChiChiNhanh = receipt.DiaChiChiNhanh,
            TenPhong = receipt.TenPhong,
            TenNguoiThanhToan = receipt.TenKhachThue,
            Thang = receipt.Thang,
            Nam = receipt.Nam,
            NgayThanhToanVn = VietnamTime(receipt.NgayThanhToanUtc),
            PhuongThuc = PaymentMethodLabel(receipt.MaGiaoDich, receipt.PhuongThucThanhToan),
            MaThamChieu = string.IsNullOrWhiteSpace(maThamChieu) ? null : maThamChieu.Trim(),
            TongTien = receipt.TongTien,
            SoTienLanNay = receipt.SoTienThanhToan,
            LuyKeDaThanhToan = receipt.LuyKeDaThanhToan,
            ConNo = Math.Max(0m, receipt.TongTien - receipt.LuyKeDaThanhToan),
            TienThua = PaymentNoteTags.Parse(receipt.GhiChu).TienThua ?? 0m,
            DaThuDu = receipt.LuyKeDaThanhToan >= receipt.TongTien,
            DotThu = receipt.DotThu,
            LinkXemBienLai = string.IsNullOrWhiteSpace(linkXemBienLai) ? null : linkXemBienLai.Trim()
        };
    }

    private static string AlertReason(TransferOutcome outcome, TransferAlertContext? context)
    {
        var transfer = outcome.Transfer;
        switch (outcome.Kind)
        {
            case TransferOutcomeKind.WrongAccount:
                return $"Tiền vào tài khoản {transfer.AccountNumber} khác tài khoản nhận tiền của hệ thống";

            case TransferOutcomeKind.Unmatched:
                return outcome.Code == null
                    ? "Nội dung chuyển khoản không có mã TTPTHD"
                    : $"Không nhận ra mã thanh toán hợp lệ ({outcome.Code})";

            case TransferOutcomeKind.InvoiceNotPayable:
                var reason = $"Hóa đơn {InvoiceLabel(outcome)} không nhận tiền được: {outcome.LyDo}";
                return string.IsNullOrEmpty(context?.MaHoaDonThayThe)
                    ? reason
                    : $"{reason}; hóa đơn thay thế: {context.MaHoaDonThayThe} (#HD{context.HoaDonThayTheId})";

            case TransferOutcomeKind.AlreadyPaid:
                return $"Khách chuyển thêm {Vnd(transfer.TransferAmount)}đ cho hóa đơn {InvoiceLabel(outcome)} đã trả đủ";

            default:
                return outcome.LyDo ?? outcome.Kind.ToString();
        }
    }

    private static string InvoiceLabel(TransferOutcome outcome)
    {
        return outcome.MaHoaDon ?? "#HD" + outcome.HoaDonId;
    }

    // Dùng chung với biên lai web và panel thanh toán của khách thuê (cùng nhãn phương thức với email biên lai).
    internal static string PaymentMethodLabel(string maGiaoDich, PhuongThucThanhToan phuongThuc)
    {
        if (maGiaoDich.StartsWith(InvoicePaymentPolicy.SePayMaGiaoDichPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Chuyển khoản 24/7 (VietQR, tự động)";
        }

        return phuongThuc == PhuongThucThanhToan.TienMat ? "Tiền mặt" : "Chuyển khoản";
    }

    // Mã tham chiếu ngân hàng; giao dịch không có thì dùng id SePay.
    private static string TransferReference(IncomingBankTransfer transfer)
    {
        var reference = transfer.ReferenceCode?.Trim();
        return string.IsNullOrEmpty(reference) ? $"ID {transfer.SePayId}" : reference;
    }

    // Nội dung chuyển khoản gốc, cắt còn 200 ký tự (không cắt giữa cặp surrogate).
    private static string OriginalContent(IncomingBankTransfer transfer)
    {
        var content = (string.IsNullOrWhiteSpace(transfer.Content) ? transfer.Description : transfer.Content) ?? string.Empty;
        content = content.Trim();
        if (content.Length <= MaxOriginalContentLength)
        {
            return content;
        }

        var length = char.IsHighSurrogate(content[MaxOriginalContentLength - 1]) ? MaxOriginalContentLength - 1 : MaxOriginalContentLength;
        return content[..length];
    }

    private static string VietnamTime(DateTime utc)
    {
        return utc.AddHours(7).ToString(VietnamDateTimeFormat, CultureInfo.InvariantCulture);
    }

    private static string Vnd(decimal soTien) => InvoicePaymentPolicy.FormatVnd(soTien);
}
