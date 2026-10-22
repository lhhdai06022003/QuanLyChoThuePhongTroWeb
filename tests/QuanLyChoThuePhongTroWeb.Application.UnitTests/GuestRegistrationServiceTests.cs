using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public sealed class GuestRegistrationServiceTests
{
    [Fact]
    public async Task Register_ValidData_CreatesGuestProfileWithHashedPassword()
    {
        var store = new FakeGuestAccountStore();
        var service = new GuestRegistrationService(store, new FakePasswordService());

        var result = await service.RegisterAsync(new GuestRegistrationRequest(
            "visitor", "strong-password", "Nguyễn Văn A", "0912345678", "A@Example.com"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(42, result.Data);
        Assert.Equal(Role.KhachVangLai, store.User!.Role);
        Assert.Equal("hashed:strong-password", store.User.MatKhauHash);
        Assert.Equal("A@Example.com", store.Profile!.Email);
        Assert.Equal("A@EXAMPLE.COM", store.Profile.EmailNormalized);
    }

    [Theory]
    [InlineData("ab", "strong-password", "Nguyễn Văn A", "0912345678", "a@example.com")]
    [InlineData("visitor", "short", "Nguyễn Văn A", "0912345678", "a@example.com")]
    [InlineData("visitor", "strong-password", "", "0912345678", "a@example.com")]
    [InlineData("visitor", "strong-password", "Nguyễn Văn A", "123", "a@example.com")]
    [InlineData("visitor", "strong-password", "Nguyễn Văn A", "0912345678", "invalid")]
    public async Task Register_InvalidInput_DoesNotCreateAccount(string username,
        string password, string fullName, string phone, string email)
    {
        var store = new FakeGuestAccountStore();
        var service = new GuestRegistrationService(store, new FakePasswordService());

        var result = await service.RegisterAsync(new GuestRegistrationRequest(
            username, password, fullName, phone, email));

        Assert.False(result.Success);
        Assert.Null(store.User);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsValidationFailure()
    {
        var store = new FakeGuestAccountStore { IsDuplicate = true };
        var service = new GuestRegistrationService(store, new FakePasswordService());

        var result = await service.RegisterAsync(new GuestRegistrationRequest(
            "visitor", "strong-password", "Nguyễn Văn A", "0912345678", "a@example.com"));

        Assert.False(result.Success);
        Assert.Contains("đã tồn tại", result.Message);
    }

    private sealed class FakeGuestAccountStore : IGuestAccountStore
    {
        public NguoiDung? User { get; private set; }
        public KhachVangLai? Profile { get; private set; }
        public bool IsDuplicate { get; set; }

        public Task<int?> TryCreateAsync(NguoiDung user, KhachVangLai profile,
            CancellationToken cancellationToken = default)
        {
            User = user;
            Profile = profile;
            return Task.FromResult<int?>(IsDuplicate ? null : 42);
        }
    }

    private sealed class FakePasswordService : IPasswordService
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
