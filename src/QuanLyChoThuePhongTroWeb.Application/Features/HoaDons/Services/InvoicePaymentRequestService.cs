using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Payments;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class InvoicePaymentRequestService : IInvoicePaymentRequestService
    {
        private const string ProofFolder = "payment-proofs";
        private const string NotFoundMessage = "Không tìm thấy hóa đơn.";
        private const int MaxCodeAttempts = 3;

        private readonly IInvoicePaymentStore _store;
        private readonly InvoicePaymentTransaction _transaction;
        private readonly IPaymentCodeGenerator _codeGenerator;
        private readonly IMeterImageStorageService _storage;
        private readonly IVietQRService _vietQr;
        private readonly VietQrSettings _vietQrSettings;
        private readonly InvoicePaymentOptions _options;
        private readonly ILogger<InvoicePaymentRequestService> _logger;

        public InvoicePaymentRequestService(
            IInvoicePaymentStore store,
            InvoicePaymentTransaction transaction,
            IPaymentCodeGenerator codeGenerator,
            IMeterImageStorageService storage,
            IVietQRService vietQr,
            VietQrSettings vietQrSettings,
            InvoicePaymentOptions options,
            ILogger<InvoicePaymentRequestService> logger)
        {
            _store = store;
            _transaction = transaction;
            _codeGenerator = codeGenerator;
            _storage = storage;
            _vietQr = vietQr;
            _vietQrSettings = vietQrSettings;
            _options = options;
            _logger = logger;
        }

        public async Task<ServiceResult<PaymentPanelDto>> GetPaymentPanelAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByHoaDonAsync(hoaDonId, ct);
            if (target == null || !target.KhachXemDuoc(nguoiThueId))
            {
                return ServiceResult<PaymentPanelDto>.NotFound(NotFoundMessage);
            }

            var invoice = await _store.GetInvoiceSnapshotAsync(hoaDonId, ct);
            if (invoice == null)
            {
                return ServiceResult<PaymentPanelDto>.NotFound(NotFoundMessage);
            }

            var now = _transaction.UtcNow;
            var requests = (await _store.GetRequestSnapshotsAsync(hoaDonId, ct))
                .Select(r => PaymentDtoMapper.ToDto(r, now))
                .ToList();
            var active = requests.FirstOrDefault(PaymentDtoMapper.IsActive);
            var latest = requests.FirstOrDefault();
            var conLai = invoice.TongTien - invoice.DaThu;

            return ServiceResult<PaymentPanelDto>.Ok(new PaymentPanelDto
            {
                HoaDonId = invoice.HoaDonId,
                MaHoaDon = invoice.MaHoaDon,
                TongTien = invoice.TongTien,
                DaThu = invoice.DaThu,
                ConLai = conLai,
                ChoPhepThanhToanMotPhan = invoice.ChoPhepThanhToanMotPhan,
                SoTienThanhToanToiThieu = invoice.SoTienThanhToanToiThieu,
                LuotGanNhat = active ?? (latest?.TrangThaiHieuLuc == AppTrangThaiYeuCauThanhToan.HetHan ? latest : null),
                LyDoKhongTheTao = CreateBlockReason(invoice, conLai, active != null),
                CoTheTaoLuot = CreateBlockReason(invoice, conLai, active != null) == null,
                ServerNowUtc = now,
                BankId = _vietQrSettings.BankId,
                AccountNumber = _vietQrSettings.AccountNumber,
                AccountName = _vietQrSettings.AccountName
            });
        }

        private static string? CreateBlockReason(PaymentInvoiceSnapshot invoice, decimal conLai, bool coLuotHoatDong)
        {
            if (invoice.IsDeleted || invoice.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
            {
                return "Hóa đơn không còn ở trạng thái nhận thanh toán.";
            }

            if (invoice.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan || conLai <= 0)
            {
                return "Hóa đơn đã được thanh toán đủ.";
            }

            if (!InvoiceMoney.IsWholeVnd(invoice.TongTien))
            {
                return "Hóa đơn có số tiền lẻ, vui lòng liên hệ quản lý để lập lại hóa đơn.";
            }

            return coLuotHoatDong ? "Hóa đơn đang có lượt thanh toán chưa hoàn tất." : null;
        }

        public async Task<ServiceResult<PaymentRequestDto>> CreateAsync(int hoaDonId, int nguoiThueId, int actorUserId, decimal? soTien, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByHoaDonAsync(hoaDonId, ct);
            if (target == null || !target.KhachXemDuoc(nguoiThueId))
            {
                return ServiceResult<PaymentRequestDto>.NotFound(NotFoundMessage);
            }

            return await _transaction.RunAsync(hoaDonId, actorUserId, async scope =>
            {
                var hoaDon = scope.HoaDon;
                var stateError = PayableStateError(hoaDon, scope.ConLai);
                if (stateError != null)
                {
                    return ServiceResult<PaymentRequestDto>.Fail(stateError);
                }

                if (await scope.Store.GetActiveRequestAsync(hoaDonId, scope.NowUtc, ct) != null)
                {
                    return ServiceResult<PaymentRequestDto>.Fail("Hóa đơn đang có lượt thanh toán chưa hoàn tất.");
                }

                var (amount, amountError) = InvoicePaymentPolicy.ResolveRequestAmount(
                    scope.ConLai, hoaDon.ChoPhepThanhToanMotPhan, hoaDon.SoTienThanhToanToiThieu, soTien);
                if (amountError != null)
                {
                    return ServiceResult<PaymentRequestDto>.Fail(amountError);
                }

                var maYeuCau = await NewUniqueRequestCodeAsync(hoaDonId, ct);
                if (maYeuCau == null)
                {
                    return ServiceResult<PaymentRequestDto>.Fail("Vui lòng thử lại.");
                }

                var yeuCau = YeuCauThanhToanHoaDon.Tao(
                    hoaDonId, maYeuCau, amount, scope.NowUtc.AddHours(_options.YeuCauHetHanSauGio), actorUserId, scope.NowUtc);
                scope.Store.AddRequest(yeuCau);
                await scope.SaveChangesAsync();

                return ServiceResult<PaymentRequestDto>.Ok(PaymentDtoMapper.ToDto(yeuCau, scope.NowUtc), "Đã tạo lượt thanh toán.");
            }, ct);
        }

        // Mã chứa HoaDonId nên chỉ trùng trong cùng hóa đơn, mà các thao tác trên một hóa đơn đã tuần tự nhờ khóa
        // dòng. Kiểm tra trước khi insert; unique index là lớp chặn cuối (spec §10.1).
        private async Task<string?> NewUniqueRequestCodeAsync(int hoaDonId, CancellationToken ct)
        {
            for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
            {
                var code = _codeGenerator.NewRequestCode(hoaDonId);
                if (!await _store.ExistsMaYeuCauAsync(code, ct))
                {
                    return code;
                }
            }

            return null;
        }

        private static string? PayableStateError(HoaDon hoaDon, decimal conLai)
        {
            if (hoaDon.IsDeleted || hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
            {
                return "Hóa đơn không còn ở trạng thái nhận thanh toán.";
            }

            if (hoaDon.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan || conLai <= 0)
            {
                return "Hóa đơn đã được thanh toán đủ.";
            }

            return hoaDon.TongTienLaSoNguyen()
                ? null
                : "Hóa đơn có số tiền lẻ, cần hủy và lập lại.";
        }

        public async Task<ServiceResult<bool>> CancelAsync(int yeuCauId, int nguoiThueId, int actorUserId, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByYeuCauAsync(yeuCauId, ct);
            if (target == null || !target.KhachXemDuoc(nguoiThueId))
            {
                return ServiceResult<bool>.NotFound("Không tìm thấy lượt thanh toán.");
            }

            return await _transaction.RunAsync(target.HoaDonId, actorUserId, async scope =>
            {
                if (scope.ExpiredRequestIds.Contains(yeuCauId))
                {
                    return ServiceResult<bool>.Fail("Lượt thanh toán đã hết hạn.");
                }

                var yeuCau = await scope.Store.GetRequestAsync(yeuCauId, ct);
                if (yeuCau == null || yeuCau.HoaDonId != scope.HoaDon.HoaDonId)
                {
                    return ServiceResult<bool>.NotFound("Không tìm thấy lượt thanh toán.");
                }

                if (yeuCau.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu)
                {
                    return ServiceResult<bool>.Fail("Lượt đã nộp minh chứng và đang chờ đối chiếu, không thể hủy.");
                }

                if (yeuCau.TrangThai != TrangThaiYeuCauThanhToan.ChoThanhToan)
                {
                    return ServiceResult<bool>.Fail("Lượt thanh toán đã kết thúc.");
                }

                yeuCau.Huy(actorUserId, scope.NowUtc, "Khách thuê hủy lượt thanh toán");
                return ServiceResult<bool>.Ok(true, "Đã hủy lượt thanh toán.");
            }, ct);
        }

        public async Task<ServiceResult<PaymentProofDto>> SubmitProofAsync(SubmitPaymentProofRequest request, int nguoiThueId, int actorUserId, CancellationToken ct = default)
        {
            if (request == null)
            {
                return ServiceResult<PaymentProofDto>.Fail("Dữ liệu không hợp lệ.");
            }

            var fileError = ProofImageValidator.Validate(request.File, _options.MinhChungToiDaBytes);
            if (fileError != null)
            {
                return ServiceResult<PaymentProofDto>.Fail(fileError);
            }

            var ngayChuyenError = request.NgayChuyenUtc == default
                ? "Vui lòng nhập thời điểm chuyển khoản."
                : InvoicePaymentPolicy.ValidateNotFuture(request.NgayChuyenUtc, _transaction.UtcNow, _options.DungSaiNgayTuongLaiPhut, "Thời điểm chuyển khoản");
            if (ngayChuyenError != null)
            {
                return ServiceResult<PaymentProofDto>.Fail(ngayChuyenError);
            }

            var maGiaoDich = InvoicePaymentPolicy.NormalizeText(request.MaGiaoDichNganHang);
            var maError = InvoicePaymentPolicy.ValidateMaGiaoDich(maGiaoDich);
            if (maError != null)
            {
                return ServiceResult<PaymentProofDto>.Fail(maError);
            }

            var target = await _store.GetTargetByYeuCauAsync(request.YeuCauId, ct);
            if (target == null || !target.KhachXemDuoc(nguoiThueId))
            {
                return ServiceResult<PaymentProofDto>.NotFound("Không tìm thấy lượt thanh toán.");
            }

            // Bước 1: kiểm dưới khóa. Nếu lượt vừa quá hạn, khung commit HetHan rồi trả lỗi (spec §6.4).
            var precheck = await _transaction.RunAsync(target.HoaDonId, actorUserId, async scope =>
            {
                var (yeuCau, error) = await LoadSubmittableRequestAsync(scope, request.YeuCauId, maGiaoDich, ct);
                return error != null
                    ? ServiceResult<string>.FailFrom(error)
                    : ServiceResult<string>.Ok(yeuCau!.MaYeuCau);
            }, ct);
            if (!precheck.Success)
            {
                return ServiceResult<PaymentProofDto>.FailFrom(precheck);
            }

            // Bước 2: tải ảnh, không giữ khóa dòng trong lúc gọi Cloudinary (spec §28).
            var file = request.File!;
            var upload = file with { FileName = precheck.Data + Path.GetExtension(file.FileName).ToLowerInvariant() };
            MeterImageUploadResult uploaded;
            try
            {
                uploaded = await _storage.UploadAsync(upload, ProofFolder, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải ảnh minh chứng cho lượt {YeuCauId}", request.YeuCauId);
                return ServiceResult<PaymentProofDto>.Fail("Không thể tải ảnh lên dịch vụ lưu trữ. Vui lòng thử lại.");
            }

            // Bước 3: kiểm lại dưới khóa (trạng thái có thể đã đổi) rồi ghi minh chứng.
            ServiceResult<PaymentProofDto> result;
            try
            {
                result = await _transaction.RunAsync(target.HoaDonId, actorUserId, async scope =>
                {
                    var (yeuCau, error) = await LoadSubmittableRequestAsync(scope, request.YeuCauId, maGiaoDich, ct);
                    if (error != null)
                    {
                        return ServiceResult<PaymentProofDto>.FailFrom(error);
                    }

                    var minhChung = yeuCau!.NopMinhChung(
                        uploaded.Url, uploaded.PublicId, maGiaoDich, request.NgayChuyenUtc, actorUserId, scope.NowUtc);

                    await scope.NotifyReviewersAsync(
                        target.ChiNhanhId,
                        "Minh chứng thanh toán mới",
                        $"Lượt {yeuCau.MaYeuCau} của hóa đơn {target.MaHoaDon}: khách khai đã chuyển {InvoicePaymentPolicy.FormatVnd(yeuCau.SoTien)}đ.",
                        $"/QuanLyNhaTro/DoiChieuThanhToan?ma={yeuCau.MaYeuCau}");

                    await scope.SaveChangesAsync();
                    return ServiceResult<PaymentProofDto>.Ok(PaymentDtoMapper.ToDto(minhChung, scope.NowUtc), "Đã nộp minh chứng, vui lòng chờ đối chiếu.");
                }, ct);
            }
            catch (OperationCanceledException)
            {
                await CleanupUploadedAsync(uploaded.PublicId);
                throw;
            }

            if (!result.Success)
            {
                await CleanupUploadedAsync(uploaded.PublicId);
            }

            return result;
        }

        private static async Task<(YeuCauThanhToanHoaDon? YeuCau, ServiceResult? Error)> LoadSubmittableRequestAsync(
            InvoicePaymentScope scope, int yeuCauId, string? maGiaoDich, CancellationToken ct)
        {
            if (scope.ExpiredRequestIds.Contains(yeuCauId))
            {
                return (null, ServiceResult.Fail("Lượt thanh toán đã hết hạn, vui lòng tạo lượt mới và không dùng lại QR cũ."));
            }

            var hoaDon = scope.HoaDon;
            if (hoaDon.IsDeleted || hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
            {
                return (null, ServiceResult.Fail("Hóa đơn không còn ở trạng thái nhận thanh toán."));
            }

            var yeuCau = await scope.Store.GetRequestAsync(yeuCauId, ct);
            if (yeuCau == null || yeuCau.HoaDonId != hoaDon.HoaDonId)
            {
                return (null, ServiceResult.NotFound("Không tìm thấy lượt thanh toán."));
            }

            if (yeuCau.TrangThai == TrangThaiYeuCauThanhToan.DangDoiChieu)
            {
                return (null, ServiceResult.Fail("Bạn đã nộp minh chứng cho lượt này, vui lòng chờ đối chiếu."));
            }

            if (yeuCau.TrangThai != TrangThaiYeuCauThanhToan.ChoThanhToan)
            {
                return (null, ServiceResult.Fail("Lượt thanh toán đã kết thúc."));
            }

            if (maGiaoDich != null)
            {
                if (await scope.Store.ExistsMaGiaoDichAsync(maGiaoDich, ct))
                {
                    return (null, ServiceResult.Fail("Mã giao dịch đã được ghi nhận."));
                }

                if (await scope.Store.ExistsPendingProofMaGiaoDichAsync(maGiaoDich, null, ct))
                {
                    return (null, ServiceResult.Fail("Mã giao dịch đang được dùng cho một minh chứng khác chờ đối chiếu."));
                }
            }

            return (yeuCau, null);
        }

        private async Task CleanupUploadedAsync(string publicId)
        {
            try
            {
                await _storage.DeleteAsync(publicId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể dọn ảnh minh chứng mồ côi {PublicId}", publicId);
            }
        }

        public async Task<ServiceResult<byte[]>> GetQrAsync(int yeuCauId, int nguoiThueId, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByYeuCauAsync(yeuCauId, ct);
            if (target == null || !target.KhachXemDuoc(nguoiThueId))
            {
                return ServiceResult<byte[]>.NotFound("Không tìm thấy lượt thanh toán.");
            }

            var snapshot = await _store.GetRequestSnapshotAsync(yeuCauId, ct);
            if (snapshot == null)
            {
                return ServiceResult<byte[]>.NotFound("Không tìm thấy lượt thanh toán.");
            }

            var request = PaymentDtoMapper.ToDto(snapshot, _transaction.UtcNow);
            if (target.IsDeleted || request.TrangThaiHieuLuc != AppTrangThaiYeuCauThanhToan.ChoThanhToan)
            {
                return ServiceResult<byte[]>.Fail("QR chỉ dùng cho lượt đang chờ thanh toán và còn hạn.");
            }

            var payload = _vietQr.GenerateVietQRString(_vietQrSettings.BankId, _vietQrSettings.AccountNumber, request.SoTien, request.NoiDungChuyenKhoan);
            return ServiceResult<byte[]>.Ok(_vietQr.GenerateQRCodePNGBytes(payload));
        }

        public async Task<ServiceResult<MeterImageReadResult>> GetProofImageAsync(int minhChungId, int nguoiThueId, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByMinhChungAsync(minhChungId, ct);
            if (target == null || !target.KhachXemDuoc(nguoiThueId) || target.YeuCauId == null)
            {
                return ServiceResult<MeterImageReadResult>.NotFound("Không tìm thấy ảnh minh chứng.");
            }

            return await PaymentProofImageReader.ReadAsync(_store, _storage, _logger, target.YeuCauId.Value, minhChungId, ct);
        }
    }

    // Đọc ảnh minh chứng qua server để kiểm quyền thay vì lộ URL Cloudinary; dùng chung cho khách và nhân viên.
    internal static class PaymentProofImageReader
    {
        public static async Task<ServiceResult<MeterImageReadResult>> ReadAsync(
            IInvoicePaymentStore store,
            IMeterImageStorageService storage,
            ILogger logger,
            int yeuCauId,
            int minhChungId,
            CancellationToken ct)
        {
            var snapshot = await store.GetRequestSnapshotAsync(yeuCauId, ct);
            var proof = snapshot?.MinhChungs.FirstOrDefault(m => m.MinhChungId == minhChungId);
            if (proof == null)
            {
                return ServiceResult<MeterImageReadResult>.NotFound("Không tìm thấy ảnh minh chứng.");
            }

            try
            {
                return ServiceResult<MeterImageReadResult>.Ok(await storage.ReadAsync(proof.PublicId ?? string.Empty, proof.HinhAnhUrl, ct));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không đọc được ảnh minh chứng {MinhChungId}", minhChungId);
                return ServiceResult<MeterImageReadResult>.Fail("Không tải được ảnh minh chứng.");
            }
        }
    }
}
