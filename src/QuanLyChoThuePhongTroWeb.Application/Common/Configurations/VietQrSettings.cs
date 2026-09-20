namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public sealed record VietQrSettings
    {
        public string BankId { get; init; } = "MB";
        public string AccountNumber { get; init; } = string.Empty;
        public string AccountName { get; init; } = string.Empty;
    }
}
