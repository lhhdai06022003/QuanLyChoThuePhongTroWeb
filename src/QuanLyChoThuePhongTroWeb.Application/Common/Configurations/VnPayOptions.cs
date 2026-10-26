namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public class VnPayOptions
    {
        public const string SectionName = "VnPay";

        // Tắt thì endpoint IPN trả 404.
        public bool Enabled { get; set; }

        public string TmnCode { get; set; } = string.Empty;
        public string HashSecret { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

        // Thời gian link thanh toán còn hiệu lực (phần tạo link dùng).
        public int ExpireMinutes { get; set; } = 15;

        public void Validate()
        {
            if (Enabled && string.IsNullOrWhiteSpace(TmnCode))
            {
                throw new System.InvalidOperationException("Cấu hình VnPay:TmnCode không được để trống khi bật VnPay.");
            }

            if (Enabled && string.IsNullOrWhiteSpace(HashSecret))
            {
                throw new System.InvalidOperationException("Cấu hình VnPay:HashSecret không được để trống khi bật VnPay.");
            }

            if (!System.Uri.TryCreate(PaymentUrl, System.UriKind.Absolute, out var paymentUri) || paymentUri.Scheme != System.Uri.UriSchemeHttps)
            {
                throw new System.InvalidOperationException("Cấu hình VnPay:PaymentUrl phải là URL tuyệt đối dùng https.");
            }

            if (ExpireMinutes < 5 || ExpireMinutes > 60)
            {
                throw new System.InvalidOperationException("Cấu hình VnPay:ExpireMinutes phải nằm trong khoảng từ 5 đến 60.");
            }
        }
    }
}
