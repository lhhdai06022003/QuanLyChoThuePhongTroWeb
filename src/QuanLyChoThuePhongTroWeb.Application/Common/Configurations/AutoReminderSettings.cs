namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public sealed record AutoReminderSettings
    {
        public bool Enabled { get; init; } = false;
        public int OverdueDays { get; init; } = 5;
    }
}
