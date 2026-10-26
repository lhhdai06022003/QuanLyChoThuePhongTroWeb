using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.ReservationPayments;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.ReservationPayments;

public sealed class ReservationPaymentStore(ApplicationDbContext context)
    : IReservationPaymentStore, IReservationAutoPaymentStore
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
        // Khóa SEPAY-/VNPAY- do hệ thống ghi tự động; mã nhân viên nhập tay trùng khóa đó là cùng một giao dịch.
        var sePayKey = InvoicePaymentPolicy.SePayLedgerKey(bankReference);
        var vnPayKey = InvoicePaymentPolicy.VnPayLedgerKey(bankReference);
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
            // Ô nhập chỉ chọn đến phút: so với phút duyệt để giao dịch trong cùng phút duyệt vẫn hợp lệ.
            var requestedMinute = payment.NgayTao.AddTicks(
                -(payment.NgayTao.Ticks % TimeSpan.TicksPerMinute));
            if (receivedAtUtc < requestedMinute)
                return PaymentError.ReceivedBeforeRequest;
            if (receivedAtUtc > nowUtc.AddMinutes(5))
                return PaymentError.InvalidState;
            if (await context.GiaoDichGiuChos.AsNoTracking()
                    .AnyAsync(g => g.MaGiaoDich == sePayKey || g.MaGiaoDich == vnPayKey,
                        cancellationToken))
                return PaymentError.SystemRecorded;
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
            await AddReceiptAsync(payment, evidenceId, bankReference, actualAmount,
                receivedAtUtc, actorId, lyDoTuDong: null, nowUtc, cancellationToken);
            if (evidence is not null)
            {
                evidence.TrangThai = TrangThaiMinhChungThanhToan.DaXacNhan;
                evidence.NguoiDoiChieuId = actorId;
                evidence.NgayDoiChieu = nowUtc;
            }
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PaymentError.None;
        }
        // Cùng cách bắt lỗi với RecordSystemTransferAsync: EF có thể bọc lỗi Postgres nhiều lớp.
        catch (Exception ex) when (HasPostgresError(ex, PostgresErrorCodes.UniqueViolation))
        {
            context.ChangeTracker.Clear();
            return PaymentError.DuplicateTransaction;
        }
        catch (Exception ex) when (HasPostgresError(ex, PostgresErrorCodes.SerializationFailure))
        {
            context.ChangeTracker.Clear();
            return PaymentError.Conflict;
        }
    }

    public Task<bool> PaymentRequestExistsAsync(string maYeuCau,
        CancellationToken cancellationToken = default) =>
        context.YeuCauThanhToanGiuChos.AsNoTracking()
            .AnyAsync(p => p.MaYeuCau == maYeuCau, cancellationToken);

    public async Task<SystemReservationRecordResult> RecordSystemTransferAsync(
        string maYeuCau, string maGiaoDich, decimal soTienThucNhan,
        DateTime ngayThucNhanUtc, string lyDoLichSu, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var payment = await context.YeuCauThanhToanGiuChos
                .Include(p => p.YeuCauGiuCho).ThenInclude(h => h.PhongTro).ThenInclude(r => r.ChiNhanh)
                .Include(p => p.YeuCauGiuCho).ThenInclude(h => h.KhachVangLai)
                .SingleOrDefaultAsync(p => p.MaYeuCau == maYeuCau, cancellationToken);
            if (payment is null)
                return new SystemReservationRecordResult(SystemReservationRecordStatus.NotFound);
            // Khóa chống trùng là duy nhất toàn bảng (kể cả giao dịch nhân viên nhập tay).
            if (await context.GiaoDichGiuChos.AsNoTracking()
                    .AnyAsync(g => g.MaGiaoDich == maGiaoDich, cancellationToken))
                return new SystemReservationRecordResult(SystemReservationRecordStatus.Duplicate);
            // Nhân viên đã xác nhận tay đúng giao dịch này bằng mã gốc (SePay gửi lại webhook, VNPay gọi lại IPN):
            // cùng yêu cầu thanh toán có dòng nhập tay mà MaGiaoDich bỏ khoảng trắng, viết hoa bằng mã gốc thì coi là trùng,
            // để không cộng đôi số đã nhận. Khóa SEPAY-ID-{id} không có mã gốc nên bỏ qua bước này.
            var rawReference = InvoicePaymentPolicy.AutoPaymentReference(maGiaoDich);
            if (rawReference is not null && await context.GiaoDichGiuChos.AsNoTracking()
                    .AnyAsync(g => g.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId &&
                        g.NguoiXacNhanId != null &&
                        g.MaGiaoDich.Replace(" ", "").ToUpper() == rawReference, cancellationToken))
                return new SystemReservationRecordResult(SystemReservationRecordStatus.Duplicate);
            var evidencePending = await context.MinhChungThanhToanGiuChos.AsNoTracking()
                .AnyAsync(e => e.YeuCauThanhToanGiuChoId == payment.YeuCauThanhToanGiuChoId &&
                    e.TrangThai == TrangThaiMinhChungThanhToan.ChoXacNhan, cancellationToken);
            // Hệ thống tự ghi: không kiểm "nhận trước phút duyệt" và giờ ở tương lai, vì giờ giao dịch do kênh báo.
            var receipt = await AddReceiptAsync(payment, evidenceId: null, maGiaoDich, soTienThucNhan,
                ngayThucNhanUtc, actorId: null, lyDoLichSu, nowUtc, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var hold = payment.YeuCauGiuCho;
            return new SystemReservationRecordResult(SystemReservationRecordStatus.Recorded)
            {
                YeuCauGiuChoId = hold.YeuCauGiuChoId,
                YeuCauThanhToanGiuChoId = payment.YeuCauThanhToanGiuChoId,
                GiaoDichGiuChoId = receipt.Row.GiaoDichGiuChoId,
                MaYeuCau = payment.MaYeuCau,
                PhongTroId = hold.PhongTroId,
                ChiNhanhId = hold.PhongTro.ChiNhanhId,
                TenChiNhanh = hold.PhongTro.ChiNhanh.TenChiNhanh,
                SoPhong = hold.PhongTro.SoPhong,
                GuestUserId = hold.KhachVangLai.NguoiDungId,
                TenKhach = hold.KhachVangLai.HoTen,
                EmailKhach = hold.KhachVangLai.Email,
                SoTienGiuCho = hold.SoTienGiuCho ?? payment.SoTien,
                DaNhanTruoc = receipt.PreviousTotal,
                DaNhanSau = receipt.PreviousTotal + soTienThucNhan,
                CanHoanTien = receipt.LateRefund,
                VuaCoHieuLuc = receipt.BecameHolding,
                TrangThaiGiuCho = hold.TrangThai.ToString(),
                HanThanhToanUtc = payment.HanThanhToan,
                HanKyHopDongUtc = hold.HanKyHopDong,
                ConMinhChungChoXacNhan = evidencePending
            };
        }
        // EF có thể bọc lỗi Postgres trong DbUpdateException rồi InvalidOperationException ("transient failure"), nên duyệt chuỗi InnerException.
        // Bỏ dòng vừa Add khỏi change tracker để scope này không lưu lại nó (như InvoicePaymentStore.LockHoaDonAsync).
        catch (Exception ex) when (HasPostgresError(ex, PostgresErrorCodes.UniqueViolation))
        {
            context.ChangeTracker.Clear();
            return new SystemReservationRecordResult(SystemReservationRecordStatus.Duplicate);
        }
        catch (Exception ex) when (HasPostgresError(ex, PostgresErrorCodes.SerializationFailure))
        {
            context.ChangeTracker.Clear();
            return new SystemReservationRecordResult(SystemReservationRecordStatus.Conflict);
        }
    }

    private static bool HasPostgresError(Exception exception, string sqlState)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is PostgresException pg && pg.SqlState == sqlState)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ReceiptOutcome(GiaoDichGiuCho Row, decimal PreviousTotal, bool LateRefund, bool BecameHolding);

    // Phần dùng chung của luồng tay (actorId có giá trị) và luồng tự động (actorId null): cộng dồn, thêm dòng giao dịch,
    // chuyển trạng thái giữ chỗ và yêu cầu thanh toán. Không gọi SaveChanges; người gọi lưu và commit.
    // payment.YeuCauGiuCho phải đã được tải.
    private async Task<ReceiptOutcome> AddReceiptAsync(YeuCauThanhToanGiuCho payment, int? evidenceId,
        string maGiaoDich, decimal amount, DateTime receivedAtUtc, int? actorId, string? lyDoTuDong,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var paymentRequestId = payment.YeuCauThanhToanGiuChoId;
        var previousTotal = await context.GiaoDichGiuChos.AsNoTracking()
            .Where(g => g.YeuCauThanhToanGiuChoId == paymentRequestId)
            .SumAsync(g => (decimal?)g.SoTienThucNhan, cancellationToken) ?? 0m;
        var hold = payment.YeuCauGiuCho;
        var active = hold.TrangThai is TrangThaiYeuCauGiuCho.ChoThanhToan or
            TrangThaiYeuCauGiuCho.ChoXacNhanTien or
            TrangThaiYeuCauGiuCho.DangGiuCho;
        var onTime = receivedAtUtc <= payment.HanThanhToan;
        var row = new GiaoDichGiuCho
        {
            YeuCauThanhToanGiuChoId = paymentRequestId,
            MinhChungThanhToanGiuChoId = evidenceId,
            MaGiaoDich = maGiaoDich,
            SoTienThucNhan = amount,
            PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
            NgayThucNhan = receivedAtUtc,
            NguoiXacNhanId = actorId,
            NgayXacNhan = nowUtc,
            GhiChu = active && onTime ? null : ReservationPaymentMarkers.LateRefundRequired
        };
        context.GiaoDichGiuChos.Add(row);

        // Luồng tay ghi người dùng và không có lý do; luồng tự động ghi hệ thống kèm lý do.
        var actorType = actorId.HasValue ? LoaiTacNhan.NguoiDung : LoaiTacNhan.HeThong;
        var historyReason = actorId.HasValue ? null : lyDoTuDong;
        var becameHolding = false;
        if (active && onTime)
        {
            var fullyPaid = previousTotal + amount >= hold.SoTienGiuCho;
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
                        LoaiTacNhan = actorType,
                        NguoiThucHienId = actorId,
                        NgayThucHien = nowUtc,
                        LyDo = historyReason
                    });
                hold.TrangThai = nextHoldStatus;
                hold.NgayCapNhat = nowUtc;
                becameHolding = nextHoldStatus == TrangThaiYeuCauGiuCho.DangGiuCho;
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
                        LoaiTacNhan = actorType,
                        NguoiThucHienId = actorId,
                        NgayThucHien = nowUtc,
                        LyDo = historyReason
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

        return new ReceiptOutcome(row, previousTotal, !(active && onTime), becameHolding);
    }
}
