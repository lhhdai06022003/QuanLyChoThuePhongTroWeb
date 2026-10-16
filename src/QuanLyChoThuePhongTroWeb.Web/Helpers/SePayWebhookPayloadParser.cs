using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Đọc payload webhook SePay (spec thanh toán trực tuyến §4.1) thành IncomingBankTransfer: chỉ parse và đổi
    // transactionDate từ giờ Việt Nam sang UTC. Thiếu hoặc sai kiểu trường bắt buộc thì coi như payload không hợp lệ.
    public static class SePayWebhookPayloadParser
    {
        private const string TransactionDateFormat = "yyyy-MM-dd HH:mm:ss";

        public static bool TryParse(byte[] body, [NotNullWhen(true)] out IncomingBankTransfer? transfer)
        {
            transfer = null;

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!TryGetRequiredInt64(root, "id", out var id) ||
                    !TryGetRequiredInt64(root, "transferAmount", out var amount) ||
                    !TryGetRequiredString(root, "transferType", out var transferType) ||
                    !TryGetRequiredString(root, "transactionDate", out var transactionDate) ||
                    !VietnamTime.TryParseExactToUtc(transactionDate, TransactionDateFormat, out var transactionDateUtc))
                {
                    return false;
                }

                transfer = new IncomingBankTransfer(
                    SePayId: id,
                    Gateway: GetOptionalString(root, "gateway"),
                    TransactionDateUtc: transactionDateUtc,
                    AccountNumber: GetOptionalString(root, "accountNumber"),
                    SubAccount: GetOptionalString(root, "subAccount"),
                    Code: GetOptionalString(root, "code"),
                    Content: GetOptionalString(root, "content"),
                    TransferType: transferType,
                    TransferAmount: amount,
                    ReferenceCode: GetOptionalString(root, "referenceCode"),
                    Description: GetOptionalString(root, "description"));
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                // Chuỗi JSON chứa ký tự không chuyển được sang UTF-16.
                return false;
            }
        }

        // Phải là số nguyên: TryGetInt64 ném lỗi nếu phần tử không phải số.
        private static bool TryGetRequiredInt64(JsonElement root, string name, out long value)
        {
            value = 0;
            return root.TryGetProperty(name, out var element) &&
                element.ValueKind == JsonValueKind.Number &&
                element.TryGetInt64(out value);
        }

        private static bool TryGetRequiredString(JsonElement root, string name, [NotNullWhen(true)] out string? value)
        {
            value = root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
            return value != null;
        }

        // Thiếu, null hoặc không phải chuỗi đều coi là không có.
        private static string? GetOptionalString(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }
    }
}
