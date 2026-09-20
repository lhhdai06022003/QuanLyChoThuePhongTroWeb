using Microsoft.AspNetCore.Identity;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
{
    public class NguoiDungPasswordTests
    {
        private readonly PasswordHasher<NguoiDung> _hasher = new();

        [Fact]
        public void PasswordHasher_HashAndVerify_Success()
        {
            // Arrange
            var user = new NguoiDung
            {
                TenDangNhap = "admin",
                Role = Role.Admin
            };
            string rawPassword = "AdminPassword123!";

            // Act
            string hash = _hasher.HashPassword(user, rawPassword);
            var result = _hasher.VerifyHashedPassword(user, hash, rawPassword);

            // Assert
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.Equal(PasswordVerificationResult.Success, result);
        }

        [Fact]
        public void PasswordHasher_WrongPassword_Failed()
        {
            // Arrange
            var user = new NguoiDung
            {
                TenDangNhap = "user1",
                Role = Role.KhachThue
            };
            string rawPassword = "CorrectPassword123!";
            string wrongPassword = "WrongPassword999!";

            // Act
            string hash = _hasher.HashPassword(user, rawPassword);
            var result = _hasher.VerifyHashedPassword(user, hash, wrongPassword);

            // Assert
            Assert.Equal(PasswordVerificationResult.Failed, result);
        }

        [Fact]
        public void AspNetPasswordService_HashAndVerify_Success()
        {
            var service = new Security.AspNetPasswordService(new PasswordHasher<NguoiDung>());
            string password = "StrongPassword@123";

            string hash = service.HashPassword(password);
            bool isValid = service.VerifyPassword(hash, password, out bool needsRehash);

            Assert.True(isValid);
            Assert.False(needsRehash);
        }

        [Fact]
        public void AspNetPasswordService_LegacySha256_VerifiesAndSetsNeedsRehash()
        {
            var service = new Security.AspNetPasswordService(new PasswordHasher<NguoiDung>());
            // SHA256 of "admin" is 8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918
            string legacyHash = "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918";

            bool isValid = service.VerifyPassword(legacyHash, "admin", out bool needsRehash);

            Assert.True(isValid);
            Assert.True(needsRehash);
        }
    }
}
