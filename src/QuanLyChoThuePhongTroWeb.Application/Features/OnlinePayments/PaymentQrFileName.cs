using System.Globalization;
using System.Text;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Tên file ảnh QR khi khách bấm "Tải ảnh QR" (spec thanh toán trực tuyến §3.2 mục 2): chỉ gồm ASCII [A-Za-z0-9_-]. Thuần.
public static class PaymentQrFileName
{
    public static string Build(string tenPhong, int hoaDonId)
    {
        var safe = Sanitize(tenPhong);
        return safe.Length > 0
            ? $"QR_Phong_{safe}_HD{hoaDonId}.png"
            : $"QR_Phong_HD{hoaDonId}.png";
    }

    // Bỏ dấu, đổi đ/Đ thành d/D, mỗi khoảng trắng thành "_", bỏ mọi ký tự còn lại ngoài [A-Za-z0-9_-].
    private static string Sanitize(string? value)
    {
        var builder = new StringBuilder();
        foreach (var c in (value ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mapped = c switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ when char.IsWhiteSpace(c) => '_',
                _ => c
            };

            if (mapped is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-')
            {
                builder.Append(mapped);
            }
        }

        return builder.ToString();
    }
}
