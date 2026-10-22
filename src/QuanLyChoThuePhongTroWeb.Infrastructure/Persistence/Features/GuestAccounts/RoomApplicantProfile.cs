using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.GuestAccounts;

internal static class RoomApplicantProfile
{
    // The caller saves a new profile together with its viewing/reservation transaction.
    internal static async Task<KhachVangLai?> ResolveAsync(ApplicationDbContext context,
        int actorId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var user = await context.NguoiDungs
            .Include(user => user.KhachVangLai)
            .Include(user => user.NguoiThue)
            .SingleOrDefaultAsync(user => user.NguoiDungId == actorId &&
                user.IsActive && !user.IsDeleted &&
                (user.Role == Role.KhachVangLai || user.Role == Role.KhachThue),
                cancellationToken);
        if (user is null)
            return null;

        if (user.Role == Role.KhachThue &&
            (user.NguoiThue is null || user.NguoiThue.IsDeleted))
            return null;
        if (user.KhachVangLai is { } profile)
            return profile;

        if (user.Role != Role.KhachThue || user.NguoiThue is not { } tenant ||
            string.IsNullOrWhiteSpace(tenant.HoVaTen) || tenant.HoVaTen.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(tenant.SoDienThoai) || tenant.SoDienThoai.Trim().Length > 20 ||
            tenant.Email?.Trim().Length > 150)
            return null;

        var email = string.IsNullOrWhiteSpace(tenant.Email) ? null : tenant.Email.Trim();
        return new KhachVangLai
        {
            NguoiDungId = actorId,
            HoTen = tenant.HoVaTen.Trim(),
            SoDienThoai = tenant.SoDienThoai.Trim(),
            Email = email,
            EmailNormalized = email?.ToUpperInvariant(),
            NgayTao = nowUtc
        };
    }
}
