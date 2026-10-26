using System.Text.RegularExpressions;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Lấy mã chuyển khoản từ giao dịch ngân hàng (spec thanh toán trực tuyến §4.2, bước 4). Thuần, không đọc DB.
public static class TransferCodeParser
{
    // Mẫu của hóa đơn: TTPTHD{HoaDonId}, cho phép khách gõ có dấu cách. Số quá 9 chữ số là không khớp.
    // Dùng [0-9] thay cho \d để không nhận chữ số Unicode khác. Thêm tiền tố mới thì thêm mẫu ở đây.
    private static readonly Regex InvoiceCodePattern = new(
        @"TTPT\s*HD\s*([0-9]{1,9})(?![0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Đúng dạng mã hóa đơn đã chuẩn hóa (TTPTHD + 1–9 chữ số), không phải tiền tố khác hay trường code tự do.
    private static readonly Regex InvoiceCodeExact = new(
        @"^TTPTHD([0-9]{1,9})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Mẫu của giữ chỗ: TTPTGC{YeuCauGiuChoId}, cùng quy tắc với mã hóa đơn.
    private static readonly Regex ReservationCodePattern = new(
        @"TTPT\s*GC\s*([0-9]{1,9})(?![0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Đúng dạng mã giữ chỗ đã chuẩn hóa (TTPTGC + 1–9 chữ số).
    private static readonly Regex ReservationCodeExact = new(
        @"^TTPTGC([0-9]{1,9})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Trả mã đã chuẩn hóa (bỏ khoảng trắng, viết hoa), ví dụ "TTPTHD105" hoặc "TTPTGC57"; null nếu không có.
    // Ưu tiên trường code của SePay; chỉ khi trường này rỗng mới tìm trong content rồi description.
    public static string? Extract(string? code, string? content, string? description)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            return new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        }

        return FromText(content) ?? FromText(description);
    }

    private static string? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Nội dung có cả hai loại mã thì lấy mã xuất hiện trước.
        var invoice = InvoiceCodePattern.Match(text);
        var reservation = ReservationCodePattern.Match(text);
        if (invoice.Success && (!reservation.Success || invoice.Index < reservation.Index))
        {
            return "TTPTHD" + invoice.Groups[1].Value;
        }

        return reservation.Success ? "TTPTGC" + reservation.Groups[1].Value : null;
    }

    // YeuCauGiuChoId trong mã đã chuẩn hóa của Extract (ví dụ "TTPTGC57" -> 57); null nếu không phải mã giữ chỗ hoặc id bằng 0.
    public static int? ExtractReservationId(string? transferCode)
    {
        if (string.IsNullOrEmpty(transferCode))
        {
            return null;
        }

        var match = ReservationCodeExact.Match(transferCode);
        return match.Success && int.TryParse(match.Groups[1].Value, out var id) && id > 0 ? id : null;
    }

    // HoaDonId trong mã đã chuẩn hóa của Extract (ví dụ "TTPTHD105" -> 105); null nếu không phải mã hóa đơn hoặc id bằng 0.
    public static int? ExtractInvoiceId(string? transferCode)
    {
        if (string.IsNullOrEmpty(transferCode))
        {
            return null;
        }

        var match = InvoiceCodeExact.Match(transferCode);
        return match.Success && int.TryParse(match.Groups[1].Value, out var id) && id > 0 ? id : null;
    }
}
