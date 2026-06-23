using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Data;
using QuanLyChoThuePhongTroWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.Services.NguoiDungs
{
    public class NguoiDungService : INguoiDungService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<NguoiDung> _passwordHasher;

        public NguoiDungService(ApplicationDbContext context, IPasswordHasher<NguoiDung> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // --- CHỨC NĂNG ĐĂNG NHẬP & SEED DATA ---

        public async Task SeedAdminAccountAsync()
        {
            var admin = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.TenDangNhap == "admin" && !x.IsDeleted);
            if (admin == null)
            {
                admin = new NguoiDung
                {
                    TenDangNhap = "admin",
                    Role = Role.Admin,
                    IsActive = true,
                    NgayTao = DateTime.UtcNow
                };
                admin.MatKhauHash = _passwordHasher.HashPassword(admin, "admin");
                _context.NguoiDungs.Add(admin);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Tự động nâng cấp mật khẩu của admin cũ từ SHA256 sang PBKDF2
                // SHA256 của "admin" = 8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918
                if (admin.MatKhauHash == "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918")
                {
                    admin.MatKhauHash = _passwordHasher.HashPassword(admin, "admin");
                    _context.NguoiDungs.Update(admin);
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task<NguoiDung?> ValidateUserAsync(string username, string password)
        {
            var user = await _context.NguoiDungs
                .FirstOrDefaultAsync(x => x.TenDangNhap == username && x.IsActive && !x.IsDeleted);

            if (user != null)
            {
                bool isPasswordValid = false;
                bool needsRehash = false;

                // 1. Kiểm tra bằng IPasswordHasher (PBKDF2)
                var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.MatKhauHash, password);
                if (verificationResult == PasswordVerificationResult.Success)
                {
                    isPasswordValid = true;
                }
                else if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    isPasswordValid = true;
                    needsRehash = true;
                }
                else
                {
                    // 2. Thử so khớp bằng SHA256 cũ (đối với tài khoản chưa nâng cấp)
                    var oldSha256Hash = HashPasswordSha256(password);
                    if (user.MatKhauHash == oldSha256Hash)
                    {
                        isPasswordValid = true;
                        needsRehash = true; // Yêu cầu băm lại sang PBKDF2
                    }
                }

                if (isPasswordValid)
                {
                    // Cập nhật lần đăng nhập cuối
                    user.LanDangNhapCuoi = DateTime.UtcNow;

                    if (needsRehash)
                    {
                        user.MatKhauHash = _passwordHasher.HashPassword(user, password);
                    }

                    _context.NguoiDungs.Update(user);
                    await _context.SaveChangesAsync();
                    return user;
                }
            }

            return null;
        }

        private string HashPasswordSha256(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }


        // --- CÁC CHỨC NĂNG CRUD ---

        public async Task<IEnumerable<NguoiDung>> GetAllAsync()
        {
            return await _context.NguoiDungs
                .Where(x => !x.IsDeleted)
                .Include(x => x.NguoiThue)
                .ToListAsync();
        }

        public async Task<NguoiDung?> GetByIdAsync(int id)
        {
            return await _context.NguoiDungs
                .Include(x => x.NguoiThue)
                .FirstOrDefaultAsync(x => x.NguoiDungId == id && !x.IsDeleted);
        }

        public async Task AddAsync(NguoiDung nguoiDung)
        {
            if (!string.IsNullOrEmpty(nguoiDung.MatKhauHash))
            {
                nguoiDung.MatKhauHash = _passwordHasher.HashPassword(nguoiDung, nguoiDung.MatKhauHash);
            }

            _context.NguoiDungs.Add(nguoiDung);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(NguoiDung nguoiDung)
        {
            _context.NguoiDungs.Update(nguoiDung);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateUserAsync(int id, Role role, bool isActive, string? newPassword)
        {
            var existingUser = await _context.NguoiDungs.FirstOrDefaultAsync(x => x.NguoiDungId == id && !x.IsDeleted);
            if (existingUser == null)
            {
                return false;
            }

            existingUser.Role = role;
            existingUser.IsActive = isActive;

            if (!string.IsNullOrEmpty(newPassword))
            {
                existingUser.MatKhauHash = _passwordHasher.HashPassword(existingUser, newPassword);
            }

            _context.NguoiDungs.Update(existingUser);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(bool IsSuccess, string Message)> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            var user = await _context.NguoiDungs.FirstOrDefaultAsync(u => u.NguoiDungId == userId && !u.IsDeleted);
            if (user == null)
            {
                return (false, "Tài khoản không tồn tại.");
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.MatKhauHash, oldPassword);
            bool isOldPasswordValid = false;

            if (verificationResult == PasswordVerificationResult.Success || verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                isOldPasswordValid = true;
            }
            else
            {
                var oldSha256Hash = HashPasswordSha256(oldPassword);
                if (user.MatKhauHash == oldSha256Hash)
                {
                    isOldPasswordValid = true;
                }
            }

            if (!isOldPasswordValid)
            {
                return (false, "Mật khẩu hiện tại không chính xác.");
            }

            user.MatKhauHash = _passwordHasher.HashPassword(user, newPassword);
            _context.NguoiDungs.Update(user);
            await _context.SaveChangesAsync();

            return (true, "Đổi mật khẩu thành công!");
        }

        public async Task DeleteAsync(int id)
        {
            var user = await _context.NguoiDungs.FindAsync(id);
            if (user != null)
            {
                user.IsDeleted = true;
                user.IsActive = false;

                _context.NguoiDungs.Update(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}