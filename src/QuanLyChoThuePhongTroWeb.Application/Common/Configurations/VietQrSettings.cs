namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public sealed record VietQrSettings
    {
        public string BankId { get; init; } = "MB";
        public string AccountNumber { get; init; } = string.Empty;
        public string AccountName { get; init; } = string.Empty;
        // Tên ngân hàng hiển thị trên panel thanh toán của khách thuê (spec thanh toán trực tuyến §10). Trống thì hiện BankId.
        public string BankName { get; init; } = string.Empty;
    }
}
