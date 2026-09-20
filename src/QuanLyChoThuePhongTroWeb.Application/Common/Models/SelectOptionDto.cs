namespace QuanLyChoThuePhongTroWeb.Application.Common.Models
{
    public sealed record SelectOptionDto(string Value, string Text, bool Selected = false)
    {
        public SelectOptionDto(string value, string text) : this(value, text, false) { }
    }
}
