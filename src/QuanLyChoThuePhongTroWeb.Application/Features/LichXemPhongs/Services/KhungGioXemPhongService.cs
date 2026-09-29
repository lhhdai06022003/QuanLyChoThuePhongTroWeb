using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.Services;

public sealed class KhungGioXemPhongService(IKhungGioXemPhongStore store) : IKhungGioXemPhongService
{
    public Task<IReadOnlyList<ManagedViewingSlotDto>> GetManagedAsync(int actorId) => store.GetManagedAsync(actorId);
    public Task<bool> CreateAsync(CreateViewingSlotRequest request, int actorId) =>
        actorId <= 0 || request.PhongTroId <= 0 || request.BatDau <= DateTimeOffset.UtcNow ||
            request.KetThuc <= request.BatDau || request.SucChua is < 1 or > 100
            ? Task.FromResult(false) : store.CreateAsync(request, actorId);
    public Task<bool> CloseAsync(int id, int actorId) => id <= 0 || actorId <= 0
        ? Task.FromResult(false) : store.CloseAsync(id, actorId);
}
