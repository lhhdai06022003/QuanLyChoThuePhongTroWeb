using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Panel thanh toán của khách thuê (spec thanh toán trực tuyến §3.2): QR, deeplink và số tiền theo hóa đơn, luôn bằng số còn lại.
// Chỉ đọc: không mở transaction, không khóa dòng, không ghi gì. Khách không tự chọn số tiền; webhook ghi đúng số thực nhận.
public sealed class TenantPaymentPanelService(
    IInvoicePaymentStore store,
    IVietQRService vietQr,
    VietQrSettings vietQrSettings) : ITenantPaymentPanelService
{
    private const string NotFoundMessage = "Không tìm thấy hóa đơn.";

    public async Task<ServiceResult<TenantPaymentPanelDto>> GetPanelAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default)
    {
        var invoice = await LoadOwnedInvoiceAsync(hoaDonId, nguoiThueId, ct);
        if (invoice == null)
        {
            return ServiceResult<TenantPaymentPanelDto>.NotFound(NotFoundMessage);
        }

        var conLai = invoice.TongTien - invoice.DaThu;
        var lyDo = CannotPayReason(invoice, conLai);
        var coTheThanhToan = lyDo == null;
        var maChuyenKhoan = TransferCode(hoaDonId);

        var rows = await store.GetActiveLedgerRowsAsync(hoaDonId, ct);
        var cacLanThanhToan = rows
            .Select((row, index) => new TenantPaymentEntryDto(
                row.LichSuThanhToanId,
                index + 1,
                row.SoTienThanhToan,
                row.NgayThanhToanUtc,
                PaymentNotificationFormatter.PaymentMethodLabel(row.MaGiaoDich, row.PhuongThucThanhToan)))
            .ToList();

        return ServiceResult<TenantPaymentPanelDto>.Ok(new TenantPaymentPanelDto
        {
            HoaDonId = invoice.HoaDonId,
            MaHoaDon = invoice.MaHoaDon,
            TenPhong = invoice.TenPhong,
            TenChiNhanh = invoice.TenChiNhanh,
            Thang = invoice.Thang,
            Nam = invoice.Nam,
            TongTien = invoice.TongTien,
            DaThu = invoice.DaThu,
            ConLai = conLai,
            HanThanhToanUtc = invoice.HanThanhToan,
            CoTheThanhToan = coTheThanhToan,
            LyDoKhongTheThanhToan = lyDo,
            MaChuyenKhoan = maChuyenKhoan,
            TenNganHang = string.IsNullOrWhiteSpace(vietQrSettings.BankName) ? vietQrSettings.BankId : vietQrSettings.BankName,
            SoTaiKhoan = vietQrSettings.AccountNumber,
            ChuTaiKhoan = vietQrSettings.AccountName,
            Deeplinks = coTheThanhToan
                ? VietQrDeeplinks.BuildAll(vietQrSettings, conLai, maChuyenKhoan)
                : Array.Empty<BankDeeplinkDto>(),
            CacLanThanhToan = cacLanThanhToan,
            MinhChungChoDoiChieu = await FindLegacyProofAsync(hoaDonId, ct)
        });
    }

    public async Task<ServiceResult<InvoiceQrImage>> GetQrAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default)
    {
        var invoice = await LoadOwnedInvoiceAsync(hoaDonId, nguoiThueId, ct);
        if (invoice == null)
        {
            return ServiceResult<InvoiceQrImage>.NotFound(NotFoundMessage);
        }

        var conLai = invoice.TongTien - invoice.DaThu;
        var lyDo = CannotPayReason(invoice, conLai);
        if (lyDo != null)
        {
            return ServiceResult<InvoiceQrImage>.Fail(lyDo);
        }

        var payload = vietQr.GenerateVietQRString(vietQrSettings.BankId, vietQrSettings.AccountNumber, conLai, TransferCode(hoaDonId));
        return ServiceResult<InvoiceQrImage>.Ok(new InvoiceQrImage(
            vietQr.GenerateQRCodePNGBytes(payload),
            PaymentQrFileName.Build(invoice.TenPhong, hoaDonId)));
    }

    // Hóa đơn thuộc khách (người đứng tên hợp đồng) và đã vào luồng thanh toán; ngược lại null để trả NotFound.
    private async Task<PaymentInvoiceSnapshot?> LoadOwnedInvoiceAsync(int hoaDonId, int nguoiThueId, CancellationToken ct)
    {
        var target = await store.GetTargetByHoaDonAsync(hoaDonId, ct);
        if (target == null || !target.KhachXemDuoc(nguoiThueId))
        {
            return null;
        }

        return await store.GetInvoiceSnapshotAsync(hoaDonId, ct);
    }

    private static string TransferCode(int hoaDonId) => InvoiceTransferHandler.InvoicePrefix + hoaDonId;

    // Lý do chưa hiện được khối chuyển khoản, xét theo thứ tự; null nghĩa là thanh toán được.
    private string? CannotPayReason(PaymentInvoiceSnapshot invoice, decimal conLai)
    {
        if (invoice.IsDeleted || invoice.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
        {
            return "Hóa đơn không còn ở trạng thái nhận thanh toán.";
        }

        if (conLai <= 0)
        {
            return "Hóa đơn đã được thanh toán đủ.";
        }

        if (!InvoiceMoney.IsWholeVnd(invoice.TongTien))
        {
            return "Hóa đơn có số tiền lẻ, vui lòng liên hệ quản lý để lập lại hóa đơn.";
        }

        if (string.IsNullOrWhiteSpace(vietQrSettings.AccountNumber))
        {
            return "Chủ trọ chưa cấu hình tài khoản nhận tiền, vui lòng liên hệ quản lý.";
        }

        return null;
    }

    // Khách đã nộp minh chứng cho lượt cũ trước khi gỡ tính năng này và nhân viên chưa đối chiếu: báo để khách không chuyển lại.
    private async Task<LegacyProofNoticeDto?> FindLegacyProofAsync(int hoaDonId, CancellationToken ct)
    {
        var requests = await store.GetRequestSnapshotsAsync(hoaDonId, ct);
        var waiting = requests.FirstOrDefault(r => r.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu);
        if (waiting == null)
        {
            return null;
        }

        var proof = waiting.MinhChungs
            .Where(m => m.TrangThai == TrangThaiMinhChungThanhToan.ChoXacNhan)
            .OrderByDescending(m => m.NgayTao)
            .FirstOrDefault();

        return proof == null ? null : new LegacyProofNoticeDto(proof.MinhChungId, waiting.SoTien, proof.NgayTao);
    }
}
