using System.Net.Mail;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Biên lai web của một dòng sổ (spec thanh toán trực tuyến §3.4) và gửi lại email biên lai (§6.3, §7.2).
// Dòng đã hủy ghi nhận không có biên lai (NotFound khi xem) và không được gửi lại.
// Đợt, lũy kế, còn nợ, tiền thừa lấy từ GetReceiptSnapshotAsync, cùng công thức với PaymentNotificationFormatter.BuildReceiptEmail.
// Gửi lại không log địa chỉ email và không log nội dung exception của lớp gửi thư (có thể chứa địa chỉ).
public sealed class InvoiceReceiptService(
    IInvoicePaymentStore store,
    IEmployeeAccessService access,
    IEmailService email,
    OnlinePaymentOptions onlinePaymentOptions,
    ILogger<InvoiceReceiptService> logger) : IInvoiceReceiptService
{
    private const string NotFoundMessage = "Không tìm thấy biên lai.";
    private const string NoPermissionMessage = "Bạn không có quyền xem biên lai này.";
    private const string NoSendPermissionMessage = "Bạn không có quyền gửi biên lai này.";
    private const string SendFailedMessage = "Không gửi được email biên lai. Vui lòng thử lại sau.";

    public async Task<ServiceResult<InvoiceReceiptDto>> GetForTenantAsync(int lichSuThanhToanId, int nguoiThueId, CancellationToken ct = default)
    {
        if (nguoiThueId <= 0)
        {
            return ServiceResult<InvoiceReceiptDto>.NotFound(NotFoundMessage);
        }

        var target = await store.GetTargetByLedgerEntryAsync(lichSuThanhToanId, ct);
        if (target == null || !target.KhachXemDuoc(nguoiThueId))
        {
            return ServiceResult<InvoiceReceiptDto>.NotFound(NotFoundMessage);
        }

        var receipt = await store.GetReceiptSnapshotAsync(lichSuThanhToanId, ct);
        if (receipt == null || receipt.IsDeleted)
        {
            return ServiceResult<InvoiceReceiptDto>.NotFound(NotFoundMessage);
        }

        return ServiceResult<InvoiceReceiptDto>.Ok(Map(receipt));
    }

    public async Task<ServiceResult<InvoiceReceiptDto>> GetForEmployeeAsync(int actorId, int lichSuThanhToanId, CancellationToken ct = default)
    {
        if (actorId <= 0)
        {
            return ServiceResult<InvoiceReceiptDto>.Forbidden(NoPermissionMessage);
        }

        var receipt = await store.GetReceiptSnapshotAsync(lichSuThanhToanId, ct);
        if (receipt == null || receipt.IsDeleted)
        {
            return ServiceResult<InvoiceReceiptDto>.NotFound(NotFoundMessage);
        }

        if (!await access.CanPerformAsync(actorId, receipt.ChiNhanhId, EmployeeActionCodes.InvoiceRead, ct))
        {
            return ServiceResult<InvoiceReceiptDto>.Forbidden(NoPermissionMessage);
        }

        return ServiceResult<InvoiceReceiptDto>.Ok(Map(receipt));
    }

    public async Task<ServiceResult<InvoiceReceiptResendResult>> ResendForEmployeeAsync(int actorId, int lichSuThanhToanId, CancellationToken ct = default)
    {
        if (actorId <= 0)
        {
            return ServiceResult<InvoiceReceiptResendResult>.Forbidden(NoSendPermissionMessage);
        }

        var receipt = await store.GetReceiptSnapshotAsync(lichSuThanhToanId, ct);
        if (receipt == null)
        {
            return ServiceResult<InvoiceReceiptResendResult>.NotFound(NotFoundMessage);
        }

        // Kiểm quyền trước khi xét dòng đã hủy để không lộ trạng thái dòng của chi nhánh khác.
        if (!await access.CanPerformAsync(actorId, receipt.ChiNhanhId, EmployeeActionCodes.InvoiceSend, ct))
        {
            return ServiceResult<InvoiceReceiptResendResult>.Forbidden(NoSendPermissionMessage);
        }

        if (receipt.IsDeleted)
        {
            return ServiceResult<InvoiceReceiptResendResult>.Fail("Khoản thanh toán đã hủy ghi nhận, không gửi biên lai.");
        }

        var toEmail = receipt.EmailKhachThue?.Trim();
        if (string.IsNullOrEmpty(toEmail) || !MailAddress.TryCreate(toEmail, out _))
        {
            logger.LogWarning("Khách thuê không có email hợp lệ để gửi lại biên lai: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}",
                receipt.HoaDonId, receipt.LichSuThanhToanId);
            return ServiceResult<InvoiceReceiptResendResult>.Fail("Khách thuê chưa có email hợp lệ.");
        }

        var data = PaymentNotificationFormatter.BuildReceiptEmail(
            receipt,
            InvoicePaymentPolicy.AutoPaymentReference(receipt.MaGiaoDich),
            onlinePaymentOptions.BuildUrl($"/KhachThue/HoaDon/BienLai/{receipt.LichSuThanhToanId}"));

        try
        {
            var (isSuccess, errorMessage) = await email.SendPaymentReceiptAsync(toEmail, data);
            if (!isSuccess)
            {
                logger.LogWarning("Không gửi lại được email biên lai: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId}: {ErrorMessage}",
                    receipt.HoaDonId, receipt.LichSuThanhToanId, errorMessage);
                return ServiceResult<InvoiceReceiptResendResult>.Fail(SendFailedMessage);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Không truyền ex vào logger: thông báo lỗi của lớp gửi thư có thể chứa địa chỉ email của khách.
            logger.LogWarning("Không gửi lại được email biên lai: hóa đơn {HoaDonId}, khoản {LichSuThanhToanId} ({ExceptionType})",
                receipt.HoaDonId, receipt.LichSuThanhToanId, ex.GetType().Name);
            return ServiceResult<InvoiceReceiptResendResult>.Fail(SendFailedMessage);
        }

        logger.LogInformation("Actor {ActorId} gửi lại biên lai khoản {LichSuThanhToanId}", actorId, receipt.LichSuThanhToanId);
        return ServiceResult<InvoiceReceiptResendResult>.Ok(
            new InvoiceReceiptResendResult { LichSuThanhToanId = receipt.LichSuThanhToanId, HoaDonId = receipt.HoaDonId },
            "Đã gửi lại biên lai.");
    }

    private static InvoiceReceiptDto Map(PaymentReceiptSnapshot receipt) => new()
    {
        LichSuThanhToanId = receipt.LichSuThanhToanId,
        HoaDonId = receipt.HoaDonId,
        MaHoaDon = receipt.MaHoaDon,
        TenChiNhanh = receipt.TenChiNhanh,
        DiaChiChiNhanh = receipt.DiaChiChiNhanh,
        TenPhong = receipt.TenPhong,
        TenNguoiThanhToan = receipt.TenKhachThue,
        Thang = receipt.Thang,
        Nam = receipt.Nam,
        NgayThanhToanUtc = receipt.NgayThanhToanUtc,
        PhuongThuc = PaymentNotificationFormatter.PaymentMethodLabel(receipt.MaGiaoDich, receipt.PhuongThucThanhToan),
        MaGiaoDich = receipt.MaGiaoDich,
        // SEPAY-ID-{id} là khóa chống trùng, không phải mã ngân hàng; dòng thu thủ công không có mã tham chiếu.
        // Dòng VNPAY-{X} có mã tham chiếu X (vnp_TransactionNo).
        MaThamChieu = InvoicePaymentPolicy.AutoPaymentReference(receipt.MaGiaoDich),
        TongTien = receipt.TongTien,
        SoTienLanNay = receipt.SoTienThanhToan,
        LuyKeDaThanhToan = receipt.LuyKeDaThanhToan,
        ConNo = Math.Max(0m, receipt.TongTien - receipt.LuyKeDaThanhToan),
        TienThua = PaymentNoteTags.Parse(receipt.GhiChu).TienThua ?? 0m,
        DaThuDu = receipt.LuyKeDaThanhToan >= receipt.TongTien,
        DotThu = receipt.DotThu
    };
}
