using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
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
    public class InvoicePaymentConfirmationService : IInvoicePaymentConfirmationService
    {
        private const string ProofNotFound = "Không tìm thấy minh chứng thanh toán.";
        private const string RequestNotFound = "Không tìm thấy lượt thanh toán.";
        private const string NoReviewPermission = "Bạn không có quyền đối chiếu thanh toán tại chi nhánh này.";

        private readonly IInvoicePaymentStore _store;
        private readonly InvoicePaymentTransaction _transaction;
        private readonly IEmployeeAccessService _access;
        private readonly IMeterImageStorageService _storage;
        private readonly InvoicePaymentOptions _options;
        private readonly ILogger<InvoicePaymentConfirmationService> _logger;

        public InvoicePaymentConfirmationService(
            IInvoicePaymentStore store,
            InvoicePaymentTransaction transaction,
            IEmployeeAccessService access,
            IMeterImageStorageService storage,
            InvoicePaymentOptions options,
            ILogger<InvoicePaymentConfirmationService> logger)
        {
            _store = store;
            _transaction = transaction;
            _access = access;
            _storage = storage;
            _options = options;
            _logger = logger;
        }

        public async Task<ServiceResult<DataTableResponse<ReviewQueueRowDto>>> GetReviewQueueAsync(
            int actorId, ReviewQueueFilter filter, DataTableRequest request, CancellationToken ct = default)
        {
            var scope = await _access.GetScopeAsync(actorId, ct);
            if (scope == null)
            {
                return ServiceResult<DataTableResponse<ReviewQueueRowDto>>.Forbidden("Bạn không có quyền xem hàng đợi đối chiếu.");
            }

            filter ??= new ReviewQueueFilter();
            var empty = new DataTableResponse<ReviewQueueRowDto> { draw = request.Draw };
            if (filter.ChiNhanhId > 0 && !scope.CanAccessBranch(filter.ChiNhanhId))
            {
                return ServiceResult<DataTableResponse<ReviewQueueRowDto>>.Ok(empty);
            }

            var page = await _store.GetReviewQueueAsync(new ReviewQueueQuery
            {
                ChiNhanhId = filter.ChiNhanhId,
                AllowedBranchIds = scope.IsAdmin ? null : scope.ActiveBranchIds.ToList(),
                DaXuLy = filter.DaXuLy,
                ChiChoAdmin = filter.ChiChoAdmin,
                SearchValue = request.SearchValue,
                Start = Math.Max(0, request.Start),
                Length = request.Length is > 0 and <= 100 ? request.Length : 25
            }, ct);

            return ServiceResult<DataTableResponse<ReviewQueueRowDto>>.Ok(new DataTableResponse<ReviewQueueRowDto>
            {
                draw = request.Draw,
                recordsTotal = page.Total,
                recordsFiltered = page.Total,
                data = page.Rows.Select(PaymentDtoMapper.ToDto).ToList()
            });
        }

        public async Task<ServiceResult<PaymentRequestDetailDto>> GetProofDetailAsync(int minhChungId, int actorId, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByMinhChungAsync(minhChungId, ct);
            if (target?.YeuCauId == null)
            {
                return ServiceResult<PaymentRequestDetailDto>.NotFound(ProofNotFound);
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.PaymentReview, ct))
            {
                return ServiceResult<PaymentRequestDetailDto>.Forbidden(NoReviewPermission);
            }

            return await BuildDetailAsync(target, minhChungId, actorId, ct);
        }

        public async Task<ServiceResult<MeterImageReadResult>> GetProofImageAsync(int minhChungId, int actorId, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByMinhChungAsync(minhChungId, ct);
            if (target?.YeuCauId == null)
            {
                return ServiceResult<MeterImageReadResult>.NotFound("Không tìm thấy ảnh minh chứng.");
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.PaymentReview, ct))
            {
                return ServiceResult<MeterImageReadResult>.Forbidden(NoReviewPermission);
            }

            return await PaymentProofImageReader.ReadAsync(_store, _storage, _logger, target.YeuCauId.Value, minhChungId, ct);
        }

        public async Task<ServiceResult<PaymentRequestDetailDto>> SearchPaymentRequestAsync(string maYeuCau, int actorId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(maYeuCau))
            {
                return ServiceResult<PaymentRequestDetailDto>.Fail("Vui lòng nhập mã lượt thanh toán.");
            }

            var target = await _store.GetTargetByMaYeuCauAsync(maYeuCau, ct);
            // Ngoài phạm vi chi nhánh trả "không tìm thấy" như không tồn tại (spec §21).
            if (target?.YeuCauId == null ||
                !await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.PaymentReview, ct))
            {
                return ServiceResult<PaymentRequestDetailDto>.NotFound(RequestNotFound);
            }

            return await BuildDetailAsync(target, null, actorId, ct);
        }

        private async Task<ServiceResult<PaymentRequestDetailDto>> BuildDetailAsync(
            InvoicePaymentTarget target, int? minhChungId, int actorId, CancellationToken ct)
        {
            var invoice = await _store.GetInvoiceSnapshotAsync(target.HoaDonId, ct);
            var request = await _store.GetRequestSnapshotAsync(target.YeuCauId!.Value, ct);
            if (invoice == null || request == null)
            {
                return ServiceResult<PaymentRequestDetailDto>.NotFound(RequestNotFound);
            }

            var isAdmin = await IsAdminAsync(actorId, ct);
            var requestDto = PaymentDtoMapper.ToDto(request, _transaction.UtcNow);
            var proof = minhChungId.HasValue ? requestDto.MinhChungs.FirstOrDefault(m => m.MinhChungId == minhChungId.Value) : null;

            return ServiceResult<PaymentRequestDetailDto>.Ok(new PaymentRequestDetailDto
            {
                HoaDonId = invoice.HoaDonId,
                MaHoaDon = invoice.MaHoaDon,
                TenPhong = invoice.TenPhong,
                TenChiNhanh = invoice.TenChiNhanh,
                TenKhachThue = invoice.TenKhachThue,
                TongTien = invoice.TongTien,
                DaThu = invoice.DaThu,
                ConLai = invoice.TongTien - invoice.DaThu,
                ChoPhepThanhToanMotPhan = invoice.ChoPhepThanhToanMotPhan,
                TrangThaiHoaDon = InvoiceStatusLabels.HienThi(invoice.TrangThaiPhatHanh, invoice.TrangThaiHoaDon),
                HoaDonDaHuy = invoice.IsDeleted || invoice.TrangThaiPhatHanh == TrangThaiPhatHanhHoaDon.DaHuy,
                LuotThanhToan = requestDto,
                MinhChungDangXemId = proof?.MinhChungId,
                LaAdmin = isAdmin,
                CoTheDoiChieu = proof != null &&
                    proof.TrangThai == AppTrangThaiMinhChungThanhToan.ChoXacNhan &&
                    requestDto.TrangThai == AppTrangThaiYeuCauThanhToan.DangDoiChieu &&
                    (isAdmin || !requestDto.ChoAdmin)
            });
        }

        public async Task<ServiceResult<ConfirmPaymentResult>> ConfirmAsync(ConfirmPaymentRequest request, int actorId, CancellationToken ct = default)
        {
            if (request == null)
            {
                return ServiceResult<ConfirmPaymentResult>.Fail("Dữ liệu không hợp lệ.");
            }

            var inputError = InvoicePaymentPolicy.ValidateWholeAmount(request.SoTienThucNhan, "Số tiền thực nhận")
                ?? (request.NgayGiaoDichUtc == default ? "Vui lòng nhập ngày giao dịch." : null)
                ?? InvoicePaymentPolicy.ValidateNotFuture(request.NgayGiaoDichUtc, _transaction.UtcNow, _options.DungSaiNgayTuongLaiPhut, "Ngày giao dịch")
                ?? InvoicePaymentPolicy.ValidateGhiChuTuyChon(request.GhiChu);
            var maGiaoDich = InvoicePaymentPolicy.NormalizeText(request.MaGiaoDich);
            inputError ??= InvoicePaymentPolicy.ValidateMaGiaoDich(maGiaoDich);
            if (inputError != null)
            {
                return ServiceResult<ConfirmPaymentResult>.Fail(inputError);
            }

            var target = await _store.GetTargetByMinhChungAsync(request.MinhChungId, ct);
            if (target == null)
            {
                return ServiceResult<ConfirmPaymentResult>.NotFound(ProofNotFound);
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.PaymentReview, ct))
            {
                return ServiceResult<ConfirmPaymentResult>.Forbidden(NoReviewPermission);
            }

            var isAdmin = await IsAdminAsync(actorId, ct);
            var ghiChu = InvoicePaymentPolicy.NormalizeText(request.GhiChu);

            return await _transaction.RunAsync(target.HoaDonId, actorId, async scope =>
            {
                var (minhChung, error) = await LoadReviewableProofAsync(scope, request.MinhChungId, isAdmin, ct);
                if (error != null)
                {
                    return ServiceResult<ConfirmPaymentResult>.FailFrom(error);
                }

                var yeuCau = minhChung!.YeuCauThanhToanHoaDon;
                var hoaDon = scope.HoaDon;
                if (scope.ConLai <= 0)
                {
                    return ServiceResult<ConfirmPaymentResult>.Fail("Hóa đơn đã được thanh toán đủ; khoản tiền này cần Admin xử lý ngoài hệ thống.");
                }

                var conLaiTruoc = scope.ConLai;
                var thucNhan = request.SoTienThucNhan;
                var decision = InvoicePaymentPolicy.DecideRecording(thucNhan, yeuCau.SoTien, conLaiTruoc, isAdmin);
                if (decision.Kind != RecordingKind.Exact && ghiChu == null)
                {
                    return ServiceResult<ConfirmPaymentResult>.Fail("Số thực nhận khác số lượt, vui lòng ghi chú lý do.");
                }

                var link = $"/QuanLyNhaTro/DoiChieuThanhToan?ma={yeuCau.MaYeuCau}";

                if (decision.Kind == RecordingKind.NeedsAdmin)
                {
                    yeuCau.GhiChuyenAdmin(actorId, scope.NowUtc, PaymentNoteTags.ChoAdmin(thucNhan, conLaiTruoc, ghiChu));
                    await scope.NotifyAdminsAsync(
                        "Thanh toán cần Admin xử lý",
                        $"Lượt {yeuCau.MaYeuCau} (hóa đơn {hoaDon.MaHoaDon}): thực nhận {Vnd(thucNhan)}đ, vượt số còn nợ {Vnd(conLaiTruoc)}đ.",
                        link);

                    return ServiceResult<ConfirmPaymentResult>.Ok(new ConfirmPaymentResult
                    {
                        KetQua = KetQuaDoiChieu.DaChuyenAdmin,
                        ChenhLech = decision.ChenhLech,
                        ConLai = conLaiTruoc,
                        TrangThaiHoaDon = InvoiceStatusLabels.ThanhToan(hoaDon.TrangThaiHoaDon),
                        Message = "Số thực nhận vượt số còn nợ, đã chuyển Admin xử lý."
                    }, "Đã chuyển Admin xử lý.");
                }

                var maGhiNhan = maGiaoDich ?? yeuCau.MaYeuCau;
                if (await scope.Store.ExistsMaGiaoDichAsync(maGhiNhan, ct))
                {
                    return ServiceResult<ConfirmPaymentResult>.Fail("Mã giao dịch đã được ghi nhận.");
                }

                scope.Store.AddLedgerEntry(new LichSuThanhToan
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaGiaoDich = maGhiNhan,
                    SoTienThanhToan = decision.SoTienGhiNhan,
                    PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
                    NgayThanhToan = request.NgayGiaoDichUtc,
                    NgayXacNhan = scope.NowUtc,
                    NguoiXacNhanId = actorId,
                    MinhChungThanhToanHoaDonId = minhChung.MinhChungThanhToanHoaDonId,
                    GhiChu = decision.Kind switch
                    {
                        RecordingKind.Variance => PaymentNoteTags.ChenhLech(decision.ChenhLech, ghiChu!),
                        RecordingKind.Overpay => PaymentNoteTags.TienThua(decision.ChenhLech, ghiChu!),
                        _ => ghiChu ?? string.Empty
                    }
                });

                yeuCau.HoanTat(minhChung, actorId, scope.NowUtc, decision.Kind switch
                {
                    RecordingKind.Variance => $"Thực nhận {Vnd(thucNhan)}, khác số lượt {Vnd(yeuCau.SoTien)}: {ghiChu}",
                    RecordingKind.Overpay => $"Thực nhận {Vnd(thucNhan)}, vượt số còn nợ {Vnd(conLaiTruoc)}; ghi nhận {Vnd(decision.SoTienGhiNhan)}, tiền thừa {Vnd(decision.ChenhLech)}: {ghiChu}",
                    _ => $"Đối chiếu khớp, ghi nhận {Vnd(decision.SoTienGhiNhan)}"
                });

                await scope.ApplyLedgerChangeAsync($"Xác nhận thanh toán lượt {yeuCau.MaYeuCau}");

                await scope.NotifyTenantAsync(
                    "Thanh toán đã được xác nhận",
                    $"Hóa đơn {hoaDon.MaHoaDon}: đã ghi nhận {Vnd(decision.SoTienGhiNhan)}đ, còn lại {Vnd(scope.ConLai)}đ.");

                if (decision.Kind == RecordingKind.Overpay)
                {
                    await scope.NotifyAdminsAsync(
                        "Ghi nhận thanh toán có tiền thừa",
                        $"Hóa đơn {hoaDon.MaHoaDon}: tiền thừa {Vnd(decision.ChenhLech)}đ cần xử lý ngoài hệ thống.",
                        link);
                }
                else if (decision.Kind == RecordingKind.Variance && !hoaDon.ChoPhepThanhToanMotPhan && scope.ConLai > 0)
                {
                    await scope.NotifyAdminsAsync(
                        "Ghi nhận thanh toán thiếu",
                        $"Hóa đơn {hoaDon.MaHoaDon} không cho trả một phần nhưng thực nhận {Vnd(thucNhan)}đ, còn nợ {Vnd(scope.ConLai)}đ.",
                        link);
                }

                var ketQua = decision.Kind switch
                {
                    RecordingKind.Variance => KetQuaDoiChieu.DaGhiNhanSoThucNhan,
                    RecordingKind.Overpay => KetQuaDoiChieu.DaXacNhanCoTienThua,
                    _ => KetQuaDoiChieu.DaXacNhan
                };

                return ServiceResult<ConfirmPaymentResult>.Ok(new ConfirmPaymentResult
                {
                    KetQua = ketQua,
                    SoTienGhiNhan = decision.SoTienGhiNhan,
                    ChenhLech = decision.ChenhLech,
                    ConLai = scope.ConLai,
                    TrangThaiHoaDon = InvoiceStatusLabels.ThanhToan(hoaDon.TrangThaiHoaDon),
                    Message = $"Đã ghi nhận {Vnd(decision.SoTienGhiNhan)}đ."
                }, "Đã xác nhận thanh toán.");
            }, ct);
        }

        public async Task<ServiceResult<bool>> RejectAsync(int minhChungId, int actorId, string lyDo, CancellationToken ct = default)
        {
            var lyDoError = InvoicePaymentPolicy.ValidateLyDo(lyDo, "Lý do từ chối");
            if (lyDoError != null)
            {
                return ServiceResult<bool>.Fail(lyDoError);
            }

            var target = await _store.GetTargetByMinhChungAsync(minhChungId, ct);
            if (target == null)
            {
                return ServiceResult<bool>.NotFound(ProofNotFound);
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.PaymentReview, ct))
            {
                return ServiceResult<bool>.Forbidden(NoReviewPermission);
            }

            var isAdmin = await IsAdminAsync(actorId, ct);
            var lyDoTrim = lyDo.Trim();

            return await _transaction.RunAsync(target.HoaDonId, actorId, async scope =>
            {
                var (minhChung, error) = await LoadReviewableProofAsync(scope, minhChungId, isAdmin, ct);
                if (error != null)
                {
                    return ServiceResult<bool>.FailFrom(error);
                }

                var yeuCau = minhChung!.YeuCauThanhToanHoaDon;
                yeuCau.TuChoiMinhChung(minhChung, actorId, lyDoTrim, scope.NowUtc, scope.NowUtc.AddHours(_options.YeuCauHetHanSauGio));

                await scope.NotifyTenantAsync(
                    "Minh chứng thanh toán bị từ chối",
                    $"Lượt {yeuCau.MaYeuCau} (hóa đơn {scope.HoaDon.MaHoaDon}): {lyDoTrim}. Bạn có thể nộp lại minh chứng trên cùng lượt.");

                return ServiceResult<bool>.Ok(true, "Đã từ chối minh chứng.");
            }, ct);
        }

        // Minh chứng phải còn chờ xác nhận, lượt đang chờ đối chiếu; lượt "chờ Admin" chỉ Admin xử lý (spec §16.1).
        private static async Task<(MinhChungThanhToanHoaDon? MinhChung, ServiceResult? Error)> LoadReviewableProofAsync(
            InvoicePaymentScope scope, int minhChungId, bool isAdmin, CancellationToken ct)
        {
            var minhChung = await scope.Store.GetProofAsync(minhChungId, ct);
            if (minhChung == null || minhChung.YeuCauThanhToanHoaDon.HoaDonId != scope.HoaDon.HoaDonId)
            {
                return (null, ServiceResult.NotFound(ProofNotFound));
            }

            if (minhChung.TrangThaiDoiChieu != TrangThaiMinhChungThanhToan.ChoXacNhan ||
                minhChung.YeuCauThanhToanHoaDon.TrangThai != TrangThaiYeuCauThanhToan.DangDoiChieu)
            {
                return (null, ServiceResult.Fail("Minh chứng đã được xử lý trước đó."));
            }

            if (scope.HoaDon.IsDeleted || scope.HoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
            {
                return (null, ServiceResult.Fail("Hóa đơn không còn ở trạng thái nhận thanh toán."));
            }

            if (!isAdmin && PaymentNoteTags.IsWaitingAdmin(minhChung.YeuCauThanhToanHoaDon))
            {
                return (null, ServiceResult.Forbidden("Lượt này đang chờ Admin xử lý."));
            }

            return (minhChung, null);
        }

        public async Task<ServiceResult<InvoicePaymentSummaryDto>> ConfigurePartialPaymentAsync(
            ConfigurePartialPaymentRequest request, int actorId, CancellationToken ct = default)
        {
            if (request == null)
            {
                return ServiceResult<InvoicePaymentSummaryDto>.Fail("Dữ liệu không hợp lệ.");
            }

            if (request.ChoPhep)
            {
                var amountError = request.SoTienToiThieu.HasValue
                    ? InvoicePaymentPolicy.ValidateWholeAmount(request.SoTienToiThieu.Value, "Mức thanh toán tối thiểu")
                    : "Vui lòng nhập mức thanh toán tối thiểu.";
                if (amountError != null)
                {
                    return ServiceResult<InvoicePaymentSummaryDto>.Fail(amountError);
                }
            }

            var target = await _store.GetTargetByHoaDonAsync(request.HoaDonId, ct);
            if (target == null)
            {
                return ServiceResult<InvoicePaymentSummaryDto>.NotFound("Không tìm thấy hóa đơn.");
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.PaymentConfigurePartial, ct))
            {
                return ServiceResult<InvoicePaymentSummaryDto>.Forbidden("Chỉ Admin được cấu hình thanh toán một phần.");
            }

            return await _transaction.RunAsync(request.HoaDonId, actorId, async scope =>
            {
                var hoaDon = scope.HoaDon;
                if (hoaDon.IsDeleted || hoaDon.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.DaGui)
                {
                    return ServiceResult<InvoicePaymentSummaryDto>.Fail("Chỉ cấu hình cho hóa đơn đã gửi khách.");
                }

                if (hoaDon.TrangThaiHoaDon == TrangThaiHoaDon.DaThanhToan)
                {
                    return ServiceResult<InvoicePaymentSummaryDto>.Fail("Hóa đơn đã được thanh toán đủ.");
                }

                if (await scope.Store.GetActiveRequestAsync(hoaDon.HoaDonId, scope.NowUtc, ct) != null)
                {
                    return ServiceResult<InvoicePaymentSummaryDto>.Fail("Hóa đơn đang có lượt thanh toán chưa hoàn tất, chưa thể đổi cấu hình.");
                }

                if (request.ChoPhep && request.SoTienToiThieu!.Value > hoaDon.TongTien)
                {
                    return ServiceResult<InvoicePaymentSummaryDto>.Fail("Mức tối thiểu không được vượt tổng tiền hóa đơn.");
                }

                hoaDon.CauHinhThanhToanMotPhan(request.ChoPhep, request.ChoPhep ? request.SoTienToiThieu : null, actorId, scope.NowUtc);

                return ServiceResult<InvoicePaymentSummaryDto>.Ok(new InvoicePaymentSummaryDto
                {
                    HoaDonId = hoaDon.HoaDonId,
                    TongTien = hoaDon.TongTien,
                    DaThu = scope.DaThu,
                    ConLai = scope.ConLai,
                    ChoPhepThanhToanMotPhan = hoaDon.ChoPhepThanhToanMotPhan,
                    SoTienThanhToanToiThieu = hoaDon.SoTienThanhToanToiThieu,
                    LaAdmin = true
                }, request.ChoPhep ? "Đã bật thanh toán một phần." : "Đã tắt thanh toán một phần.");
            }, ct);
        }

        public async Task<ServiceResult<InvoicePaymentSummaryDto>> GetInvoicePaymentSummaryAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            var target = await _store.GetTargetByHoaDonAsync(hoaDonId, ct);
            if (target == null)
            {
                return ServiceResult<InvoicePaymentSummaryDto>.NotFound("Không tìm thấy hóa đơn.");
            }

            if (!await _access.CanPerformAsync(actorId, target.ChiNhanhId, EmployeeActionCodes.InvoiceRead, ct))
            {
                return ServiceResult<InvoicePaymentSummaryDto>.Forbidden("Bạn không có quyền xem hóa đơn tại chi nhánh này.");
            }

            var invoice = await _store.GetInvoiceSnapshotAsync(hoaDonId, ct);
            if (invoice == null)
            {
                return ServiceResult<InvoicePaymentSummaryDto>.NotFound("Không tìm thấy hóa đơn.");
            }

            var now = _transaction.UtcNow;
            var active = (await _store.GetRequestSnapshotsAsync(hoaDonId, ct))
                .Select(r => PaymentDtoMapper.ToDto(r, now))
                .FirstOrDefault(PaymentDtoMapper.IsActive);

            return ServiceResult<InvoicePaymentSummaryDto>.Ok(new InvoicePaymentSummaryDto
            {
                HoaDonId = invoice.HoaDonId,
                TongTien = invoice.TongTien,
                DaThu = invoice.DaThu,
                ConLai = invoice.TongTien - invoice.DaThu,
                ChoPhepThanhToanMotPhan = invoice.ChoPhepThanhToanMotPhan,
                SoTienThanhToanToiThieu = invoice.SoTienThanhToanToiThieu,
                MaLuotHoatDong = active?.MaYeuCau,
                TrangThaiLuotHoatDong = active?.TrangThaiHieuLuc,
                LaAdmin = await IsAdminAsync(actorId, ct)
            });
        }

        private async Task<bool> IsAdminAsync(int actorId, CancellationToken ct)
        {
            return (await _access.GetScopeAsync(actorId, ct))?.IsAdmin == true;
        }

        private static string Vnd(decimal soTien) => InvoicePaymentPolicy.FormatVnd(soTien);
    }
}
