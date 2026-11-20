namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public sealed record AutoReminderSettings
    {
        public bool Enabled { get; init; } = false;
        // Mốc nhắc đầu tiên: số ngày quá hạn (theo ngày VN).
        public int OverdueDays { get; init; } = 5;
        // Sau mốc đầu, nhắc lại mỗi bấy nhiêu ngày tới khi khách trả; 0 = chỉ nhắc một lần ở mốc đầu.
        public int RepeatEveryDays { get; init; } = 5;
    }
}
