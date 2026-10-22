using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class InvoiceLedgerService : IInvoiceLedgerService
    {
        private const string InvoiceNotFound = "Không tìm thấy hóa đơn.";

        private readonly IInvoicePaymentStore _store;
        private readonly InvoicePaymentTransaction _transaction;
        private readonly IEmployeeAccessService _access;
        private readonly IPaymentCodeGenerator _codeGenerator;
        private readonly InvoicePaymentOptions _options;

        public InvoiceLedgerService(
            IInvoicePaymentStore store,
            InvoicePaymentTransaction transaction,
            IEmployeeAccessService access,
            IPaymentCodeGenerator codeGenerator,
            InvoicePaymentOptions options)
        {
            _store = store;
            _transaction = transaction;
            _access = access;
            _codeGenerator = codeGenerator;
            _options = options;
        }

        public async Task<ServiceResult<ManualCollectionResult>> CollectManuallyAsync(ManualCollectionRequest request, int actorId, CancellationToken ct = default)
        {
            if (request == null || actorId <= 0)
            {
                return ServiceResult<ManualCollectionResult>.Fail("Dữ liệu không hợp lệ.");
            }

            var laTienMat = request.PhuongThuc == AppPhuongThucThanhToan.TienMat;
            var maGiaoDich = InvoicePaymentPolicy.NormalizeText(request.MaGiaoDich);
            var ghiChu = InvoicePaymentPolicy.NormalizeText(request.GhiChu);

            var inputError = (Enum.IsDefined(request.PhuongThuc) ? null : "Phương thức thanh toán không hợp lệ.")
                ?? InvoicePaymentPolicy.ValidateWholeAmount(request.SoTienThucNhan, "Số tiền thực nhận")
                ?? (request.NgayThanhToanUtc == default ? "Vui lòng nhập ngày thanh toán." : null)
                ?? InvoicePaymentPolicy.ValidateNotFuture(request.NgayThanhToanUtc, _transaction.UtcNow, _options.DungSaiNgayTuongLaiPhut, "Ngày thanh toán")
                ?? InvoicePaymentPolicy.ValidateMaGiaoDich(maGiaoDich)
                ?? InvoicePaymentPolicy.ValidateGhiChuTuyChon(ghiChu)
                ?? (laTienMat && maGiaoDich != null ? "Thu tiền mặt không nhập mã giao dịch ngân hàng." : null)
                // Chuyển khoản không có mã (ví dụ một lần chuyển trả nhiều hóa đơn) thì ghi chú tham chiếu là bắt buộc.
                ?? (!laTienMat && maGiaoDich == null && ghiChu == null
                    ? "Chuyển khoản không có mã giao dịch thì phải ghi chú tham chiếu giao dịch."
                    : null);
            if (inputError != null)
            {
                return ServiceResult<ManualCollectionResult>.Fail(inputError);
            }

            var target = await _store.GetTargetByHoaDonAsync(request.HoaDonId, ct);
            if (target == null || target.IsDeleted)
            {
                return ServiceResult<ManualCollectionResult>.NotFound(InvoiceNotFound);
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.InvoiceSend, ct))
            {
                return ServiceResult<ManualCollectionResult>.Forbidden("Bạn không có quyền thu tiền hóa đơn tại chi nhánh này.");
            }

            var isAdmin = (await _access.GetScopeAsync(actorId, ct))?.IsAdmin == true;

            return await _transaction.RunAsync(request.HoaDonId, actorId, async scope =>
            {
                var hoaDon = scope.HoaDon;
                if (hoaDon.IsDeleted || hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
                {
                    return ServiceResult<ManualCollectionResult>.Fail("Chỉ có thể thu tiền cho hóa đơn đã gửi đến khách thuê.");
                }

                if (hoaDon.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan || scope.ConLai <= 0)
                {
                    return ServiceResult<ManualCollectionResult>.Fail("Hóa đơn này đã được thanh toán đủ.");
                }

                if (!hoaDon.TongTienLaSoNguyen())
                {
                    return ServiceResult<ManualCollectionResult>.Fail("Hóa đơn có số tiền lẻ, cần hủy và lập lại.");
                }

                var active = await scope.Store.GetActiveRequestAsync(hoaDon.HoaDonId, scope.NowUtc, ct);
                if (active?.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu)
                {
                    return ServiceResult<ManualCollectionResult>.Fail("Khách đã nộp minh chứng, hãy đối chiếu trước để tránh ghi trùng.");
                }

                // Không phụ thuộc cấu hình trả một phần: thu thủ công ghi đúng số thực nhận (spec §9.3, §18).
                var conLaiTruoc = scope.ConLai;
                var decision = InvoicePaymentPolicy.DecideRecording(request.SoTienThucNhan, null, conLaiTruoc, isAdmin);
                if (decision.Kind == RecordingKind.NeedsAdmin)
                {
                    return ServiceResult<ManualCollectionResult>.Fail("Số tiền vượt số còn nợ, cần Admin xử lý.");
                }

                if (decision.Kind == RecordingKind.Overpay && ghiChu == null)
                {
                    return ServiceResult<ManualCollectionResult>.Fail("Số tiền vượt số còn nợ, vui lòng ghi chú cách xử lý tiền thừa.");
                }

                var maLedger = maGiaoDich ?? _codeGenerator.NewLedgerCode(laTienMat ? "TM" : "CK", scope.NowUtc);
                if (maGiaoDich != null && await scope.Store.ExistsMaGiaoDichAsync(maGiaoDich, ct))
                {
                    return ServiceResult<ManualCollectionResult>.Fail("Mã giao dịch đã được ghi nhận.");
                }

                string? maLuotDaHuy = null;
                if (active?.TrangThai == TrangThaiYeuCauThanhToan.ChoThanhToan)
                {
                    active.Huy(actorId, scope.NowUtc, "Đã thu bằng phương thức khác");
                    maLuotDaHuy = active.MaYeuCau;
                }

                var ledger = new LichSuThanhToan
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaGiaoDich = maLedger,
                    SoTienThanhToan = decision.SoTienGhiNhan,
                    PhuongThucThanhToan = laTienMat ? PhuongThucThanhToan.TienMat : PhuongThucThanhToan.ChuyenKhoan,
                    NgayThanhToan = request.NgayThanhToanUtc,
                    NgayXacNhan = scope.NowUtc,
                    NguoiXacNhanId = actorId,
                    GhiChu = decision.Kind == RecordingKind.Overpay
                        ? PaymentNoteTags.TienThua(decision.ChenhLech, ghiChu!)
                        : ghiChu ?? string.Empty
                };
                scope.Store.AddLedgerEntry(ledger);
                await scope.ApplyLedgerChangeAsync($"Thu tiền {(laTienMat ? "tiền mặt" : "chuyển khoản")} {maLedger}");

                await scope.NotifyTenantAsync(
                    "Đã ghi nhận thanh toán",
                    $"Hóa đơn {hoaDon.MaHoaDon}: đã ghi nhận {Vnd(decision.SoTienGhiNhan)}đ, còn lại {Vnd(scope.ConLai)}đ.");

                if (decision.Kind == RecordingKind.Overpay)
                {
                    await scope.NotifyAdminsAsync(
                        "Ghi nhận thanh toán có tiền thừa",
                        $"Hóa đơn {hoaDon.MaHoaDon}: tiền thừa {Vnd(decision.ChenhLech)}đ cần xử lý ngoài hệ thống.",
                        "/QuanLyNhaTro/LichSuThanhToan");
                }

                return ServiceResult<ManualCollectionResult>.Ok(new ManualCollectionResult
                {
                    LichSuThanhToanId = ledger.LichSuThanhToanId,
                    MaGiaoDich = maLedger,
                    SoTienGhiNhan = decision.SoTienGhiNhan,
                    TienThua = decision.Kind == RecordingKind.Overpay ? decision.ChenhLech : 0,
                    ConLai = scope.ConLai,
                    TrangThaiHoaDon = InvoiceStatusLabels.ThanhToan(hoaDon.TrangThaiHoaDon),
                    MaLuotDaHuy = maLuotDaHuy
                }, "Thu tiền thành công.");
            }, ct);
        }

        public async Task<ServiceResult<VoidPaymentResult>> VoidPaymentAsync(int lichSuThanhToanId, int actorId, string lyDo, CancellationToken ct = default)
        {
            var lyDoError = InvoicePaymentPolicy.ValidateLyDo(lyDo, "Lý do hủy ghi nhận");
            if (lyDoError != null)
            {
                return ServiceResult<VoidPaymentResult>.Fail(lyDoError);
            }

            if ((await _access.GetScopeAsync(actorId, ct))?.IsAdmin != true)
            {
                return ServiceResult<VoidPaymentResult>.Forbidden("Chỉ Admin được hủy ghi nhận thanh toán.");
            }

            var target = await _store.GetTargetByLedgerEntryAsync(lichSuThanhToanId, ct);
            if (target == null)
            {
                return ServiceResult<VoidPaymentResult>.NotFound("Không tìm thấy khoản thanh toán.");
            }

            var lyDoTrim = lyDo.Trim();
            return await _transaction.RunAsync(target.HoaDonId, actorId, async scope =>
            {
                var ledger = await scope.Store.GetLedgerEntryAsync(lichSuThanhToanId, ct);
                if (ledger == null || ledger.HoaDonId != scope.HoaDon.HoaDonId)
                {
                    return ServiceResult<VoidPaymentResult>.NotFound("Không tìm thấy khoản thanh toán.");
                }

                if (ledger.IsDeleted)
                {
                    return ServiceResult<VoidPaymentResult>.Fail("Khoản thanh toán đã bị hủy ghi nhận trước đó.");
                }

                ledger.HuyGhiNhan(PaymentNoteTags.AppendDaHuy(ledger.GhiChu, scope.NowUtc, actorId, lyDoTrim));

                // Luôn ghi lịch sử hóa đơn: đây là nơi lưu người, thời điểm và lý do hủy (spec §20).
                await scope.ApplyLedgerChangeAsync(
                    $"Hủy ghi nhận thanh toán {ledger.MaGiaoDich} ({Vnd(ledger.SoTienThanhToan)}đ): {lyDoTrim}",
                    luonGhiLichSu: true);

                return ServiceResult<VoidPaymentResult>.Ok(new VoidPaymentResult
                {
                    HoaDonId = scope.HoaDon.HoaDonId,
                    ConLai = scope.ConLai,
                    TrangThaiHoaDon = InvoiceStatusLabels.ThanhToan(scope.HoaDon.TrangThaiHoaDon)
                }, $"Đã hủy ghi nhận. Hóa đơn hiện ở trạng thái {InvoiceStatusLabels.ThanhToan(scope.HoaDon.TrangThaiHoaDon).ToLowerInvariant()}.");
            }, ct);
        }

        public async Task<ServiceResult<bool>> CancelInvoiceAsync(int hoaDonId, int actorId, string lyDo, CancellationToken ct = default)
        {
            if (actorId <= 0)
            {
                return ServiceResult<bool>.Fail("Người thực hiện không hợp lệ.");
            }

            var lyDoError = InvoicePaymentPolicy.ValidateLyDo(lyDo, "Lý do hủy hóa đơn");
            if (lyDoError != null)
            {
                return ServiceResult<bool>.Fail(lyDoError);
            }

            var target = await _store.GetTargetByHoaDonAsync(hoaDonId, ct);
            if (target == null || target.IsDeleted)
            {
                return ServiceResult<bool>.NotFound(InvoiceNotFound);
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.InvoiceCancel, ct))
            {
                return ServiceResult<bool>.Forbidden("Bạn không có quyền hủy hóa đơn tại chi nhánh này.");
            }

            var lyDoTrim = lyDo.Trim();
            return await _transaction.RunAsync(hoaDonId, actorId, async scope =>
            {
                var hoaDon = scope.HoaDon;
                if (hoaDon.IsDeleted)
                {
                    return ServiceResult<bool>.NotFound(InvoiceNotFound);
                }

                if (hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaChot && hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
                {
                    return ServiceResult<bool>.Fail("Chỉ hóa đơn đã chốt hoặc đã gửi mới được phép hủy.");
                }

                if (scope.DaThu > 0 || hoaDon.TrangThaiHoaDon != TrangThaiHoaDon.ChuaThanhToan)
                {
                    return ServiceResult<bool>.Fail("Không thể hủy hóa đơn đã phát sinh khoản thanh toán.");
                }

                var active = await scope.Store.GetActiveRequestAsync(hoaDonId, scope.NowUtc, ct);
                if (active?.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu)
                {
                    return ServiceResult<bool>.Fail("Khách đã nộp minh chứng thanh toán, cần đối chiếu trước khi hủy hóa đơn.");
                }

                active?.Huy(actorId, scope.NowUtc, $"Hóa đơn bị hủy: {lyDoTrim}");
                hoaDon.HuyHoaDon(actorId, lyDoTrim, scope.NowUtc);
                return ServiceResult<bool>.Ok(true, "Đã hủy hóa đơn.");
            }, ct);
        }

        private static string Vnd(decimal soTien) => InvoicePaymentPolicy.FormatVnd(soTien);
    }
}
