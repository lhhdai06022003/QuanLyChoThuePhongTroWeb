namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    // Cấu hình chung của thanh toán trực tuyến (spec thanh toán trực tuyến §10).
    public class OnlinePaymentOptions
    {
        public const string SectionName = "OnlinePayment";

        // URL công khai của web (ví dụ https://ten-mien-ngrok), dùng cho link trong email. Trống thì không chèn link.
        public string PublicBaseUrl { get; set; } = string.Empty;

        // Thanh toán trực tuyến tiền giữ chỗ (spec giữ chỗ §11, P8). Mặc định tắt: không ghi tự động tiền giữ chỗ.
        public bool ReservationEnabled { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PublicBaseUrl))
            {
                return;
            }

            if (!System.Uri.TryCreate(PublicBaseUrl.Trim(), System.UriKind.Absolute, out var uri) ||
                (uri.Scheme != System.Uri.UriSchemeHttp && uri.Scheme != System.Uri.UriSchemeHttps))
            {
                throw new System.InvalidOperationException("Cấu hình OnlinePayment:PublicBaseUrl phải là URL tuyệt đối http hoặc https.");
            }
        }

        // Ghép PublicBaseUrl với đường dẫn tương đối (bắt đầu bằng '/'). PublicBaseUrl trống thì không có link.
        public string? BuildUrl(string pathAndQuery)
        {
            if (string.IsNullOrWhiteSpace(PublicBaseUrl))
            {
                return null;
            }

            return PublicBaseUrl.Trim().TrimEnd('/') + pathAndQuery;
        }
    }
}
