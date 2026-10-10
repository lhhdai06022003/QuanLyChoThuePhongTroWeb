using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Viewings;

public sealed class ViewingAdminRequestStore(ApplicationDbContext context)
    : IViewingAdminRequestStore
{
    public async Task<IReadOnlyList<ViewingAdminRequestDto>> ListAsync(
        IReadOnlyCollection<int>? branchIds,
        CancellationToken cancellationToken = default)
    {
        var query = context.YeuCauXemPhongs.AsNoTracking();
        if (branchIds is not null)
            query = query.Where(v => branchIds.Contains(v.PhongTro.ChiNhanhId));
        var requests = await query.OrderByDescending(v => v.NgayTao).Take(200)
            .Select(v => new
            {
                v.YeuCauXemPhongId, v.PhongTroId, v.PhongTro.ChiNhanhId,
                v.PhongTro.SoPhong, v.HoTen, v.SoDienThoai, v.Email,
                v.ThoiGianMongMuon, v.ThoiGianXacNhan, v.TrangThai, v.GhiChu
            }).ToListAsync(cancellationToken);
        return requests.Select(v => new ViewingAdminRequestDto(
            v.YeuCauXemPhongId, v.PhongTroId, v.ChiNhanhId,
            v.SoPhong, v.HoTen, v.SoDienThoai, v.Email ?? string.Empty,
            v.ThoiGianMongMuon, v.ThoiGianXacNhan,
            v.TrangThai.ToString(), v.GhiChu)).ToArray();
    }

    public Task<int?> GetBranchIdAsync(int requestId,
        CancellationToken cancellationToken = default) =>
        context.YeuCauXemPhongs.AsNoTracking()
            .Where(v => v.YeuCauXemPhongId == requestId)
            .Select(v => (int?)v.PhongTro.ChiNhanhId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ViewingAdminError> UpdateAsync(int actorId, int requestId,
        ViewingAdminAction action, DateTime? confirmedTimeUtc, string? reason,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var request = await context.YeuCauXemPhongs
                .SingleOrDefaultAsync(v => v.YeuCauXemPhongId == requestId,
                    cancellationToken);
            if (request is null)
                return ViewingAdminError.NotFound;
            var previous = request.TrangThai;
            switch (action)
            {
                case ViewingAdminAction.Confirm:
                    if (previous != TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                        confirmedTimeUtc is null || confirmedTimeUtc <= nowUtc)
                        return ViewingAdminError.InvalidState;
                    request.ThoiGianXacNhan = confirmedTimeUtc;
                    request.HinhThucXacNhan = HinhThucXacNhanLich.NhanVien;
                    request.TrangThai = TrangThaiYeuCauXemPhong.DaXacNhanLich;
                    break;
                case ViewingAdminAction.Reject:
                    if (previous is not (TrangThaiYeuCauXemPhong.ChoNhanVienXuLy or
                        TrangThaiYeuCauXemPhong.DaXacNhanLich) ||
                        (request.ThoiGianXacNhan ?? request.ThoiGianMongMuon) <= nowUtc)
                        return ViewingAdminError.InvalidState;
                    request.TrangThai = TrangThaiYeuCauXemPhong.TuChoi;
                    break;
                case ViewingAdminAction.MarkViewed:
                    if (previous != TrangThaiYeuCauXemPhong.DaXacNhanLich ||
                        (request.ThoiGianXacNhan ?? request.ThoiGianMongMuon) > nowUtc)
                        return ViewingAdminError.InvalidState;
                    request.TrangThai = TrangThaiYeuCauXemPhong.DaXemPhong;
                    break;
                default:
                    return ViewingAdminError.InvalidState;
            }
            request.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauXemPhongs.Add(
                new LichSuTrangThaiYeuCauXemPhong
                {
                    YeuCauXemPhongId = requestId,
                    TrangThaiTruoc = previous,
                    TrangThaiSau = request.TrangThai,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung,
                    NguoiThucHienId = actorId,
                    NgayThucHien = nowUtc,
                    LyDo = reason
                });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ViewingAdminError.None;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return ViewingAdminError.Conflict;
        }
    }
}
