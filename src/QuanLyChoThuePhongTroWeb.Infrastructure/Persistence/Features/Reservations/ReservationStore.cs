using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.GuestAccounts;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Reservations;

public sealed class ReservationStore(ApplicationDbContext context) : IReservationStore
{
    public async Task ExpireUnpaidAsync(DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var expired = await context.YeuCauGiuChos
            .Include(h => h.YeuCauThanhToanGiuChos)
            .Where(h => h.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan &&
                h.HanThanhToan <= nowUtc &&
                !context.MinhChungThanhToanGiuChos.Any(e =>
                    e.YeuCauThanhToanGiuCho.YeuCauGiuChoId == h.YeuCauGiuChoId))
            .OrderBy(h => h.HanThanhToan).Take(100)
            .ToListAsync(cancellationToken);
        foreach (var hold in expired)
        {
            hold.TrangThai = TrangThaiYeuCauGiuCho.HetHan;
            hold.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauGiuChos.Add(
                new LichSuTrangThaiYeuCauGiuCho
                {
                    YeuCauGiuChoId = hold.YeuCauGiuChoId,
                    TrangThaiTruoc = TrangThaiYeuCauGiuCho.ChoThanhToan,
                    TrangThaiSau = TrangThaiYeuCauGiuCho.HetHan,
                    LoaiTacNhan = LoaiTacNhan.HeThong,
                    NgayThucHien = nowUtc,
                    LyDo = "Hết hạn chuyển tiền và chưa có minh chứng"
                });
            foreach (var payment in hold.YeuCauThanhToanGiuChos.Where(p =>
                         p.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan))
            {
                payment.TrangThai = TrangThaiYeuCauThanhToan.HetHan;
                payment.NgayCapNhat = nowUtc;
                context.LichSuTrangThaiYeuCauThanhToanGiuChos.Add(
                    new LichSuTrangThaiYeuCauThanhToanGiuCho
                    {
                        YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                        TrangThaiTruoc = TrangThaiYeuCauThanhToan.ChoThanhToan,
                        TrangThaiSau = TrangThaiYeuCauThanhToan.HetHan,
                        LoaiTacNhan = LoaiTacNhan.HeThong,
                        NgayThucHien = nowUtc
                    });
            }
        }
        if (expired.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ReservationCreateResult> CreateAsync(int actorId, int roomId,
        int? viewingId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var guest = await RoomApplicantProfile.ResolveAsync(context, actorId,
                nowUtc, cancellationToken);
            if (guest is null)
                return new(null, ReservationError.GuestMissing);

            var roomAvailable = await context.PhongTros.AsNoTracking()
                .AnyAsync(room => room.PhongTroId == roomId && !room.IsDeleted &&
                    !room.ChiNhanh.IsDeleted && room.DuocDangTin &&
                    room.TrangThai == TrangThaiPhong.Trong &&
                    !context.YeuCauGiuChos.Any(hold => hold.PhongTroId == roomId &&
                        (hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan ||
                         hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                         hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho)),
                    cancellationToken);
            if (!roomAvailable)
                return new(null, ReservationError.RoomUnavailable);

            if (viewingId.HasValue && !await context.YeuCauXemPhongs.AsNoTracking()
                    .AnyAsync(viewing => viewing.YeuCauXemPhongId == viewingId &&
                        viewing.KhachVangLai != null &&
                        viewing.KhachVangLai.NguoiDungId == actorId && viewing.PhongTroId == roomId &&
                        viewing.TrangThai != TrangThaiYeuCauXemPhong.TuChoi &&
                        viewing.TrangThai != TrangThaiYeuCauXemPhong.DaHuy,
                        cancellationToken))
                return new(null, ReservationError.ViewingUnavailable);

            if (await context.YeuCauGiuChos.AsNoTracking().AnyAsync(hold =>
                    hold.KhachVangLai.NguoiDungId == actorId && hold.PhongTroId == roomId &&
                    (hold.TrangThai == TrangThaiYeuCauGiuCho.MoiTao ||
                     hold.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan ||
                     hold.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                     hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho),
                    cancellationToken))
                return new(null, ReservationError.Duplicate);

            var hold = new YeuCauGiuCho
            {
                KhachVangLai = guest,
                PhongTroId = roomId,
                YeuCauXemPhongId = viewingId,
                TrangThai = TrangThaiYeuCauGiuCho.MoiTao,
                NgayTao = nowUtc
            };
            hold.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                TrangThaiSau = hold.TrangThai,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = nowUtc
            });
            context.YeuCauGiuChos.Add(hold);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(hold.YeuCauGiuChoId, ReservationError.None);
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            return new(null, ReservationError.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            return new(null, ReservationError.Conflict);
        }
    }

