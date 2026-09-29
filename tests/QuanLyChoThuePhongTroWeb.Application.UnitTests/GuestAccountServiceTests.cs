using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests;

public class GuestAccountServiceTests
{
    [Fact]
    public async Task RegisterAsync_HashesPasswordAndCreatesGuest()
    {
        var store = new FakeStore();
        var result = await new KhachVangLaiService(store, new FakePasswords())
            .RegisterAsync(new GuestRegistrationRequest("guest1", "password123", "Nguyễn An", "0900000000", "an@example.com", "012345678901"));

        Assert.True(result.Success);
        Assert.Equal(17, result.NguoiDungId);
        Assert.Equal("hashed:password123", store.LastHash);
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateAndInvalidData()
    {
        var store = new FakeStore { CreateResult = null };
        var service = new KhachVangLaiService(store, new FakePasswords());
        Assert.False((await service.RegisterAsync(new("guest1", "password123", "Nguyễn An", "0900000000", "an@example.com", "012345678901"))).Success);
        Assert.False((await service.RegisterAsync(new("bad name", "short", "A", "abc", null, "bad"))).Success);
        Assert.False((await service.RegisterAsync(new("guest2", "password123", "Nguyễn An", "0900000000", null, "012345678901"))).Success);
    }

    private sealed class FakeStore : IKhachVangLaiStore
    {
        public int? CreateResult { get; set; } = 17;
        public string? LastHash { get; private set; }
        public Task<int?> TryRegisterAsync(string username, string hash, string name, string phone, string email, string cccd)
        {
            LastHash = hash;
            return Task.FromResult(CreateResult);
        }
        public Task<GuestProfileDto?> GetProfileAsync(int actorId) => Task.FromResult<GuestProfileDto?>(null);
        public Task<bool> UpdateIdentityAsync(int actorId, string email, string cccd) => Task.FromResult(true);
    }

    private sealed class FakePasswords : IPasswordService
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
