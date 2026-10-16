namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Reason chỉ mô tả loại lỗi để log, không chứa secret, header hay nội dung request.
public sealed record SePayWebhookVerification(bool IsValid, string? Reason);

// Xác thực webhook SePay theo SePay:WebhookAuthMode (Infrastructure cài đặt). Đối số là giá trị header thô.
public interface ISePayWebhookVerifier
{
    SePayWebhookVerification Verify(byte[] rawBody, string? timestampHeader, string? signatureHeader, string? authorizationHeader);
}
