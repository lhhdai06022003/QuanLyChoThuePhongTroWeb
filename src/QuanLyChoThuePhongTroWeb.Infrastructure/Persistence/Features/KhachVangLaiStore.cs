using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.KhachVangLais.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features;

public sealed class KhachVangLaiStore(ApplicationDbContext context) : IKhachVangLaiStore
{
    public async Task<GuestProfileDto?> GetProfileAsync(int actorId)
    {
        var guest = await context.KhachVangLais.AsNoTracking()
            .Where(guest => guest.NguoiDungId == actorId && guest.NguoiDung.IsActive &&
                !guest.NguoiDung.IsDeleted && guest.NguoiDung.Role == Role.KhachVangLai)
            .Select(guest => new GuestProfileDto(guest.HoTen, guest.SoDienThoai,
                guest.Email, guest.CCCD)).FirstOrDefaultAsync();
        if (guest is not null) return guest;
        return await context.NguoiDungs.AsNoTracking()
            .Where(user => user.NguoiDungId == actorId && user.IsActive && !user.IsDeleted &&
                user.Role == Role.KhachThue && user.NguoiThue != null && !user.NguoiThue.IsDeleted)
            .Select(user => new GuestProfileDto(user.NguoiThue!.HoVaTen,
                user.NguoiThue.SoDienThoai, user.NguoiThue.Email, user.NguoiThue.CCCD))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateIdentityAsync(int actorId, string email, string cccd)
    {
        var guest = await context.KhachVangLais.FirstOrDefaultAsync(item =>
            item.NguoiDungId == actorId && item.NguoiDung.IsActive &&
            !item.NguoiDung.IsDeleted && item.NguoiDung.Role == Role.KhachVangLai);
        if (guest is null) return false;
        guest.Email = email;
        guest.EmailNormalized = email.ToUpperInvariant();
        guest.CCCD = cccd;
        guest.NgayCapNhat = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<int?> TryRegisterAsync(string username, string hash, string name, string phone, string email, string cccd)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            // Serialize registrations for the same username, including simultaneous POST requests.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({username}))");
            if (await context.NguoiDungs.AnyAsync(user => !user.IsDeleted &&
                    user.TenDangNhap.ToLower() == username)) return null;

            var user = new NguoiDung
            {
                TenDangNhap = username,
                MatKhauHash = hash,
                Role = Role.KhachVangLai,
                IsActive = true,
                NgayTao = DateTime.UtcNow,
                KhachVangLai = new KhachVangLai
                {
                    HoTen = name,
                    SoDienThoai = phone,
                    Email = email,
                    EmailNormalized = email.ToUpperInvariant(),
                    CCCD = cccd,
                    DaXacMinhEmail = false,
                    NgayTao = DateTime.UtcNow
                }
            };
            context.NguoiDungs.Add(user);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return user.NguoiDungId;
        }
        catch (Exception ex) when (DatabaseConflict.IsExpected(ex))
        {
            return null;
        }
    }
}
