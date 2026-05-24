using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System.Security.Cryptography;
using System.Text;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs
{
    public class NguoiDungService : INguoiDungService
    {
        private readonly ApplicationDbContext _context;

        public NguoiDungService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedAdminAccountAsync()
        {
            if (!await _context.NguoiDungs.AnyAsync(x => !x.IsDeleted))
            {
                var admin = new NguoiDung
                {
                    TenDangNhap = "admin",
                    MatKhauHash = HashPassword("admin"),
                    Role = Role.Admin,
                    IsActive = true
                };
                _context.NguoiDungs.Add(admin);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<NguoiDung> ValidateUserAsync(string username, string password)
        {
            var hashPassword = HashPassword(password);

            var user = await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.TenDangNhap == username && x.MatKhauHash == hashPassword && x.IsActive && !x.IsDeleted);

            if (user != null)
            {
                // Cập nhật lần đăng nhập cuối
                user.LanDangNhapCuoi = DateTime.UtcNow;
                _context.NguoiDungs.Update(user);
                await _context.SaveChangesAsync();
            }

            return user;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }
    }
}
