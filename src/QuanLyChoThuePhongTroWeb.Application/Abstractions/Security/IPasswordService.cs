namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Security
{
    public interface IPasswordService
    {
        string HashPassword(string password);
        bool VerifyPassword(string hashedPassword, string providedPassword, out bool needsRehash);
        bool VerifyPassword(string hashedPassword, string providedPassword);
    }
}
