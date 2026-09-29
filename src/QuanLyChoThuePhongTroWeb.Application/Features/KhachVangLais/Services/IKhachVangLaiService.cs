using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Services;

public interface IKhachVangLaiService
{
    Task<GuestRegistrationResult> RegisterAsync(GuestRegistrationRequest request);
    Task<GuestProfileDto?> GetProfileAsync(int actorId);
    Task<GuestRegistrationResult> UpdateIdentityAsync(int actorId, UpdateGuestIdentityRequest request);
}
