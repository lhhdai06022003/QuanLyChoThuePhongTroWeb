using System;
using System.Linq;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Đăng nhập, seed admin và đặt mật khẩu của NguoiDungService (spec bảo mật xác thực §2.1, §2.2, §2.4, §2.6).
    public class NguoiDungServiceTests
    {
        private static readonly DateTimeOffset Start = new(2026, 11, 6, 3, 0, 0, TimeSpan.Zero);

        private readonly FakeNguoiDungStore _store = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakePasswordService _passwords = new();
        private readonly FixedTimeProvider _time = new(Start);
        private readonly InMemoryLoginAttemptThrottle _throttle;

        public NguoiDungServiceTests()
        {
            _throttle = new InMemoryLoginAttemptThrottle(new LoginThrottleOptions(), _time, new CapturingLogger<InMemoryLoginAttemptThrottle>());
        }

        private NguoiDungService Create(string? adminSeedPassword = null) =>
            new(_store, _unitOfWork, _passwords, new AdminSeedOptions { Password = adminSeedPassword }, _throttle);

        private NguoiDung AddUser(string ten, string matKhau, Role role, string? soDienThoai = null, bool isActive = true)
        {
            var user = new NguoiDung
            {
                NguoiDungId = _store.Users.Count + 1,
                TenDangNhap = ten,
                MatKhauHash = "hash:" + matKhau,
                Role = role,
                IsActive = isActive
            };

            if (soDienThoai != null)
            {
                user.NguoiThueId = 100 + user.NguoiDungId;
                user.NguoiThue = new NguoiThue
                {
                    NguoiThueId = user.NguoiThueId.Value,
                    HoVaTen = "Khách " + ten,
                    Email = ten,
                    SoDienThoai = soDienThoai,
                    CCCD = "079" + user.NguoiDungId
                };
            }

            _store.Users.Add(user);
            return user;
        }

        // --- Seed admin ---

        [Fact]
        public async Task Seed_AdminExists_ReturnsDaCo_AndDoesNotTouchAccount()
        {
            var admin = AddUser("admin", "admin", Role.Admin);
            // Hash SHA256 cũ của "admin": bản cũ đặt lại thành "admin", bản mới không đụng.
            admin.MatKhauHash = "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918";

            var result = await Create("Mat khau moi 2026").SeedAdminAccountAsync();

            Assert.Equal(SeedAdminResult.DaCo, result);
            Assert.Equal(0, _store.UpdateCount);
            Assert.Empty(_store.Added);
            Assert.Equal(0, _unitOfWork.SaveCount);
            Assert.Equal("8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918", admin.MatKhauHash);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Seed_NoAdmin_NoPassword_ReturnsThieuMatKhau_AndCreatesNothing(string? password)
        {
            var result = await Create(password).SeedAdminAccountAsync();

            Assert.Equal(SeedAdminResult.ThieuMatKhau, result);
            Assert.Empty(_store.Added);
            Assert.Equal(0, _unitOfWork.SaveCount);
        }

        [Fact]
        public async Task Seed_OnlySoftDeletedAdmin_NoPassword_ReturnsThieuMatKhau()
        {
            AddUser("admin", "admin", Role.Admin).IsDeleted = true;

            var result = await Create().SeedAdminAccountAsync();

            Assert.Equal(SeedAdminResult.ThieuMatKhau, result);
            Assert.Empty(_store.Added);
        }

        [Fact]
        public async Task Seed_NoAdmin_WithPassword_CreatesAdminWithThatPassword()
        {
            var result = await Create("Mat khau moi 2026").SeedAdminAccountAsync();

            Assert.Equal(SeedAdminResult.DaTao, result);
            var admin = Assert.Single(_store.Added);
            Assert.Equal("admin", admin.TenDangNhap);
            Assert.Equal(Role.Admin, admin.Role);
            Assert.True(admin.IsActive);
            Assert.Equal("hash:Mat khau moi 2026", admin.MatKhauHash);
            Assert.Equal(1, _unitOfWork.SaveCount);
        }

        // --- Đăng nhập ---

        [Fact]
        public async Task Login_CorrectPassword_ReturnsThanhCong_AndRecordsLastLogin()
        {
            var user = AddUser("nhanvien01", "Password123!", Role.NhanVien);

            var result = await Create().LoginAsync("nhanvien01", "Password123!", null);

            Assert.Equal(LoginStatus.ThanhCong, result.Status);
            Assert.NotNull(result.User);
            Assert.Equal(user.NguoiDungId, result.User!.NguoiDungId);
            Assert.Equal(AppRole.NhanVien, result.User.Role);
            Assert.False(result.User.BatBuocDoiMatKhau);
            Assert.NotNull(user.LanDangNhapCuoi);
            Assert.Equal(1, _store.UpdateCount);
            Assert.Equal(1, _unitOfWork.SaveCount);
        }

        [Fact]
        public async Task Login_LegacyHash_IsRehashed()
        {
            var user = AddUser("nhanvien01", "Password123!", Role.NhanVien);
            user.MatKhauHash = "legacy:Password123!";

            var result = await Create().LoginAsync("nhanvien01", "Password123!", null);

            Assert.Equal(LoginStatus.ThanhCong, result.Status);
            Assert.Equal("hash:Password123!", user.MatKhauHash);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsSai()
        {
            var user = AddUser("nhanvien01", "Password123!", Role.NhanVien);

            var result = await Create().LoginAsync("nhanvien01", "sai-mat-khau", null);

            Assert.Equal(LoginStatus.Sai, result.Status);
            Assert.Null(result.User);
            Assert.Null(user.LanDangNhapCuoi);
            Assert.Equal(0, _unitOfWork.SaveCount);
        }

        [Fact]
        public async Task Login_UnknownUser_StillVerifiesOnce_AndReturnsSai()
        {
            var result = await Create().LoginAsync("khong_ton_tai", "Password123!", null);

            Assert.Equal(LoginStatus.Sai, result.Status);
            Assert.Equal(1, _passwords.VerifyCount);
        }

        [Fact]
        public async Task Login_InactiveUser_StillVerifiesOnce_AndReturnsSai()
        {
            AddUser("nhanvien01", "Password123!", Role.NhanVien, isActive: false);

            var result = await Create().LoginAsync("nhanvien01", "Password123!", null);

            Assert.Equal(LoginStatus.Sai, result.Status);
            Assert.Equal(1, _passwords.VerifyCount);
        }

        [Fact]
        public async Task Login_AfterFiveFailures_CorrectPassword_IsTamKhoa_WithoutVerifying()
        {
            AddUser("nhanvien01", "Password123!", Role.NhanVien);
            var service = Create();
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(LoginStatus.Sai, (await service.LoginAsync("nhanvien01", "sai-mat-khau", null)).Status);
            }

            var verifyBefore = _passwords.VerifyCount;
            var result = await service.LoginAsync("nhanvien01", "Password123!", null);

            Assert.Equal(LoginStatus.TamKhoa, result.Status);
            Assert.Equal(15, result.SoPhutCho);
            Assert.Null(result.User);
            Assert.Equal(verifyBefore, _passwords.VerifyCount);
        }

        [Fact]
        public async Task Login_Locked_SoPhutCho_IsRoundedUp()
        {
            AddUser("nhanvien01", "Password123!", Role.NhanVien);
            var service = Create();
            for (var i = 0; i < 5; i++)
            {
                await service.LoginAsync("nhanvien01", "sai-mat-khau", null);
            }

            _time.SetUtcNow(Start.AddMinutes(10).AddSeconds(30));
            var result = await service.LoginAsync("nhanvien01", "Password123!", null);

            Assert.Equal(LoginStatus.TamKhoa, result.Status);
            Assert.Equal(5, result.SoPhutCho);
        }

        [Fact]
        public async Task Login_UnknownUsernames_AreCounted_AndLocked()
        {
            var service = Create();
            for (var i = 0; i < 5; i++)
            {
                await service.LoginAsync("khong_ton_tai", "sai", null);
            }

            Assert.Equal(LoginStatus.TamKhoa, (await service.LoginAsync("khong_ton_tai", "sai", null)).Status);
        }

        [Fact]
        public async Task Login_Success_ClearsFailureCount()
        {
            AddUser("nhanvien01", "Password123!", Role.NhanVien);
            var service = Create();
            for (var i = 0; i < 4; i++)
            {
                await service.LoginAsync("nhanvien01", "sai-mat-khau", null);
            }

            Assert.Equal(LoginStatus.ThanhCong, (await service.LoginAsync("nhanvien01", "Password123!", null)).Status);

            for (var i = 0; i < 4; i++)
            {
                await service.LoginAsync("nhanvien01", "sai-mat-khau", null);
            }

            Assert.Equal(LoginStatus.ThanhCong, (await service.LoginAsync("nhanvien01", "Password123!", null)).Status);
        }

        [Fact]
        public async Task Login_AdminWithAdminPassword_IsForcedToChange()
        {
            AddUser("admin", "admin", Role.Admin);

            var result = await Create().LoginAsync("admin", "admin", null);

            Assert.Equal(LoginStatus.ThanhCong, result.Status);
            Assert.True(result.User!.BatBuocDoiMatKhau);
        }

        [Fact]
        public async Task Login_PasswordSameAsUsername_IsForcedToChange()
        {
            AddUser("nhanvien01", "nhanvien01", Role.NhanVien);

            var result = await Create().LoginAsync("nhanvien01", "nhanvien01", null);

            Assert.True(result.User!.BatBuocDoiMatKhau);
        }

        [Theory]
        [InlineData("0901234567", "0901234567")]
        [InlineData("0901 234 567", "0901 234 567")]
        [InlineData("0901234567", "84901234567")]
        [InlineData("+84901234567", "0901234567")]
        public async Task Login_TenantWithPhonePassword_IsForcedToChange(string soDienThoai, string matKhau)
        {
            AddUser("khach@example.com", matKhau, Role.KhachThue, soDienThoai);

            var result = await Create().LoginAsync("khach@example.com", matKhau, null);

            Assert.Equal(LoginStatus.ThanhCong, result.Status);
            Assert.True(result.User!.BatBuocDoiMatKhau);
            Assert.NotNull(result.User.NguoiThueId);
        }

        [Fact]
        public async Task Login_TenantWithOwnPassword_IsNotForced()
        {
            AddUser("khach@example.com", "Password123!", Role.KhachThue, "0901234567");

            var result = await Create().LoginAsync("khach@example.com", "Password123!", null);

            Assert.False(result.User!.BatBuocDoiMatKhau);
        }

        [Fact]
        public async Task Login_GuestWithPasswordSameAsUsername_IsNotForced()
        {
            AddUser("khachvanglai", "khachvanglai", Role.KhachVangLai);

            var result = await Create().LoginAsync("khachvanglai", "khachvanglai", null);

            Assert.Equal(LoginStatus.ThanhCong, result.Status);
            Assert.False(result.User!.BatBuocDoiMatKhau);
        }

        [Fact]
        public async Task Login_GuestWithAdminPassword_IsNotForced()
        {
            AddUser("khachvanglai", "admin", Role.KhachVangLai);

            var result = await Create().LoginAsync("khachvanglai", "admin", null);

            Assert.False(result.User!.BatBuocDoiMatKhau);
        }

        [Fact]
        public async Task ValidateUser_ReturnsUserOnSuccess_AndNullOnFailure()
        {
            AddUser("nhanvien01", "Password123!", Role.NhanVien);
            var service = Create();

            Assert.NotNull(await service.ValidateUserAsync("nhanvien01", "Password123!"));
            Assert.Null(await service.ValidateUserAsync("nhanvien01", "sai-mat-khau"));
        }

        // --- Đổi mật khẩu ---

        [Fact]
        public async Task ChangePassword_WrongOldPassword_Fails()
        {
            var user = AddUser("nhanvien01", "Password123!", Role.NhanVien);

            var result = await Create().ChangePasswordAsync(user.NguoiDungId, "sai-mat-khau", "Mat khau moi 2026");

            Assert.False(result.Success);
            Assert.Contains("không chính xác", result.Message);
            Assert.Equal("hash:Password123!", user.MatKhauHash);
        }

        [Fact]
        public async Task ChangePassword_SameAsOld_Fails()
        {
            var user = AddUser("nhanvien01", "Password123!", Role.NhanVien);

            var result = await Create().ChangePasswordAsync(user.NguoiDungId, "Password123!", "Password123!");

            Assert.False(result.Success);
            Assert.Equal("Mật khẩu mới phải khác mật khẩu hiện tại.", result.Message);
            Assert.Equal(0, _unitOfWork.SaveCount);
        }

        [Theory]
        [InlineData("ngan")]
        [InlineData("nhanvien01")]
        [InlineData("12345678")]
        public async Task ChangePassword_PolicyViolation_Fails(string newPassword)
        {
            var user = AddUser("nhanvien01", "Password123!", Role.NhanVien);

            var result = await Create().ChangePasswordAsync(user.NguoiDungId, "Password123!", newPassword);

            Assert.False(result.Success);
            Assert.Equal("hash:Password123!", user.MatKhauHash);
            Assert.Equal(0, _unitOfWork.SaveCount);
        }

        [Fact]
        public async Task ChangePassword_TenantNewPasswordIsPhone_Fails()
        {
            var user = AddUser("khach@example.com", "0901234567", Role.KhachThue, "0901234567");

            var result = await Create().ChangePasswordAsync(user.NguoiDungId, "0901234567", "+84901234567");

            Assert.False(result.Success);
            Assert.Contains("số điện thoại", result.Message);
        }

        [Fact]
        public async Task ChangePassword_Valid_UpdatesHash_AndIsNoLongerForced()
        {
            var user = AddUser("khach@example.com", "0901234567", Role.KhachThue, "0901234567");
            var service = Create();

            var result = await service.ChangePasswordAsync(user.NguoiDungId, "0901234567", "Mat khau moi 2026");

            Assert.True(result.Success, result.Message);
            Assert.Equal("hash:Mat khau moi 2026", user.MatKhauHash);
            Assert.Equal(1, _unitOfWork.SaveCount);

            var login = await service.LoginAsync("khach@example.com", "Mat khau moi 2026", null);
            Assert.False(login.User!.BatBuocDoiMatKhau);
        }

        // --- Tạo và sửa tài khoản ---

        [Theory]
        [InlineData("ngan")]
        [InlineData("nhanvien_moi")]
        [InlineData("password")]
        public async Task Add_PolicyViolation_Fails_AndCreatesNothing(string matKhau)
        {
            var result = await Create().AddAsync(new CreateNguoiDungReq { TenDangNhap = "nhanvien_moi", MatKhau = matKhau, Role = AppRole.NhanVien });

            Assert.False(result.Success);
            Assert.Empty(_store.Added);
        }

        [Fact]
        public async Task Add_RestoreSoftDeleted_PolicyViolation_Fails()
        {
            var deleted = AddUser("nhanvien_cu", "Password123!", Role.NhanVien);
            deleted.IsDeleted = true;

            var result = await Create().AddAsync(new CreateNguoiDungReq { TenDangNhap = "nhanvien_cu", MatKhau = "1234567", Role = AppRole.NhanVien });

            Assert.False(result.Success);
            Assert.True(deleted.IsDeleted);
            Assert.Equal("hash:Password123!", deleted.MatKhauHash);
        }

        [Fact]
        public async Task Add_ValidPassword_Creates()
        {
            var result = await Create().AddAsync(new CreateNguoiDungReq { TenDangNhap = "nhanvien_moi", MatKhau = "Password123!", Role = AppRole.NhanVien });

            Assert.True(result.Success, result.Message);
            Assert.Equal("hash:Password123!", Assert.Single(_store.Added).MatKhauHash);
        }

        [Fact]
        public async Task Update_PolicyViolation_Fails()
        {
            var user = AddUser("khach@example.com", "Password123!", Role.KhachThue, "0901234567");

            var result = await Create().UpdateUserAsync(user.NguoiDungId, AppRole.KhachThue, true, "0901 234 567", user.NguoiThueId);

            Assert.False(result.Success);
            Assert.Equal("hash:Password123!", user.MatKhauHash);
        }

        [Fact]
        public async Task Update_WithoutPassword_KeepsHash()
        {
            var user = AddUser("nhanvien01", "Password123!", Role.NhanVien);

            var result = await Create().UpdateUserAsync(user.NguoiDungId, AppRole.NhanVien, false, null, null);

            Assert.True(result.Success, result.Message);
            Assert.False(user.IsActive);
            Assert.Equal("hash:Password123!", user.MatKhauHash);
        }
    }
}
