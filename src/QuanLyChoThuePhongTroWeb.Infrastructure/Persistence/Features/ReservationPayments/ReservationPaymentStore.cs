using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationPayments;

public sealed class ReservationPaymentStore(ApplicationDbContext context)
    : IReservationPaymentStore
{
    public async Task<int?> AddEvidenceAsync(int actorId, EvidenceRecord submission,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var payment = await context.YeuCauThanhToanGiuChos
            .Include(p => p.YeuCauGiuCho)
            .SingleOrDefaultAsync(p => p.YeuCauThanhToanGiuChoId ==
                submission.PaymentRequestId &&
                p.YeuCauGiuCho.KhachVangLai.NguoiDungId == actorId,
                cancellationToken);
        if (payment is null || payment.HanThanhToan < nowUtc ||
            payment.YeuCauGiuCho.TrangThai is not
                (TrangThaiYeuCauGiuCho.ChoThanhToan or
                 TrangThaiYeuCauGiuCho.ChoXacNhanTien) ||
            payment.TrangThai is not
                (TrangThaiYeuCauThanhToan.ChoThanhToan or
                 TrangThaiYeuCauThanhToan.DaBaoChuyen or
                 TrangThaiYeuCauThanhToan.DangDoiChieu))
            return null;

        var evidence = new MinhChungThanhToanGiuCho
        {
            YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
            UrlHinhAnh = submission.ImageUrl,
            PublicIdHinhAnh = submission.ImagePublicId,
            SoTienKhaiBao = submission.ClaimedAmount,
            NgayChuyenTien = submission.TransferTimeUtc,
            MaGiaoDichNganHang = submission.BankReference?.Trim(),
            GhiChuKhachHang = submission.Note?.Trim(),
            TrangThai = TrangThaiMinhChungThanhToan.ChoXacNhan,
            NgayTao = nowUtc
        };
        context.MinhChungThanhToanGiuChos.Add(evidence);
        if (payment.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan)
        {
            payment.TrangThai = TrangThaiYeuCauThanhToan.DaBaoChuyen;
            payment.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauThanhToanGiuChos.Add(
                new LichSuTrangThaiYeuCauThanhToanGiuCho
                {
                    YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                    TrangThaiTruoc = TrangThaiYeuCauThanhToan.ChoThanhToan,
                    TrangThaiSau = payment.TrangThai,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung,
                    NguoiThucHienId = actorId,
                    NgayThucHien = nowUtc
                });
        }
        if (payment.YeuCauGiuCho.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan)
        {
            payment.YeuCauGiuCho.TrangThai = TrangThaiYeuCauGiuCho.ChoXacNhanTien;
            payment.YeuCauGiuCho.NgayCapNhat = nowUtc;
            context.LichSuTrangThaiYeuCauGiuChos.Add(
                new LichSuTrangThaiYeuCauGiuCho
                {
                    YeuCauGiuChoId = payment.YeuCauGiuChoId,
                    TrangThaiTruoc = TrangThaiYeuCauGiuCho.ChoThanhToan,
                    TrangThaiSau = TrangThaiYeuCauGiuCho.ChoXacNhanTien,
                    LoaiTacNhan = LoaiTacNhan.NguoiDung,
                    NguoiThucHienId = actorId,
                    NgayThucHien = nowUtc
                });
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return evidence.MinhChungThanhToanGiuChoId;
    }

    public async Task<IReadOnlyList<PaymentEvidenceDto>> ListMineAsync(int actorId,
        CancellationToken cancellationToken = default) =>
        await EvidenceQuery().Where(e =>
                e.YeuCauThanhToanGiuCho.YeuCauGiuCho.KhachVangLai.NguoiDungId == actorId)
            .OrderByDescending(e => e.NgayTao)
            .Select(e => new PaymentEvidenceDto(e.MinhChungThanhToanGiuChoId,
                e.YeuCauThanhToanGiuChoId, e.SoTienKhaiBao,
                e.NgayChuyenTien, e.TrangThai.ToString(),
                e.MaGiaoDichNganHang, e.LyDoTuChoi))
            .Take(200).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentEvidenceDto>> ListForReservationAsync(
        int reservationId, CancellationToken cancellationToken = default) =>
        await EvidenceQuery().Where(e =>
                e.YeuCauThanhToanGiuCho.YeuCauGiuChoId == reservationId)
            .OrderByDescending(e => e.NgayTao)
            .Select(e => new PaymentEvidenceDto(e.MinhChungThanhToanGiuChoId,
                e.YeuCauThanhToanGiuChoId, e.SoTienKhaiBao,
                e.NgayChuyenTien, e.TrangThai.ToString(),
                e.MaGiaoDichNganHang, e.LyDoTuChoi))
            .ToListAsync(cancellationToken);

    private IQueryable<MinhChungThanhToanGiuCho> EvidenceQuery() =>
        context.MinhChungThanhToanGiuChos.AsNoTracking();

    public async Task<PaymentEvidenceAccess?> GetEvidenceAccessAsync(int evidenceId,
        CancellationToken cancellationToken = default)
    {
        return await EvidenceQuery()
            .Where(e => e.MinhChungThanhToanGiuChoId == evidenceId)
            .Select(e => new PaymentEvidenceAccess(
                e.MinhChungThanhToanGiuChoId,
                e.YeuCauThanhToanGiuCho.YeuCauGiuCho.KhachVangLai.NguoiDungId,
                e.YeuCauThanhToanGiuCho.YeuCauGiuCho.PhongTro.ChiNhanhId,
                e.UrlHinhAnh, e.PublicIdHinhAnh))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int?> GetPaymentBranchIdAsync(int paymentRequestId,
        CancellationToken cancellationToken = default) =>
        context.YeuCauThanhToanGiuChos.AsNoTracking()
            .Where(p => p.YeuCauThanhToanGiuChoId == paymentRequestId)
            .Select(p => (int?)p.YeuCauGiuCho.PhongTro.ChiNhanhId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PaymentError> ConfirmReceivedAsync(int actorId,
        int paymentRequestId, int? evidenceId, string bankReference,
        decimal actualAmount, DateTime receivedAtUtc, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var payment = await context.YeuCauThanhToanGiuChos
                .Include(p => p.YeuCauGiuCho)
                .SingleOrDefaultAsync(p => p.YeuCauThanhToanGiuChoId == paymentRequestId,
                    cancellationToken);
            if (payment is null)
                return PaymentError.NotFound;
            if (receivedAtUtc < payment.NgayTao ||
                receivedAtUtc > nowUtc.AddMinutes(5))
                return PaymentError.InvalidState;
            if (await context.GiaoDichGiuChos.AsNoTracking()
                    .AnyAsync(g => g.MaGiaoDich == bankReference ||
                        (evidenceId.HasValue &&
                         g.MinhChungThanhToanGiuChoId == evidenceId),
                        cancellationToken))
                return PaymentError.DuplicateTransaction;
            MinhChungThanhToanGiuCho? evidence = null;
            if (evidenceId.HasValue)
            {
                evidence = await context.MinhChungThanhToanGiuChos
                    .SingleOrDefaultAsync(e => e.MinhChungThanhToanGiuChoId == evidenceId &&
                        e.YeuCauThanhToanGiuChoId == paymentRequestId,
                        cancellationToken);
                if (evidence is null || evidence.TrangThai !=
                    TrangThaiMinhChungThanhToan.ChoXacNhan)
                    return PaymentError.InvalidState;
            }
            var previousTotal = await context.GiaoDichGiuChos.AsNoTracking()
                .Where(g => g.YeuCauThanhToanGiuChoId == paymentRequestId)
                .SumAsync(g => (decimal?)g.SoTienThucNhan, cancellationToken) ?? 0m;
            var hold = payment.YeuCauGiuCho;
            var active = hold.TrangThai is TrangThaiYeuCauGiuCho.ChoThanhToan or
                TrangThaiYeuCauGiuCho.ChoXacNhanTien or
                TrangThaiYeuCauGiuCho.DangGiuCho;
            var onTime = receivedAtUtc <= payment.HanThanhToan;
            context.GiaoDichGiuChos.Add(new GiaoDichGiuCho
            {
                YeuCauThanhToanGiuChoId = paymentRequestId,
                MinhChungThanhToanGiuChoId = evidenceId,
                MaGiaoDich = bankReference,
                SoTienThucNhan = actualAmount,
                PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                NgayThucNhan = receivedAtUtc,
                NguoiXacNhanId = actorId,
                NgayXacNhan = nowUtc,
                GhiChu = active && onTime ? null : ReservationPaymentMarkers.LateRefundRequired
            });
            if (evidence is not null)
            {
                evidence.TrangThai = TrangThaiMinhChungThanhToan.DaXacNhan;
                evidence.NguoiDoiChieuId = actorId;
                evidence.NgayDoiChieu = nowUtc;
            }

            if (active && onTime)
            {
                var fullyPaid = previousTotal + actualAmount >= hold.SoTienGiuCho;
                var nextHoldStatus = fullyPaid
                    ? TrangThaiYeuCauGiuCho.DangGiuCho
                    : TrangThaiYeuCauGiuCho.ChoXacNhanTien;
                if (hold.TrangThai != nextHoldStatus)
                {
                    context.LichSuTrangThaiYeuCauGiuChos.Add(
                        new LichSuTrangThaiYeuCauGiuCho
                        {
                            YeuCauGiuChoId = hold.YeuCauGiuChoId,
                            TrangThaiTruoc = hold.TrangThai,
                            TrangThaiSau = nextHoldStatus,
                            LoaiTacNhan = LoaiTacNhan.NguoiDung,
                            NguoiThucHienId = actorId,
                            NgayThucHien = nowUtc
                        });
                    hold.TrangThai = nextHoldStatus;
                    hold.NgayCapNhat = nowUtc;
                }
                var nextPaymentStatus = fullyPaid
                    ? TrangThaiYeuCauThanhToan.DaHoanTat
                    : TrangThaiYeuCauThanhToan.DangDoiChieu;
                if (payment.TrangThai != nextPaymentStatus)
                {
                    context.LichSuTrangThaiYeuCauThanhToanGiuChos.Add(
                        new LichSuTrangThaiYeuCauThanhToanGiuCho
                        {
                            YeuCauThanhToanGiuChoId = paymentRequestId,
                            TrangThaiTruoc = payment.TrangThai,
                            TrangThaiSau = nextPaymentStatus,
                            LoaiTacNhan = LoaiTacNhan.NguoiDung,
                            NguoiThucHienId = actorId,
                            NgayThucHien = nowUtc
                        });
                    payment.TrangThai = nextPaymentStatus;
                    payment.NgayCapNhat = nowUtc;
                }
            }
            else if (!onTime && hold.TrangThai is
                TrangThaiYeuCauGiuCho.ChoThanhToan or
                TrangThaiYeuCauGiuCho.ChoXacNhanTien)
            {
                var previousHoldStatus = hold.TrangThai;
                hold.TrangThai = TrangThaiYeuCauGiuCho.HetHan;
                hold.NgayCapNhat = nowUtc;
                context.LichSuTrangThaiYeuCauGiuChos.Add(
                    new LichSuTrangThaiYeuCauGiuCho
                    {
                        YeuCauGiuChoId = hold.YeuCauGiuChoId,
                        TrangThaiTruoc = previousHoldStatus,
                        TrangThaiSau = TrangThaiYeuCauGiuCho.HetHan,
                        LoaiTacNhan = LoaiTacNhan.HeThong,
                        NgayThucHien = nowUtc,
                        LyDo = "Quá hạn chuyển tiền; khoản đến muộn cần hoàn"
                    });
                if (payment.TrangThai is TrangThaiYeuCauThanhToan.ChoThanhToan or
                    TrangThaiYeuCauThanhToan.DaBaoChuyen or
                    TrangThaiYeuCauThanhToan.DangDoiChieu)
                {
                    var previousPaymentStatus = payment.TrangThai;
                    payment.TrangThai = TrangThaiYeuCauThanhToan.HetHan;
                    payment.NgayCapNhat = nowUtc;
                    context.LichSuTrangThaiYeuCauThanhToanGiuChos.Add(
                        new LichSuTrangThaiYeuCauThanhToanGiuCho
                        {
                            YeuCauThanhToanGiuChoId = paymentRequestId,
                            TrangThaiTruoc = previousPaymentStatus,
                            TrangThaiSau = TrangThaiYeuCauThanhToan.HetHan,
                            LoaiTacNhan = LoaiTacNhan.HeThong,
                            NgayThucHien = nowUtc
                        });
                }
            }
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PaymentError.None;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return PaymentError.DuplicateTransaction;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return PaymentError.DuplicateTransaction;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return PaymentError.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return PaymentError.Conflict;
        }
    }
}
