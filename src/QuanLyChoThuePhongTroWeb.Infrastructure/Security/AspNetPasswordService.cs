using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Security
{
    public class AspNetPasswordService : IPasswordService
    {
        private readonly IPasswordHasher<NguoiDung> _passwordHasher;

        public AspNetPasswordService(IPasswordHasher<NguoiDung> passwordHasher)
        {
            _passwordHasher = passwordHasher;
        }

        public string HashPassword(string password)
        {
            return _passwordHasher.HashPassword(null!, password);
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword, out bool needsRehash)
        {
            needsRehash = false;
            if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            {
                return false;
            }

            // 1. Kiểm tra bằng PBKDF2 (ASP.NET Core Identity standard)
            try
            {
                var result = _passwordHasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
                if (result == PasswordVerificationResult.Success)
                {
                    return true;
                }

                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    needsRehash = true;
                    return true;
                }
            }
            catch
            {
                // Format hash không phải định dạng của PasswordHasher, chuyển sang kiểm tra legacy hash
            }

            // 2. Fallback kiểm tra SHA256 legacy hash
            var sha256Hash = HashSha256(providedPassword);
            if (string.Equals(hashedPassword, sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                needsRehash = true;
                return true;
            }

            return false;
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            return VerifyPassword(hashedPassword, providedPassword, out _);
        }

        private static string HashSha256(string input)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
