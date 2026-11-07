using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

// Cổng khách vãng lai dùng chính sách mật khẩu chung (spec bảo mật xác thực, mục chính sách mật khẩu).
public sealed class GuestRegistrationPasswordPolicyTests
{
    [Theory]
    [InlineData("visitor01", "VISITOR01", "0912345678")]
    [InlineData("visitor01", "0912345678", "0912345678")]
    [InlineData("visitor01", "+84912345678", "0912345678")]
    [InlineData("visitor01", "12345678", "0912345678")]
    [InlineData("visitor01", "Password", "0912345678")]
    [InlineData("visitor01", "        ", "0912345678")]
    [InlineData("visitor01", "1234567", "0912345678")]
    public async Task Register_PolicyViolation_DoesNotCreateAccount(string username, string password, string phone)
    {
        var store = new CountingStore();
        var service = new GuestRegistrationService(store, new HashOnlyPasswordService());

        var result = await service.RegisterAsync(new GuestRegistrationRequest(
            username, password, "Nguyễn Văn A", phone, "a@example.com"));

        Assert.False(result.Success);
        Assert.Equal(0, store.CreateCalls);
    }

    [Theory]
    [InlineData("Mat khau dai hop le")]
    [InlineData("a1b2c3d4")]
    public async Task Register_PolicyCompliantPassword_CreatesAccount(string password)
    {
        var store = new CountingStore();
        var service = new GuestRegistrationService(store, new HashOnlyPasswordService());

        var result = await service.RegisterAsync(new GuestRegistrationRequest(
            "visitor01", password, "Nguyễn Văn A", "0912345678", "a@example.com"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, store.CreateCalls);
    }

    [Fact]
    public async Task Register_Password128Characters_IsAccepted_And129IsRejected()
    {
        var store = new CountingStore();
        var service = new GuestRegistrationService(store, new HashOnlyPasswordService());

        var ok = await service.RegisterAsync(new GuestRegistrationRequest(
            "visitor01", new string('a', 128), "Nguyễn Văn A", "0912345678", "a@example.com"));
        var tooLong = await service.RegisterAsync(new GuestRegistrationRequest(
            "visitor02", new string('a', 129), "Nguyễn Văn A", "0912345679", "b@example.com"));

        Assert.True(ok.Success, ok.Message);
        Assert.False(tooLong.Success);
        Assert.Equal(1, store.CreateCalls);
    }

    private sealed class CountingStore : IGuestAccountStore
    {
        public int CreateCalls { get; private set; }

        public Task<int?> TryCreateAsync(NguoiDung user, KhachVangLai profile,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult<int?>(7);
        }
    }

    private sealed class HashOnlyPasswordService : IPasswordService
    {
        public string HashPassword(string password) => "hashed:" + password;
        public bool VerifyPassword(string hashedPassword, string providedPassword, out bool needsRehash)
        {
            needsRehash = false;
            return false;
        }
        public bool VerifyPassword(string hashedPassword, string providedPassword) => false;
    }
}
