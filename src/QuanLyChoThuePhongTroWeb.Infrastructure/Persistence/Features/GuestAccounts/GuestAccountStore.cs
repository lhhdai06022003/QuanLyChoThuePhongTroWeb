using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.GuestAccounts;

public sealed class GuestAccountStore(ApplicationDbContext context) : IGuestAccountStore
{
    public async Task<int?> TryCreateAsync(NguoiDung user, KhachVangLai profile,
        CancellationToken cancellationToken = default)
    {
        var username = user.TenDangNhap.ToUpperInvariant();
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var exists = await context.NguoiDungs.AsNoTracking()
                .AnyAsync(existing => existing.TenDangNhap.ToUpper() == username,
                    cancellationToken);
            if (exists)
                return null;

            profile.NguoiDung = user;
            context.NguoiDungs.Add(user);
            context.KhachVangLais.Add(profile);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return user.NguoiDungId;
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure
                   or PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }
    }
}