    public Task<int?> GetBranchIdAsync(int reservationId,
        CancellationToken cancellationToken = default) =>
        context.YeuCauGiuChos.AsNoTracking()
            .Where(hold => hold.YeuCauGiuChoId == reservationId)
            .Select(hold => (int?)hold.PhongTro.ChiNhanhId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<ReservationDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default) =>
        ListAsync(context.YeuCauGiuChos.AsNoTracking().Where(hold =>
            hold.KhachVangLai.NguoiDungId == actorId), cancellationToken);

    public Task<IReadOnlyList<ReservationDto>> ListForStaffAsync(
        IReadOnlyCollection<int>? branchIds, CancellationToken cancellationToken = default)
    {
        var query = context.YeuCauGiuChos.AsNoTracking();
        if (branchIds is not null)
            query = query.Where(hold => branchIds.Contains(hold.PhongTro.ChiNhanhId));
        return ListAsync(query, cancellationToken);
    }

    private static async Task<IReadOnlyList<ReservationDto>> ListAsync(
        IQueryable<YeuCauGiuCho> query, CancellationToken cancellationToken)
    {
        var holds = await query
            .Include(hold => hold.KhachVangLai)
            .Include(hold => hold.PhongTro)
            .Include(hold => hold.YeuCauThanhToanGiuChos)
                .ThenInclude(payment => payment.GiaoDichGiuChos)
            .OrderByDescending(hold => hold.NgayTao)
            .Take(200)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return holds.Select(hold =>
        {
            var payment = hold.YeuCauThanhToanGiuChos.OrderByDescending(p => p.NgayTao)
                .FirstOrDefault();
            return new ReservationDto(hold.YeuCauGiuChoId, hold.PhongTroId,
                hold.PhongTro.SoPhong, hold.PhongTro.ChiNhanhId,
                hold.KhachVangLai.HoTen, hold.TrangThai.ToString(),
                hold.SoTienGiuCho, hold.HanThanhToan, hold.HanKyHopDong,
                payment?.YeuCauThanhToanGiuChoId, payment?.MaYeuCau,
                payment?.TrangThai.ToString(),
                hold.YeuCauThanhToanGiuChos.Sum(p =>
                    p.GiaoDichGiuChos.Sum(g => g.SoTienThucNhan)),
                hold.TrangThai is TrangThaiYeuCauGiuCho.MoiTao or
                    TrangThaiYeuCauGiuCho.ChoThanhToan or
                    TrangThaiYeuCauGiuCho.ChoXacNhanTien or
                    TrangThaiYeuCauGiuCho.DangGiuCho);
        }).ToArray();
    }

