using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Persistence;

public interface IKhachVangLaiStore
{
    Task<int?> TryRegisterAsync(string username, string hash, string name, string phone, string email, string cccd);
    Task<GuestProfileDto?> GetProfileAsync(int actorId);
    Task<bool> UpdateIdentityAsync(int actorId, string email, string cccd);
}
