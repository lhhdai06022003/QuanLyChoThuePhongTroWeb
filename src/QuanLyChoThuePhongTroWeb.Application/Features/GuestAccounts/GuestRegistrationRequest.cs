namespace QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;

public sealed record GuestRegistrationRequest(string Username, string Password,
    string FullName, string Phone, string Email);
