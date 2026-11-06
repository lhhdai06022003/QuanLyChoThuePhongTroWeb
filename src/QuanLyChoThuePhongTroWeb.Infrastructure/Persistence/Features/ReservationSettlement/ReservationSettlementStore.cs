using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;
using QuanLyChoThuePhongTroWeb.Application.Features.Reservations;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;
using QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.Reservations;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationSettlement;

public sealed class ReservationSettlementStore(ApplicationDbContext context,
    IHopDongCreation contractCreation)
    : IReservationSettlementStore
{
    private const string BookingAccountUnavailable = "Tài khoản đặt chỗ không có hồ sơ khách hợp lệ.";

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

    public async Task<ServiceResult<ReservationContractDraft>> GetContractDraftAsync(int reservationId,
        int authorizedBranchId, CancellationToken cancellationToken = default)
    {
        var hold = await FullQuery().Include(h => h.PhongTro).AsNoTracking()
            .SingleOrDefaultAsync(h => h.YeuCauGiuChoId == reservationId, cancellationToken);
        if (hold is null)
            return ServiceResult<ReservationContractDraft>.NotFound("Không tìm thấy yêu cầu giữ chỗ.");
        if (hold.PhongTro.ChiNhanhId != authorizedBranchId)
            return ServiceResult<ReservationContractDraft>.Forbidden("Bạn không có quyền xử lý yêu cầu này.");

        var deposit = GetConvertibleDeposit(hold);
        if (!deposit.Success)
            return ServiceResult<ReservationContractDraft>.FailFrom(deposit);
        var booker = await BookerQuery(hold).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (booker is null)
            return ServiceResult<ReservationContractDraft>.Fail(BookingAccountUnavailable);

        var guest = hold.KhachVangLai;
        var tenant = booker.NguoiThue;
        return ServiceResult<ReservationContractDraft>.Ok(new(reservationId, hold.PhongTro.SoPhong,
            tenant?.HoVaTen ?? guest.HoTen, tenant?.Email ?? guest.Email ?? string.Empty,
            tenant?.SoDienThoai ?? guest.SoDienThoai, string.IsNullOrWhiteSpace(tenant?.CCCD),
            hold.PhongTro.GiaThue, deposit.Data));
    }

    public async Task<ServiceResult<int>> CreateContractAsync(int actorId, int reservationId,
        int authorizedBranchId, ReservationContractInput input, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var preparingTenant = false;
        var committed = false;
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await LockReservationAsync(reservationId, cancellationToken);
            if (hold is null)
                return ServiceResult<int>.NotFound("Không tìm thấy yêu cầu giữ chỗ.");
            if (hold.PhongTro.ChiNhanhId != authorizedBranchId)
                return ServiceResult<int>.Forbidden("Bạn không có quyền xử lý yêu cầu này.");
            if (hold.TrangThai == TrangThaiYeuCauGiuCho.DaChuyenHopDong &&
                hold.ApDungTienGiuChoVaoTienCoc is { } applied)
            {
                var tenantId = await BookerQuery(hold).Select(u => u.NguoiThueId)
                    .SingleOrDefaultAsync(cancellationToken);
                var belongsToBooker = tenantId.HasValue && await context.HopDongs.AnyAsync(h =>
                    h.HopDongId == applied.HopDongId && h.PhongTroId == hold.PhongTroId &&
                    h.NguoiThueId == tenantId.Value && !h.IsDeleted, cancellationToken);
                return belongsToBooker
                    ? ServiceResult<int>.Ok(applied.HopDongId, "Yêu cầu đã được chuyển sang hợp đồng.")
                    : ServiceResult<int>.Fail("Tiền giữ chỗ đã được áp dụng vào hợp đồng chưa liên kết với tài khoản đặt chỗ.");
            }

            var deposit = GetConvertibleDeposit(hold);
            if (!deposit.Success)
                return ServiceResult<int>.FailFrom(deposit);

            var bookerId = hold.KhachVangLai.NguoiDungId;
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM nguoi_dung WHERE \"NguoiDungId\" = {bookerId} FOR UPDATE", cancellationToken);
            var booker = await BookerQuery(hold).SingleOrDefaultAsync(cancellationToken);
            if (booker is null)
                return ServiceResult<int>.Fail(BookingAccountUnavailable);

            preparingTenant = true;
            var tenant = await PrepareTenantAsync(booker, hold.KhachVangLai, input.CCCD, nowUtc, cancellationToken);
            if (!tenant.Success || tenant.Data is null)
                return ServiceResult<int>.FailFrom(tenant);
            booker.Role = Role.KhachThue;
            await context.SaveChangesAsync(cancellationToken);

            var contract = await contractCreation.CreateInTransactionAsync(new HopDongReq
            {
                PhongTroId = hold.PhongTroId,
                NguoiThueId = tenant.Data.NguoiThueId,
                ThoiDiemBatDau = input.StartDate!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                ThoiDiemKetThuc = input.EndDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                TienCocPhong = deposit.Data,
                TienThuePhong = input.Rent
            }, cancellationToken);
            if (!contract.Success)
                return contract;

            ApplyDeposit(hold, actorId, contract.Data, deposit.Data, nowUtc);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return ServiceResult<int>.Ok(contract.Data, "Đã tạo hợp đồng và áp dụng tiền giữ chỗ vào tiền cọc.");
        }
        catch (PostgresException ex) when (IsWriteConflict(ex))
        {
            return ServiceResult<int>.Fail("Dữ liệu vừa thay đổi, vui lòng tải lại.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && IsWriteConflict(pg))
        {
            return ServiceResult<int>.Fail("Dữ liệu vừa thay đổi, vui lòng tải lại.");
        }
        catch (UniqueConstraintViolationException)
        {
            return ServiceResult<int>.Fail("Dữ liệu vừa thay đổi, vui lòng tải lại.");
        }
        finally
        {
            if (preparingTenant && !committed)
                context.ChangeTracker.Clear();
        }
    }

    public async Task<ServiceResult> CloseWithoutRentAsync(int actorId, int reservationId,
        int authorizedBranchId, string reason, bool requireExpired, bool confirm,
        bool preservePriorRefund, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await LockReservationAsync(reservationId, cancellationToken);
            if (hold is null)
                return ServiceResult.NotFound("Không tìm thấy yêu cầu giữ chỗ.");
            if (hold.PhongTro.ChiNhanhId != authorizedBranchId)
                return ServiceResult.Forbidden("Bạn không có quyền xử lý yêu cầu này.");
            if (requireExpired && (hold.HanKyHopDong is null || hold.HanKyHopDong > nowUtc))
                return ServiceResult.Fail("Chỉ hủy theo hạn ký khi yêu cầu đã quá hạn.");

            // Yêu cầu cũ được nhân viên duyệt có thể hủy quá hạn theo quy trình cũ.
            var legacyCancellation = requireExpired && !ReservationDepositRules.IsSelfService(hold);
            if (!legacyCancellation && !confirm)
                return ServiceResult.Fail("Cần xác nhận cách xử lý tiền cọc trước khi kết thúc giữ chỗ.");
            if (hold.TrangThai == TrangThaiYeuCauGiuCho.KhachKhongThue)
                return ServiceResult.Ok("Đã ghi nhận khách không thuê và giữ lại tiền cọc.");
            if (FindPriorRefundClosureMarker(hold) is not null)
                return ServiceResult.Ok(ClosedWithPriorRefundMessage);
            if (hold.TrangThai != TrangThaiYeuCauGiuCho.DangGiuCho || hold.ApDungTienGiuChoVaoTienCoc is not null)
                return ServiceResult.Fail("Yêu cầu không còn ở trạng thái đang giữ chỗ.");

            var closeWithPriorRefund = false;
            var historyReason = reason;
            if (!legacyCancellation)
            {
                var deposit = ReservationDepositRules.PaidDeposit(hold);
                if (deposit == 0m)
                    return ServiceResult.Fail("Chưa nhận đủ tiền giữ chỗ trong hạn để kết thúc theo chính sách cọc.");
                var summary = Map(hold);
                var separateRefund = summary.ConfirmedAmount - deposit;
                var committedRefund = Math.Max(summary.ApprovedRefundAmount.GetValueOrDefault(), summary.RefundedAmount);
                closeWithPriorRefund = summary.UsesPriorRefundForClosure;
                if (preservePriorRefund != closeWithPriorRefund)
                    return ServiceResult.Fail("Cách xử lý tiền cọc đã thay đổi. Vui lòng tải lại và xác nhận.");
                if (!closeWithPriorRefund && committedRefund > separateRefund)
                    return ServiceResult.Fail("Quyết định hoàn tiền đã dùng một phần tiền cọc giữ chỗ.");

                if (closeWithPriorRefund)
                {
                    // Chỉ lưu phần cam kết lấy từ cọc. Phần dư/đến muộn đã nằm trong tổng giao dịch.
                    historyReason = FormattableString.Invariant(
                        $"{SelfServiceReservationRules.PriorRefundClosurePrefix}{committedRefund - separateRefund}");
                }
                else if (hold.QuyetDinhHoanTienGiuCho is null)
                {
                    context.QuyetDinhHoanTienGiuChos.Add(new QuyetDinhHoanTienGiuCho
                    {
                        YeuCauGiuChoId = reservationId,
                        SoTienHoanDuyet = separateRefund,
                        LyDo = separateRefund > 0m ? "Chỉ hoàn tiền chuyển dư hoặc đến muộn." : "Giữ lại tiền cọc do khách không thuê.",
                        NguoiQuyetDinhId = actorId,
                        NgayQuyetDinh = nowUtc,
                        TrangThai = separateRefund > 0m ? TrangThaiHoanTien.ChoHoanTien : TrangThaiHoanTien.DaHoanTien
                    });
                }
            }

            // Trạng thái kết thúc đánh dấu chính xác SoTienGiuCho là phần đã giữ lại.
            // Không đổi trạng thái phòng: chỉ phòng còn trống và được đăng tin mới hiện lại.
            var nextStatus = legacyCancellation || closeWithPriorRefund
                ? TrangThaiYeuCauGiuCho.DaHuy : TrangThaiYeuCauGiuCho.KhachKhongThue;
            hold.TrangThai = nextStatus;
            hold.LyDoTuChoiHoacHuy = reason;
            hold.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauGiuChos.Add(new LichSuTrangThaiYeuCauGiuCho
            {
                YeuCauGiuChoId = reservationId,
                TrangThaiTruoc = TrangThaiYeuCauGiuCho.DangGiuCho,
                TrangThaiSau = nextStatus,
                LoaiTacNhan = LoaiTacNhan.NguoiDung,
                NguoiThucHienId = actorId,
                NgayThucHien = nowUtc,
                LyDo = historyReason
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (closeWithPriorRefund)
                return ServiceResult.Ok(ClosedWithPriorRefundMessage);
            return ServiceResult.Ok(legacyCancellation
                ? "Đã hủy giữ chỗ; nhân viên cần quyết định hoàn tiền."
                : "Đã ghi nhận khách không thuê và giữ lại tiền cọc.");
        }
        catch (PostgresException ex) when (IsWriteConflict(ex))
        {
            context.ChangeTracker.Clear();
            return ServiceResult.Fail("Dữ liệu vừa thay đổi, vui lòng tải lại.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && IsWriteConflict(pg))
        {
            context.ChangeTracker.Clear();
            return ServiceResult.Fail("Dữ liệu vừa thay đổi, vui lòng tải lại.");
        }
    }

    private const string ClosedWithPriorRefundMessage = "Đã hủy giữ chỗ theo quyết định hoàn tiền trước đó.";

    private static string? FindPriorRefundClosureMarker(YeuCauGiuCho hold) =>
        hold.TrangThai == TrangThaiYeuCauGiuCho.DaHuy && ReservationDepositRules.IsSelfService(hold)
            ? hold.LichSuTrangThais.Where(h => h.TrangThaiSau == TrangThaiYeuCauGiuCho.DaHuy)
                .Select(h => h.LyDo).FirstOrDefault(reason => reason == SelfServiceReservationRules.PriorRefundClosureMarker ||
                    reason?.StartsWith(SelfServiceReservationRules.PriorRefundClosurePrefix, StringComparison.Ordinal) == true)
            : null;

    // Settlement mutations lock room first, then reservation, in a serializable transaction.
    private async Task<YeuCauGiuCho?> LockReservationAsync(int reservationId,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM phong_tro WHERE "PhongTroId" =
                (SELECT "PhongTroId" FROM yeu_cau_giu_cho WHERE "YeuCauGiuChoId" = {reservationId})
            FOR UPDATE
            """, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM yeu_cau_giu_cho WHERE \"YeuCauGiuChoId\" = {reservationId} FOR UPDATE", cancellationToken);
        return await FullQuery().Include(h => h.PhongTro)
            .SingleOrDefaultAsync(h => h.YeuCauGiuChoId == reservationId, cancellationToken);
    }

    private static bool IsWriteConflict(PostgresException exception) => exception.SqlState is
        PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DeadlockDetected;

    private IQueryable<NguoiDung> BookerQuery(YeuCauGiuCho hold) =>
        context.NguoiDungs.Include(u => u.NguoiThue).Where(u =>
            u.NguoiDungId == hold.KhachVangLai.NguoiDungId && u.IsActive && !u.IsDeleted &&
            (u.Role == Role.KhachVangLai || u.Role == Role.KhachThue && u.NguoiThueId != null) &&
            (u.NguoiThueId == null || u.NguoiThue != null && !u.NguoiThue.IsDeleted));

    private static ServiceResult<decimal> GetConvertibleDeposit(YeuCauGiuCho hold)
    {
        if (hold.TrangThai != TrangThaiYeuCauGiuCho.DangGiuCho || hold.ApDungTienGiuChoVaoTienCoc is not null)
            return ServiceResult<decimal>.Fail("Yêu cầu không còn ở trạng thái chờ xác nhận thuê.");
        if (hold.PhongTro.IsDeleted || hold.PhongTro.TrangThai != TrangThaiPhong.Trong)
            return ServiceResult<decimal>.Fail("Phòng không còn trống để lập hợp đồng.");

        var deposit = ReservationDepositRules.PaidDeposit(hold);
        if (deposit == 0m)
            return ServiceResult<decimal>.Fail("Chưa nhận đủ tiền giữ chỗ trong hạn để lập hợp đồng.");

        var summary = Map(hold);
        var committedRefund = Math.Max(summary.ApprovedRefundAmount.GetValueOrDefault(), summary.RefundedAmount);
        if (committedRefund > summary.ConfirmedAmount - deposit)
            return ServiceResult<decimal>.Fail("Quyết định hoàn tiền đã dùng một phần tiền cọc giữ chỗ.");

        return ServiceResult<decimal>.Ok(deposit);
    }

    private async Task<ServiceResult<NguoiThue>> PrepareTenantAsync(NguoiDung booker, KhachVangLai guest,
        string? enteredCccd, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var tenant = booker.NguoiThue;
        if (!string.IsNullOrWhiteSpace(tenant?.CCCD))
            return ServiceResult<NguoiThue>.Ok(tenant);

        var cccd = enteredCccd?.Trim();
        if (cccd is null || cccd.Length is < 9 or > 12 || !cccd.All(char.IsAsciiDigit))
            return ServiceResult<NguoiThue>.Fail("Vui lòng nhập CCCD hợp lệ từ 9 đến 12 số.");

        if (tenant is not null)
        {
            if (await context.NguoiThues.AnyAsync(t => !t.IsDeleted && t.NguoiThueId != tenant.NguoiThueId &&
                    t.CCCD.Trim() == cccd, cancellationToken))
                return ServiceResult<NguoiThue>.Fail("CCCD đã có trong hồ sơ người thuê khác.");
            tenant.CCCD = cccd;
            tenant.NgayCapNhat = nowUtc;
            return ServiceResult<NguoiThue>.Ok(tenant);
        }

        var email = guest.Email?.Trim();
        var name = guest.HoTen.Trim();
        var phone = guest.SoDienThoai.Trim();
        if (string.IsNullOrWhiteSpace(email) || name.Length == 0 || phone.Length == 0)
            return ServiceResult<NguoiThue>.Fail(BookingAccountUnavailable);

        var normalizedEmail = email.ToUpperInvariant();
        var matches = await context.NguoiThues.Where(t => !t.IsDeleted &&
            (t.CCCD.Trim() == cccd || t.Email.Trim().ToUpper() == normalizedEmail || t.SoDienThoai.Trim() == phone))
            .Take(2).ToListAsync(cancellationToken);
        if (matches.Count != 0)
        {
            tenant = matches[0];
            if (matches.Count != 1 || tenant.CCCD.Trim() != cccd || !SameGuest(guest, tenant) ||
                !string.Equals(tenant.HoVaTen.Trim(), name, StringComparison.OrdinalIgnoreCase) ||
                await context.NguoiDungs.AnyAsync(u => u.NguoiThueId == tenant.NguoiThueId, cancellationToken))
                return ServiceResult<NguoiThue>.Fail("CCCD hoặc thông tin liên hệ trùng với hồ sơ người thuê khác.");
        }
        else
        {
            tenant = new NguoiThue
            {
                CCCD = cccd, HoVaTen = name, Email = email, SoDienThoai = phone, NgayTao = nowUtc
            };
            context.NguoiThues.Add(tenant);
        }

        booker.NguoiThue = tenant;
        return ServiceResult<NguoiThue>.Ok(tenant);
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
            var hold = await LockReservationAsync(reservationId, cancellationToken);
            if (hold is null)
                return SettlementError.NotFound;
            if (ReservationDepositRules.IsSelfService(hold))
                return SettlementError.ContractConfirmationRequired;
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

            ApplyDeposit(hold, actorId, contractId, amount, nowUtc);
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

    private void ApplyDeposit(YeuCauGiuCho hold, int actorId, int contractId,
        decimal amount, DateTime nowUtc)
    {
        context.ApDungTienGiuChoVaoTienCocs.Add(new ApDungTienGiuChoVaoTienCoc
        {
            YeuCauGiuChoId = hold.YeuCauGiuChoId,
            HopDongId = contractId,
            SoTienApDung = amount,
            NgayApDung = nowUtc,
            NguoiThucHienId = actorId
        });
        hold.TrangThai = TrangThaiYeuCauGiuCho.DaChuyenHopDong;
        hold.NgayCapNhat = nowUtc;
        context.LichSuTrangThaiYeuCauGiuChos.Add(new LichSuTrangThaiYeuCauGiuCho
        {
            YeuCauGiuChoId = hold.YeuCauGiuChoId,
            TrangThaiTruoc = TrangThaiYeuCauGiuCho.DangGiuCho,
            TrangThaiSau = hold.TrangThai,
            LoaiTacNhan = LoaiTacNhan.NguoiDung,
            NguoiThucHienId = actorId,
            NgayThucHien = nowUtc,
            LyDo = "Áp dụng tiền giữ chỗ vào cọc hợp đồng"
        });
    }

    public async Task<SettlementError> DecideRefundAsync(int actorId,
        int reservationId, decimal amount, string reason, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var hold = await LockReservationAsync(reservationId, cancellationToken);
            if (hold is null)
                return SettlementError.NotFound;
            var summary = Map(hold);
            if (amount < summary.MandatoryRefundAmount)
                return SettlementError.BelowMandatoryRefund;
            if (amount > summary.AvailableRefundAmount ||
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
            var hold = await LockReservationAsync(reservationId, cancellationToken);
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
                amount > summary.AvailableRefundAmount - summary.RefundedAmount)
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
            .Include(h => h.LichSuTrangThais)
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
        var agreedDeposit = hold.SoTienGiuCho.GetValueOrDefault();
        var committedRefund = Math.Max(decision?.SoTienHoanDuyet ?? 0m, refunded);
        var paid = hold.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho ? ReservationDepositRules.PaidDeposit(hold) : 0m;
        var retained = hold.TrangThai == TrangThaiYeuCauGiuCho.KhachKhongThue ? agreedDeposit : 0m;
        var isSelfService = ReservationDepositRules.IsSelfService(hold);
        var refundTouchesDeposit = paid > 0m && committedRefund > received - paid;
        var priorRefundClosureMarker = FindPriorRefundClosureMarker(hold);
        var closedWithPriorRefund = priorRefundClosureMarker is not null;
        var reserved = retained > 0m ? retained : isSelfService ? paid : 0m;
        if (priorRefundClosureMarker is not null)
        {
            var prefix = SelfServiceReservationRules.PriorRefundClosurePrefix;
            if (!priorRefundClosureMarker.StartsWith(prefix, StringComparison.Ordinal) ||
                !decimal.TryParse(priorRefundClosureMarker.AsSpan(prefix.Length), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var committedFromDeposit))
            {
                // Dấu cũ không lưu số tiền, chỉ bảo toàn tổng đã cam kết và phần hoàn riêng xác định được.
                var separate = Math.Max(0m, received - applied - agreedDeposit);
                committedFromDeposit = Math.Max(0m, committedRefund - separate);
            }
            reserved = Math.Max(0m, agreedDeposit - committedFromDeposit);
        }
        var available = Math.Max(0m, received - applied - reserved);
        var mandatory = reserved > 0m || closedWithPriorRefund
            ? available : SettlementMath.MandatoryRefund(received, agreedDeposit, applied, late);
        return new SettlementSummaryDto(hold.YeuCauGiuChoId, received,
            applied, hold.ApDungTienGiuChoVaoTienCoc?.HopDongId,
            decision?.QuyetDinhHoanTienGiuChoId,
            decision?.SoTienHoanDuyet, refunded,
            mandatory, decision?.TrangThai.ToString(), retained, available,
            paid > 0m && hold.ApDungTienGiuChoVaoTienCoc is null &&
                (!refundTouchesDeposit || isSelfService),
            retained > 0m || closedWithPriorRefund ? hold.LyDoTuChoiHoacHuy : null,
            closedWithPriorRefund || isSelfService && refundTouchesDeposit);
    }

    private static bool SameGuest(KhachVangLai guest, NguoiThue tenant) =>
        !string.IsNullOrWhiteSpace(guest.Email) &&
        string.Equals(guest.Email.Trim(), tenant.Email?.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        Digits(guest.SoDienThoai) == Digits(tenant.SoDienThoai);

    private static string Digits(string? text) =>
        new((text ?? string.Empty).Where(char.IsDigit).ToArray());
}
