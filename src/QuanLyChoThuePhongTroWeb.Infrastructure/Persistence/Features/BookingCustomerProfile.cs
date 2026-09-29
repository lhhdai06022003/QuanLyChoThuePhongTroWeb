using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

internal static class BookingCustomerProfile
{
    // Viewing and reservation tables use KhachVangLaiId for ownership. An existing
    // tenant keeps the same login and tenant record; this row only links new requests.
    public static async Task<KhachVangLai?> GetOrCreateAsync(ApplicationDbContext context, int actorId)
    {
        var user = await context.NguoiDungs.Include(item => item.NguoiThue)
            .FirstOrDefaultAsync(item => item.NguoiDungId == actorId && item.IsActive && !item.IsDeleted);
        if (user is null || user.Role is not (Role.KhachVangLai or Role.KhachThue)) return null;

        var profile = await context.KhachVangLais.FirstOrDefaultAsync(item => item.NguoiDungId == actorId);
        if (user.Role == Role.KhachVangLai) return profile;

        var tenant = user.NguoiThue;
        if (tenant is null || tenant.IsDeleted || string.IsNullOrWhiteSpace(tenant.HoVaTen) ||
            string.IsNullOrWhiteSpace(tenant.SoDienThoai)) return null;

        profile ??= new KhachVangLai { NguoiDungId = actorId, NgayTao = DateTime.UtcNow };
        profile.HoTen = tenant.HoVaTen.Trim();
        profile.SoDienThoai = tenant.SoDienThoai.Trim();
        profile.Email = tenant.Email?.Trim();
        profile.EmailNormalized = profile.Email?.ToUpperInvariant();
        profile.CCCD = tenant.CCCD?.Trim();
        profile.NgayCapNhat = DateTime.UtcNow;
        if (profile.KhachVangLaiId == 0) context.KhachVangLais.Add(profile);
        await context.SaveChangesAsync();
        return profile;
    }
}
