namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs
{
    public sealed record NguoiThueAutocompleteDto
    {
        public int Id { get; init; }
        public string HoVaTen { get; init; } = string.Empty;
        public string SoDienThoai { get; init; } = string.Empty;
        public string CCCD { get; init; } = string.Empty;
    }
}