    public async Task<ReservationError> ApproveAsync(int actorId, int reservationId,
        decimal amount, DateTime paymentDeadlineUtc, DateTime contractDeadlineUtc,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await context.YeuCauGiuChos.Include(h => h.PhongTro)
                .SingleOrDefaultAsync(h => h.YeuCauGiuChoId == reservationId,
                    cancellationToken);
            if (hold is null)
                return ReservationError.NotFound;
            if (hold.TrangThai != TrangThaiYeuCauGiuCho.MoiTao ||
                hold.PhongTro.IsDeleted || !hold.PhongTro.DuocDangTin ||
                hold.PhongTro.TrangThai != TrangThaiPhong.Trong ||
                await context.YeuCauGiuChos.AsNoTracking().AnyAsync(other =>
                    other.PhongTroId == hold.PhongTroId &&
                    other.YeuCauGiuChoId != reservationId &&
                    (other.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan ||
                     other.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                     other.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho),
                    cancellationToken))
                return ReservationError.InvalidState;

            hold.SoTienGiuCho = amount;
            hold.HanThanhToan = paymentDeadlineUtc;
            hold.HanKyHopDong = contractDeadlineUtc;
            hold.NguoiDuyetId = actorId;
            hold.NgayDuyet = nowUtc;
            hold.NgayCapNhat = nowUtc;
            hold.TrangThai = TrangThaiYeuCauGiuCho.ChoThanhToan;
            context.LichSuTrangThaiYeuCauGiuChos.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                YeuCauGiuChoId = reservationId,
                TrangThaiTruoc = TrangThaiYeuCauGiuCho.MoiTao,
                TrangThaiSau = hold.TrangThai,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = nowUtc
            });
            var code = "GC" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            var payment = new YeuCauThanhToanGiuCho
            {
                YeuCauGiuChoId = reservationId,
                MaYeuCau = code,
                SoTien = amount,
                NoiDungChuyenKhoan = code,
                HanThanhToan = paymentDeadlineUtc,
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiTaoId = actorId,
                NgayTao = nowUtc
            };
            payment.LichSuTrangThais.Add(new LichSuTrangThaiYeuCauThanhToanGiuCho
            {
                TrangThaiSau = payment.TrangThai,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = nowUtc
            });
            context.YeuCauThanhToanGiuChos.Add(payment);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReservationError.None;
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return ReservationError.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return ReservationError.Conflict;
        }
    }

    public async Task<ReservationError> CancelAsync(int actorId, int reservationId,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var hold = await context.YeuCauGiuChos
            .Include(h => h.YeuCauThanhToanGiuChos)
            .SingleOrDefaultAsync(h => h.YeuCauGiuChoId == reservationId &&
                h.KhachVangLai.NguoiDungId == actorId, cancellationToken);
        if (hold is null)
            return ReservationError.NotFound;
        if (hold.TrangThai is not (TrangThaiYeuCauGiuCho.MoiTao or
            TrangThaiYeuCauGiuCho.ChoThanhToan or
            TrangThaiYeuCauGiuCho.ChoXacNhanTien or
            TrangThaiYeuCauGiuCho.DangGiuCho))
            return ReservationError.InvalidState;

        var oldStatus = hold.TrangThai;
        hold.TrangThai = TrangThaiYeuCauGiuCho.DaHuy;
        hold.LyDoTuChoiHoacHuy = "Khách yêu cầu hủy";
        hold.NgayCapNhat = nowUtc;
        context.LichSuTrangThaiYeuCauGiuChos.Add(new LichSuTrangThaiYeuCauGiuCho
        {
            YeuCauGiuChoId = reservationId,
            TrangThaiTruoc = oldStatus,
            TrangThaiSau = hold.TrangThai,
            LoaiTacNhan = LoaiTacNhan.NguoiDung,
            NguoiThucHienId = actorId,
            NgayThucHien = nowUtc,
            LyDo = hold.LyDoTuChoiHoacHuy
        });
        foreach (var payment in hold.YeuCauThanhToanGiuChos.Where(payment =>
                     payment.TrangThai is TrangThaiYeuCauThanhToan.ChoThanhToan or
                         TrangThaiYeuCauThanhToan.DaBaoChuyen or
                         TrangThaiYeuCauThanhToan.DangDoiChieu))
        {
            var previous = payment.TrangThai;
            payment.TrangThai = TrangThaiYeuCauThanhToan.DaHuy;
            payment.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauThanhToanGiuChos.Add(
                new LichSuTrangThaiYeuCauThanhToanGiuCho
                {
                    YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                    TrangThaiTruoc = previous,
                    TrangThaiSau = payment.TrangThai,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung,
                    NguoiThucHienId = actorId,
                    NgayThucHien = nowUtc
                });
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ReservationError.None;
    }

    public async Task<ReservationError> ExtendContractDeadlineAsync(int actorId,
        int reservationId, DateTime newDeadlineUtc, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await context.YeuCauGiuChos.SingleOrDefaultAsync(h =>
                h.YeuCauGiuChoId == reservationId, cancellationToken);
            if (hold is null)
                return ReservationError.NotFound;
            if (hold.TrangThai != TrangThaiYeuCauGiuCho.DangGiuCho ||
                hold.HanKyHopDong is null || hold.HanKyHopDong > nowUtc ||
                newDeadlineUtc <= nowUtc)
                return ReservationError.InvalidState;

            hold.HanKyHopDong = newDeadlineUtc;
            hold.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauGiuChos.Add(
                new LichSuTrangThaiYeuCauGiuCho
                {
                    YeuCauGiuChoId = reservationId,
                    TrangThaiTruoc = TrangThaiYeuCauGiuCho.DangGiuCho,
                    TrangThaiSau = TrangThaiYeuCauGiuCho.DangGiuCho,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung,
                    NguoiThucHienId = actorId,
                    NgayThucHien = nowUtc,
                    LyDo = $"Gia hạn ký hợp đồng đến {newDeadlineUtc:O}"
                });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReservationError.None;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return ReservationError.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return ReservationError.Conflict;
        }
    }

    public async Task<ReservationError> CancelExpiredContractAsync(int actorId,
        int reservationId, string reason, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await context.YeuCauGiuChos.SingleOrDefaultAsync(h =>
                h.YeuCauGiuChoId == reservationId, cancellationToken);
            if (hold is null)
                return ReservationError.NotFound;
            if (hold.TrangThai != TrangThaiYeuCauGiuCho.DangGiuCho ||
                hold.HanKyHopDong is null || hold.HanKyHopDong > nowUtc)
                return ReservationError.InvalidState;

            hold.TrangThai = TrangThaiYeuCauGiuCho.DaHuy;
            hold.LyDoTuChoiHoacHuy = reason;
            hold.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauGiuChos.Add(
                new LichSuTrangThaiYeuCauGiuCho
                {
                    YeuCauGiuChoId = reservationId,
                    TrangThaiTruoc = TrangThaiYeuCauGiuCho.DangGiuCho,
                    TrangThaiSau = TrangThaiYeuCauGiuCho.DaHuy,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung,
                    NguoiThucHienId = actorId,
                    NgayThucHien = nowUtc,
                    LyDo = reason
                });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReservationError.None;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return ReservationError.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return ReservationError.Conflict;
        }
    }
}
