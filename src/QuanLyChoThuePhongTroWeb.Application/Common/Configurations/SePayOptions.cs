namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public enum SePayWebhookAuthMode
    {
        // Chữ ký HMAC-SHA256 trên "{timestamp}.{body}" (khuyến nghị).
        Hmac = 0,
        // Header "Authorization: Apikey {WebhookApiKey}"; chỉ dùng khi thử nhanh.
        ApiKey = 1
    }

    public class SePayOptions
    {
        public const string SectionName = "SePay";

        // Tắt thì endpoint webhook trả 404.
        public bool Enabled { get; set; }

        public SePayWebhookAuthMode WebhookAuthMode { get; set; } = SePayWebhookAuthMode.Hmac;
        public string WebhookSecret { get; set; } = string.Empty;
        public string WebhookApiKey { get; set; } = string.Empty;

        // Chênh lệch tối đa giữa giờ server và timestamp của webhook.
        public int TimestampToleranceSeconds { get; set; } = 300;

        // Dành cho đồng bộ bù qua API SePay; webhook không dùng hai giá trị này.
        public string ApiBaseUrl { get; set; } = "https://userapi.sepay.vn/v2";
        public string ApiToken { get; set; } = string.Empty;

        public void Validate()
        {
            if (!System.Enum.IsDefined(WebhookAuthMode))
            {
                throw new System.InvalidOperationException("Cấu hình SePay:WebhookAuthMode phải là Hmac hoặc ApiKey.");
            }

            if (Enabled && WebhookAuthMode == SePayWebhookAuthMode.Hmac && string.IsNullOrWhiteSpace(WebhookSecret))
            {
                throw new System.InvalidOperationException("Cấu hình SePay:WebhookSecret không được để trống khi bật SePay với WebhookAuthMode là Hmac.");
            }

            if (Enabled && WebhookAuthMode == SePayWebhookAuthMode.ApiKey && string.IsNullOrWhiteSpace(WebhookApiKey))
            {
                throw new System.InvalidOperationException("Cấu hình SePay:WebhookApiKey không được để trống khi bật SePay với WebhookAuthMode là ApiKey.");
            }

            if (TimestampToleranceSeconds < 30 || TimestampToleranceSeconds > 900)
            {
                throw new System.InvalidOperationException("Cấu hình SePay:TimestampToleranceSeconds phải nằm trong khoảng từ 30 đến 900.");
            }

            if (!System.Uri.TryCreate(ApiBaseUrl, System.UriKind.Absolute, out var apiBaseUri) || apiBaseUri.Scheme != System.Uri.UriSchemeHttps)
            {
                throw new System.InvalidOperationException("Cấu hình SePay:ApiBaseUrl phải là URL tuyệt đối dùng https.");
            }
        }
    }
}
