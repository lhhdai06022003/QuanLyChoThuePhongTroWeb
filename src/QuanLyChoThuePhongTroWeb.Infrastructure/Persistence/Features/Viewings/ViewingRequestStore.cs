using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.Viewings;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Viewings;

public sealed class ViewingRequestStore(ApplicationDbContext context) : IViewingRequestStore
{
    public async Task<IReadOnlyList<ViewingRequestDto>> ListMineAsync(int actorId,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var requests = await context.YeuCauXemPhongs.AsNoTracking()
            .Where(request => request.KhachVangLai != null &&
                request.KhachVangLai.NguoiDungId == actorId)
            .OrderByDescending(request => request.NgayTao)
            .Select(request => new
            {
                request.YeuCauXemPhongId,
                request.PhongTroId,
                request.PhongTro.SoPhong,
                request.ThoiGianMongMuon,
                request.ThoiGianXacNhan,
                request.TrangThai
            })
            .ToListAsync(cancellationToken);

        return requests.Select(request => new ViewingRequestDto(
            request.YeuCauXemPhongId, request.PhongTroId, request.SoPhong,
            request.ThoiGianMongMuon, request.ThoiGianXacNhan,
            request.TrangThai.ToString(),
            request.TrangThai is TrangThaiYeuCauXemPhong.ChoNhanVienXuLy or
                TrangThaiYeuCauXemPhong.DaXacNhanLich &&
                (request.ThoiGianXacNhan ?? request.ThoiGianMongMuon) > nowUtc))
            .ToArray();
    }

    public async Task<ViewingCancelError> CancelAsync(int actorId, int requestId,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var request = await context.YeuCauXemPhongs
            .Include(item => item.KhachVangLai)
            .SingleOrDefaultAsync(item => item.YeuCauXemPhongId == requestId &&
                item.KhachVangLai != null &&
                item.KhachVangLai.NguoiDungId == actorId, cancellationToken);
        if (request is null)
            return ViewingCancelError.NotFound;

        var due = request.ThoiGianXacNhan ?? request.ThoiGianMongMuon;
        if (due <= nowUtc || request.TrangThai is not
            (TrangThaiYeuCauXemPhong.ChoNhanVienXuLy or
             TrangThaiYeuCauXemPhong.DaXacNhanLich))
            return ViewingCancelError.TooLateOrClosed;

        var previous = request.TrangThai;
        request.TrangThai = TrangThaiYeuCauXemPhong.DaHuy;
        request.NgayCapNhat = nowUtc;
        context.LichSuTrangThaiYeuCauXemPhongs.Add(new LichSuTrangThaiYeuCauXemPhong
        {
            YeuCauXemPhongId = requestId,
            TrangThaiTruoc = previous,
            TrangThaiSau = request.TrangThai,
            LoaiTacNhan = LoaiTacNhan.NguoiDung,
            NguoiThucHienId = actorId,
            NgayThucHien = nowUtc,
            LyDo = "Khách hủy trước giờ xem"
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ViewingCancelError.None;
    }

    public async Task<IReadOnlyList<ViewingSlotDto>> ListAvailableSlotsAsync(int roomId,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var slots = await context.KhungGioXemPhongs.AsNoTracking()
            .Where(slot => slot.PhongTroId == roomId && slot.IsActive &&
                slot.ThoiGianBatDau > nowUtc)
            .Select(slot => new
            {
                slot.KhungGioXemPhongId,
                slot.ThoiGianBatDau,
                slot.ThoiGianKetThuc,
                slot.SoLuongToiDa,
                Used = context.YeuCauXemPhongs.Count(request =>
                    request.KhungGioXemPhongId == slot.KhungGioXemPhongId &&
                    request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich)
            })
            .Where(slot => slot.Used < slot.SoLuongToiDa)
            .OrderBy(slot => slot.ThoiGianBatDau)
            .ToListAsync(cancellationToken);

        return slots.Select(slot => new ViewingSlotDto(slot.KhungGioXemPhongId,
            slot.ThoiGianBatDau, slot.ThoiGianKetThuc,
            slot.SoLuongToiDa - slot.Used)).ToArray();
    }

    public async Task<ViewingCreateResult> CreateAsync(int actorId, ViewingRequestInput input,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var guestId = await context.KhachVangLais.AsNoTracking()
                .Where(guest => guest.NguoiDungId == actorId &&
                    guest.NguoiDung.IsActive && !guest.NguoiDung.IsDeleted &&
                    guest.NguoiDung.Role == Role.KhachVangLai)
                .Select(guest => (int?)guest.KhachVangLaiId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!guestId.HasValue)
                return new ViewingCreateResult(null, ViewingCreateError.GuestMissing);

            var roomAvailable = await context.PhongTros.AsNoTracking()
                .AnyAsync(room => room.PhongTroId == input.RoomId &&
                    !room.IsDeleted && !room.ChiNhanh.IsDeleted && room.DuocDangTin &&
                    room.TrangThai == TrangThaiPhong.Trong &&
                    !context.YeuCauGiuChos.Any(hold => hold.PhongTroId == room.PhongTroId &&
                        (hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan &&
                            hold.HanThanhToan > nowUtc ||
                         hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                         hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho)),
                    cancellationToken);
            if (!roomAvailable)
                return new ViewingCreateResult(null, ViewingCreateError.RoomUnavailable);

            KhungGioXemPhong? slot = null;
            if (input.SlotId.HasValue)
            {
                slot = await context.KhungGioXemPhongs.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.KhungGioXemPhongId == input.SlotId &&
                        item.PhongTroId == input.RoomId && item.IsActive &&
                        item.ThoiGianBatDau > nowUtc, cancellationToken);
                if (slot is null)
                    return new ViewingCreateResult(null, ViewingCreateError.SlotUnavailable);

                var used = await context.YeuCauXemPhongs.AsNoTracking()
                    .CountAsync(request => request.KhungGioXemPhongId == slot.KhungGioXemPhongId &&
                        request.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich,
                        cancellationToken);
                if (used >= slot.SoLuongToiDa)
                    return new ViewingCreateResult(null, ViewingCreateError.SlotFull);
            }

            var status = slot is null
                ? TrangThaiYeuCauXemPhong.ChoNhanVienXuLy
                : TrangThaiYeuCauXemPhong.DaXacNhanLich;
            var request = new YeuCauXemPhong
            {
                PhongTroId = input.RoomId,
                KhachVangLaiId = guestId.Value,
                KhungGioXemPhongId = slot?.KhungGioXemPhongId,
                HoTen = input.FullName.Trim(),
                SoDienThoai = input.Phone.Trim(),
                Email = input.Email.Trim(),
                ThoiGianMongMuon = slot?.ThoiGianBatDau ?? input.PreferredTimeUtc!.Value,
                ThoiGianXacNhan = slot?.ThoiGianBatDau,
                HinhThucXacNhan = slot is null ? null : HinhThucXacNhanLich.TuDong,
                NguonTao = NguonTaoYeuCauXemPhong.TrangCongKhai,
                TrangThai = status,
                GhiChu = input.Note?.Trim(),
                NgayTao = nowUtc
            };
            request.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauXemPhong
            {
                TrangThaiSau = status,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = nowUtc
            });
            context.YeuCauXemPhongs.Add(request);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ViewingCreateResult(request.YeuCauXemPhongId, ViewingCreateError.None);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return new ViewingCreateResult(null, ViewingCreateError.Conflict);
        }
    }
}
