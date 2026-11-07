using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Chính sách mật khẩu và nhận diện mật khẩu mặc định (spec bảo mật xác thực §2.2, §2.4).
    public class PasswordPolicyTests
    {
        [Theory]
        [InlineData(7)]
        [InlineData(129)]
        public void Validate_LengthOutOfRange_ReturnsError(int length)
        {
            var error = PasswordPolicy.Validate(new string('x', length - 1) + "Q", "nguoidung", null);

            Assert.NotNull(error);
            Assert.Contains("8 đến 128", error);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(128)]
        public void Validate_LengthAtBoundary_IsValid(int length)
        {
            Assert.Null(PasswordPolicy.Validate(new string('x', length - 1) + "Q", "nguoidung", null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Validate_NullOrEmpty_ReturnsError(string? matKhau)
        {
            Assert.NotNull(PasswordPolicy.Validate(matKhau, "nguoidung", null));
        }

        [Fact]
        public void Validate_OnlyWhitespace_ReturnsError()
        {
            var error = PasswordPolicy.Validate("          ", "nguoidung", null);

            Assert.NotNull(error);
            Assert.Contains("khoảng trắng", error);
        }

        [Theory]
        [InlineData("NhanVien01", "nhanvien01")]
        [InlineData("nhanvien01", "NHANVIEN01")]
        [InlineData("khach@example.com", "Khach@Example.com")]
        public void Validate_SameAsUsername_IgnoringCase_ReturnsError(string matKhau, string tenDangNhap)
        {
            var error = PasswordPolicy.Validate(matKhau, tenDangNhap, null);

            Assert.NotNull(error);
            Assert.Contains("tên đăng nhập", error);
        }

        [Theory]
        [InlineData("0901234567")]
        [InlineData("0901 234 567")]
        [InlineData("84901234567")]
        [InlineData("+84901234567")]
        [InlineData("+84 901-234-567")]
        public void Validate_SameAsPhone_InAnyForm_ReturnsError(string matKhau)
        {
            var error = PasswordPolicy.Validate(matKhau, "khach@example.com", "0901234567");

            Assert.NotNull(error);
            Assert.Contains("số điện thoại", error);
        }

        [Fact]
        public void Validate_StoredPhoneWithCountryCode_MatchesLocalForm()
        {
            Assert.NotNull(PasswordPolicy.Validate("0901234567", "khach", "+84 901 234 567"));
        }

        [Fact]
        public void Validate_PasswordContainingPhoneAndLetters_IsValid()
        {
            Assert.Null(PasswordPolicy.Validate("abc0901234567", "khach", "0901234567"));
        }

        [Theory]
        [InlineData("password")]
        [InlineData("PASSWORD")]
        [InlineData("12345678")]
        [InlineData("123456789")]
        [InlineData("1234567890")]
        [InlineData("11111111")]
        [InlineData("00000000")]
        [InlineData("qwertyui")]
        [InlineData("QwertyUI")]
        [InlineData("abcd1234")]
        [InlineData("ABCD1234")]
        public void Validate_WeakList_ReturnsError(string matKhau)
        {
            var error = PasswordPolicy.Validate(matKhau, "nguoidung", null);

            Assert.NotNull(error);
            Assert.Contains("dễ đoán", error);
        }

        [Fact]
        public void Validate_NotExactlyWeakEntry_IsValid()
        {
            // Danh sách yếu so khớp nguyên chuỗi: "matkhau1" không phải "matkhau".
            Assert.Null(PasswordPolicy.Validate("matkhau1", "nguoidung", null));
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("matkhau")]
        public void Validate_ShortWeakEntries_AreRejected(string matKhau)
        {
            Assert.NotNull(PasswordPolicy.Validate(matKhau, "nguoidung", null));
        }

        [Fact]
        public void Validate_StrongPassword_IsValid()
        {
            Assert.Null(PasswordPolicy.Validate("Phong tro 2026", "nguoidung", "0901234567"));
        }

        [Theory]
        [InlineData("admin", "admin", null)]
        [InlineData("ADMIN", "quanly", null)]
        [InlineData("Admin", "nhanvien01", null)]
        public void LaMatKhauMacDinh_AdminPassword_IgnoringCase_IsTrue(string matKhau, string tenDangNhap, string? soDienThoai)
        {
            Assert.True(PasswordPolicy.LaMatKhauMacDinh(matKhau, tenDangNhap, soDienThoai));
        }

        [Theory]
        [InlineData("nhanvien01", "nhanvien01")]
        [InlineData("NhanVien01", "nhanvien01")]
        public void LaMatKhauMacDinh_SameAsUsername_IsTrue(string matKhau, string tenDangNhap)
        {
            Assert.True(PasswordPolicy.LaMatKhauMacDinh(matKhau, tenDangNhap, null));
        }

        [Theory]
        [InlineData("0901234567", "0901234567")]
        [InlineData("0901234567", "0901 234 567")]
        [InlineData("84901234567", "0901234567")]
        [InlineData("+84901234567", "0901234567")]
        [InlineData("0901234567", "+84901234567")]
        public void LaMatKhauMacDinh_SameAsPhone_IsTrue(string matKhau, string soDienThoai)
        {
            Assert.True(PasswordPolicy.LaMatKhauMacDinh(matKhau, "khach@example.com", soDienThoai));
        }

        [Fact]
        public void LaMatKhauMacDinh_PhonePassword_WithoutPhone_IsFalse()
        {
            Assert.False(PasswordPolicy.LaMatKhauMacDinh("0901234567", "khach@example.com", null));
        }

        [Fact]
        public void LaMatKhauMacDinh_OwnStrongPassword_IsFalse()
        {
            Assert.False(PasswordPolicy.LaMatKhauMacDinh("Password123!", "khach@example.com", "0901234567"));
        }

        [Theory]
        [InlineData("0901234567", "0901234567")]
        [InlineData("0901 234 567", "0901234567")]
        [InlineData("+84 901-234-567", "0901234567")]
        [InlineData("84901234567", "0901234567")]
        [InlineData("abc", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ChuanHoaSoDienThoai_KeepsDigits_AndReplacesCountryCode(string? input, string? expected)
        {
            Assert.Equal(expected, PasswordPolicy.ChuanHoaSoDienThoai(input));
        }
    }
}
