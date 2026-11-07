using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class AdminSeedOptionsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Validate_EmptyPassword_IsValid(string? password)
        {
            new AdminSeedOptions { Password = password }.Validate();
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("ADMIN")]
        [InlineData("1234567")]
        [InlineData("password")]
        public void Validate_WeakOrShortPassword_Throws_WithoutLeakingPassword(string password)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new AdminSeedOptions { Password = password }.Validate());

            Assert.Contains("AdminSeed:Password", ex.Message);
            Assert.DoesNotContain(password, ex.Message);
        }

        [Fact]
        public void Validate_StrongPassword_IsValid()
        {
            new AdminSeedOptions { Password = "Quan ly tro 2026" }.Validate();
        }
    }

    public class LoginThrottleOptionsTests
    {
        [Fact]
        public void Defaults_AreValid()
        {
            var options = new LoginThrottleOptions();

            options.Validate();

            Assert.Equal(5, options.MaxFailuresPerUsername);
            Assert.Equal(20, options.MaxFailuresPerClient);
            Assert.Equal(15, options.WindowMinutes);
            Assert.Equal(15, options.LockMinutes);
            Assert.Equal(10_000, options.MaxTrackedKeys);
        }

        [Fact]
        public void MaxFailuresPerClient_Zero_IsValid()
        {
            new LoginThrottleOptions { MaxFailuresPerClient = 0 }.Validate();
        }

        [Theory]
        [InlineData(0, 20, 15, 15, 10_000, "MaxFailuresPerUsername")]
        [InlineData(5, -1, 15, 15, 10_000, "MaxFailuresPerClient")]
        [InlineData(5, 20, 0, 15, 10_000, "WindowMinutes")]
        [InlineData(5, 20, 15, 0, 10_000, "LockMinutes")]
        [InlineData(5, 20, 15, 15, 99, "MaxTrackedKeys")]
        public void InvalidValues_Throw(int user, int client, int window, int lockMinutes, int maxKeys, string key)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new LoginThrottleOptions
            {
                MaxFailuresPerUsername = user,
                MaxFailuresPerClient = client,
                WindowMinutes = window,
                LockMinutes = lockMinutes,
                MaxTrackedKeys = maxKeys
            }.Validate());

            Assert.Contains("LoginThrottle:" + key, ex.Message);
        }
    }
}
