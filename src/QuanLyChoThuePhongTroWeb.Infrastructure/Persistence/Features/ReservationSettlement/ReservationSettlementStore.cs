using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationSettlement;

public sealed class ReservationSettlementStore(ApplicationDbContext context)
    : IReservationSettlementStore
{
    public Task<int?> GetBranchIdAsync(int reservationId,
        CancellationToken cancellationToken = default) =>
        context.YeuCauGiuChos.AsNoTracking()
            .Where(h => h.YeuCauGiuChoId == reservationId)
            .Select(h => (int?)h.PhongTro.ChiNhanhId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SettlementSummaryDto?> GetAsync(int reservationId,
        CancellationToken cancellationToken = default)
    {
        var hold = await FullQuery().AsNoTracking()
            .SingleOrDefaultAsync(h => h.YeuCauGiuChoId == reservationId,
                cancellationToken);
        return hold is null ? null : Map(hold);
    }

    public async Task<IReadOnlyList<SettlementSummaryDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default)
    {
        var holds = await FullQuery().AsNoTracking()
            .Where(h => h.KhachVangLai.NguoiDungId == actorId)
            .OrderByDescending(h => h.NgayTao).Take(200)
            .ToListAsync(cancellationToken);
        return holds.Select(Map).ToArray();
    }

    public async Task<SettlementError> ApplyAsync(int actorId, int reservationId,
        int contractId, decimal amount, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await FullQuery().SingleOrDefaultAsync(h =>
                h.YeuCauGiuChoId == reservationId, cancellationToken);
            if (hold is null)
                return SettlementError.NotFound;
            if (hold.TrangThai != TrangThaiYeuCauGiuCho.DangGiuCho ||
                hold.ApDungTienGiuChoVaoTienCoc is not null)
                return SettlementError.InvalidState;

            var contract = await context.HopDongs.Include(h => h.NguoiThue)
                .SingleOrDefaultAsync(h => h.HopDongId == contractId &&
                    !h.IsDeleted && h.TrangThaiHopDong != TrangThaiHopDong.DaHuy,
                    cancellationToken);
            if (contract is null || contract.PhongTroId != hold.PhongTroId ||
                !SameGuest(hold.KhachVangLai, contract.NguoiThue))
                return SettlementError.ContractMismatch;

            var summary = Map(hold);
            var alreadyAppliedToContract = await context.ApDungTienGiuChoVaoTienCocs
                .AsNoTracking().Where(a => a.HopDongId == contractId)
                .SumAsync(a => (decimal?)a.SoTienApDung, cancellationToken) ?? 0m;
            var depositRemaining = contract.TienCocPhong - alreadyAppliedToContract;
            if (amount > depositRemaining ||
                amount > summary.ConfirmedAmount -
                    (summary.ApprovedRefundAmount ?? 0m) ||
                hold.QuyetDinhHoanTienGiuCho is not null &&
                hold.QuyetDinhHoanTienGiuCho.SoTienHoanDuyet <
                    summary.ConfirmedAmount - amount)
                return SettlementError.AmountExceedsAvailable;

            context.ApDungTienGiuChoVaoTienCocs.Add(new ApDungTienGiuChoVaoTienCoc
            {
                YeuCauGiuChoId = reservationId,
                HopDongId = contractId,
                SoTienApDung = amount,
                NgayApDung = nowUtc,
                NguoiThucHienId = actorId
            });
            hold.TrangThai = TrangThaiYeuCauGiuCho.DaChuyenHopDong;
            hold.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauGiuChos.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                YeuCauGiuChoId = reservationId,
                TrangThaiTruoc = TrangThaiYeuCauGiuCho.DangGiuCho,
                TrangThaiSau = hold.TrangThai,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = nowUtc,
                LyDo = "Áp dụng tiền giữ chỗ vào cọc hợp đồng"
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SettlementError.None;
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return SettlementError.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return SettlementError.Conflict;
        }
    }

    public async Task<SettlementError> DecideRefundAsync(int actorId,
        int reservationId, decimal amount, string reason, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await FullQuery().SingleOrDefaultAsync(h =>
                h.YeuCauGiuChoId == reservationId, cancellationToken);
            if (hold is null)
                return SettlementError.NotFound;
            var summary = Map(hold);
            if (amount < summary.MandatoryRefundAmount)
                return SettlementError.BelowMandatoryRefund;
            if (amount > summary.ConfirmedAmount - summary.AppliedToContractDeposit ||
                amount < summary.RefundedAmount)
                return SettlementError.AmountExceedsAvailable;

            var decision = hold.QuyetDinhHoanTienGiuCho;
            if (decision is null)
            {
                decision = new QuyetDinhHoanTienGiuCho
                {
                    YeuCauGiuChoId = reservationId,
                    SoTienHoanDuyet = amount,
                    LyDo = reason,
                    NguoiQuyetDinhId = actorId,
                    NgayQuyetDinh = nowUtc,
                    TrangThai = amount == 0
                        ? TrangThaiHoanTien.DaHoanTien
                        : TrangThaiHoanTien.ChoHoanTien
                };
                context.QuyetDinhHoanTienGiuChos.Add(decision);
            }
            else
            {
                decision.SoTienHoanDuyet = amount;
                decision.LyDo = reason;
                decision.NguoiQuyetDinhId = actorId;
                decision.NgayQuyetDinh = nowUtc;
                decision.TrangThai = amount == summary.RefundedAmount
                    ? TrangThaiHoanTien.DaHoanTien
                    : summary.RefundedAmount > 0
                        ? TrangThaiHoanTien.DangHoanTien
                        : TrangThaiHoanTien.ChoHoanTien;
            }
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SettlementError.None;
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return SettlementError.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        {
            return SettlementError.Conflict;
        }
    }

    public async Task<SettlementError> RecordRefundAsync(int actorId,
        int reservationId, string bankReference, decimal amount, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await FullQuery().SingleOrDefaultAsync(h =>
                h.YeuCauGiuChoId == reservationId, cancellationToken);
            if (hold is null)
                return SettlementError.NotFound;
            var decision = hold.QuyetDinhHoanTienGiuCho;
            if (decision is null)
                return SettlementError.InvalidState;
            if (await context.GiaoDichHoanTienGiuChos.AsNoTracking()
                    .AnyAsync(g => g.MaGiaoDichHoan == bankReference,
                        cancellationToken))
                return SettlementError.DuplicateTransaction;

            var summary = Map(hold);
            if (amount > decision.SoTienHoanDuyet - summary.RefundedAmount ||
                amount > summary.ConfirmedAmount - summary.AppliedToContractDeposit -
                    summary.RefundedAmount)
                return SettlementError.AmountExceedsAvailable;
            context.GiaoDichHoanTienGiuChos.Add(new GiaoDichHoanTienGiuCho
            {
                QuyetDinhHoanTienGiuChoId = decision.QuyetDinhHoanTienGiuChoId,
                MaGiaoDichHoan = bankReference,
                SoTienHoan = amount,
                NguoiXacNhanId = actorId,
                NgayHoan = nowUtc
            });
            decision.TrangThai = summary.RefundedAmount + amount >=
                decision.SoTienHoanDuyet
                ? TrangThaiHoanTien.DaHoanTien
                : TrangThaiHoanTien.DangHoanTien;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SettlementError.None;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return SettlementError.DuplicateTransaction;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return SettlementError.DuplicateTransaction;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return SettlementError.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return SettlementError.Conflict;
        }
    }

    private IQueryable<YeuCauGiuCho> FullQuery() =>
        context.YeuCauGiuChos
            .Include(h => h.KhachVangLai)
            .Include(h => h.YeuCauThanhToanGiuChos)
                .ThenInclude(p => p.GiaoDichGiuChos)
            .Include(h => h.ApDungTienGiuChoVaoTienCoc)
            .Include(h => h.QuyetDinhHoanTienGiuCho)
                .ThenInclude(q => q!.GiaoDichHoanTiens)
            .AsSplitQuery();

    private static SettlementSummaryDto Map(YeuCauGiuCho hold)
    {
        var transactions = hold.YeuCauThanhToanGiuChos
            .SelectMany(p => p.GiaoDichGiuChos).ToArray();
        var received = transactions.Sum(g => g.SoTienThucNhan);
        var late = transactions.Where(g =>
                g.GhiChu == ReservationPaymentMarkers.LateRefundRequired)
            .Sum(g => g.SoTienThucNhan);
        var applied = hold.ApDungTienGiuChoVaoTienCoc?.SoTienApDung ?? 0m;
        var decision = hold.QuyetDinhHoanTienGiuCho;
        var refunded = decision?.GiaoDichHoanTiens.Sum(g => g.SoTienHoan) ?? 0m;
        return new SettlementSummaryDto(hold.YeuCauGiuChoId, received,
            applied, hold.ApDungTienGiuChoVaoTienCoc?.HopDongId,
            decision?.QuyetDinhHoanTienGiuChoId,
            decision?.SoTienHoanDuyet, refunded,
            SettlementMath.MandatoryRefund(received, hold.SoTienGiuCho ?? 0m,
                applied, late), decision?.TrangThai.ToString());
    }

    private static bool SameGuest(KhachVangLai guest, NguoiThue tenant) =>
        !string.IsNullOrWhiteSpace(guest.Email) &&
        string.Equals(guest.Email.Trim(), tenant.Email?.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        Digits(guest.SoDienThoai) == Digits(tenant.SoDienThoai);

    private static string Digits(string? text) =>
        new((text ?? string.Empty).Where(char.IsDigit).ToArray());
}
