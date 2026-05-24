using QuanLyChoThuePhongTroWeb.Models;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs
{
    public interface INguoiDungService
    {
        Task SeedAdminAccountAsync();
        Task<NguoiDung> ValidateUserAsync(string username, string password);
    }
}
