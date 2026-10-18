using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Biên lai web của một dòng sổ (spec thanh toán trực tuyến §3.4). Chỉ đọc. Dòng đã hủy ghi nhận không có biên lai (NotFound).
// Đợt, lũy kế, còn nợ, tiền thừa lấy từ GetReceiptSnapshotAsync, cùng công thức với PaymentNotificationFormatter.BuildReceiptEmail.
public sealed class InvoiceReceiptService(IInvoicePaymentStore store, IEmployeeAccessService access) : IInvoiceReceiptService
{
    private const string NotFoundMessage = "Không tìm thấy biên lai.";
    private const string NoPermissionMessage = "Bạn không có quyền xem biên lai này.";
    private const string SePayIdPrefix = InvoicePaymentPolicy.SePayMaGiaoDichPrefix + "ID-";

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
        MaThamChieu = BankReference(receipt.MaGiaoDich),
        TongTien = receipt.TongTien,
        SoTienLanNay = receipt.SoTienThanhToan,
        LuyKeDaThanhToan = receipt.LuyKeDaThanhToan,
        ConNo = Math.Max(0m, receipt.TongTien - receipt.LuyKeDaThanhToan),
        TienThua = PaymentNoteTags.Parse(receipt.GhiChu).TienThua ?? 0m,
        DaThuDu = receipt.LuyKeDaThanhToan >= receipt.TongTien,
        DotThu = receipt.DotThu
    };

    // SEPAY-ID-{id}: khóa chống trùng khi webhook không có mã tham chiếu, không phải mã ngân hàng. SEPAY-{X}: X là mã tham chiếu.
    // Dòng thu thủ công không có mã tham chiếu.
    private static string? BankReference(string maGiaoDich)
    {
        if (maGiaoDich.StartsWith(SePayIdPrefix, StringComparison.OrdinalIgnoreCase) ||
            !maGiaoDich.StartsWith(InvoicePaymentPolicy.SePayMaGiaoDichPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var reference = maGiaoDich[InvoicePaymentPolicy.SePayMaGiaoDichPrefix.Length..];
        return string.IsNullOrWhiteSpace(reference) ? null : reference;
    }
}
