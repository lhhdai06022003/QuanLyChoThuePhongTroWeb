using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Persistence;

public interface IPhongCongKhaiStore
{
    Task<IReadOnlyList<PublicRoomRecord>> GetCandidatesAsync();
    Task<PublicRoomRecord?> GetByCodeAsync(string code);
}
