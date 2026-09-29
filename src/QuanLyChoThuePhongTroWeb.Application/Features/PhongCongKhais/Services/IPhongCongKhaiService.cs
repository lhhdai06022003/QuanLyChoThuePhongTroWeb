using QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongCongKhais.Services;

public interface IPhongCongKhaiService
{
    Task<PublicRoomPageDto> SearchAsync(PublicRoomQuery query);
    Task<PublicRoomDto?> GetAsync(string code);
}
