using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public class MeterReadingWorkflowService : IMeterReadingWorkflowService
    {
        private readonly IMeterImageStore _store;
        private readonly IMeterImageStorageService _storageService;
        private readonly IMeterOcrService _ocrService;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHoaDonStore _hoaDonStore;
        private readonly IHoaDonCalculatorService _calculatorService;
        private readonly ILogger<MeterReadingWorkflowService> _logger;
        private readonly QuanLyChoThuePhongTroWeb.Application.Common.Configurations.MeterImageOptions _options;
        private readonly TimeProvider _time;

        public MeterReadingWorkflowService(
            IMeterImageStore store,
            IMeterImageStorageService storageService,
            IMeterOcrService ocrService,
            IEmployeeAccessService employeeAccessService,
            IUnitOfWork unitOfWork,
            IHoaDonStore hoaDonStore,
            IHoaDonCalculatorService calculatorService,
            ILogger<MeterReadingWorkflowService> logger,
            QuanLyChoThuePhongTroWeb.Application.Common.Configurations.MeterImageOptions options,
            TimeProvider? timeProvider = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
            _employeeAccessService = employeeAccessService ?? throw new ArgumentNullException(nameof(employeeAccessService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _hoaDonStore = hoaDonStore ?? throw new ArgumentNullException(nameof(hoaDonStore));
            _calculatorService = calculatorService ?? throw new ArgumentNullException(nameof(calculatorService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _time = timeProvider ?? TimeProvider.System;
        }

        public async Task<ServiceResult<MeterImageWorkflowResult>> UploadImageAsync(UploadMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Yêu cầu không hợp lệ.");
            }

            if (!Enum.IsDefined(typeof(LoaiDongHo), request.LoaiDongHo))
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Loại đồng hồ không hợp lệ.");
            }

            if (request.PhongTroId <= 0)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Phòng trọ không hợp lệ.");
            }

            if (request.Thang < 1 || request.Thang > 12 || request.Nam < 2000)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Thời gian kỳ không hợp lệ.");
            }

            if (request.File == null || request.File.Length <= 0 || request.File.Content == null || !request.File.Content.CanRead)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Tệp ảnh tải lên không hợp lệ hoặc không thể đọc.");
            }

            if (request.File.Length > _options.MaxFileSizeBytes)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail($"Dung lượng tệp vượt quá giới hạn tối đa cho phép ({_options.MaxFileSizeBytes / 1024 / 1024}MB).");
            }

            var mimeType = request.File.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrEmpty(mimeType) || !_options.AllowedContentTypes.Contains(mimeType))
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Định dạng tệp không được hỗ trợ. Chỉ chấp nhận JPG, PNG, WEBP.");
            }

            if (actorUserId <= 0)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Người thực hiện không hợp lệ.");
            }

            var branchId = await _store.GetBranchIdByRoomAsync(request.PhongTroId, cancellationToken);
            if (!branchId.HasValue)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Phòng trọ không tồn tại hoặc đã bị xóa.");
            }

            // Authorization: Kiểm tra nhân viên/Admin trước
            var isStaffAuthorized = await _employeeAccessService.CanPerformAsync(actorUserId, branchId.Value, EmployeeActionCodes.MeterUpload, cancellationToken);
            if (!isStaffAuthorized)
            {
                // Nếu không phải nhân viên/Admin, kiểm tra xem có phải khách thuê hợp lệ trong kỳ
                var isTenantAuthorized = await _store.HasActiveContractForTenantInPeriodAsync(request.PhongTroId, actorUserId, request.Thang, request.Nam, cancellationToken);
                if (!isTenantAuthorized)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Bạn không có quyền tải ảnh chỉ số cho phòng này.");
                }
            }

            bool laKhachThue = !isStaffAuthorized;

            // Thu thập MeterUploadFacts và kiểm tra sơ bộ ngoài transaction
            var now = _time.GetUtcNow().UtcDateTime;
            var kyHienTai = new MeterPeriod(request.Thang, request.Nam);
            var kyTruoc = MeterPeriodPolicy.Previous(kyHienTai);

            var coKySau = await _store.HasSubsequentPeriodAsync(request.PhongTroId, request.Thang, request.Nam, cancellationToken);
            var prelimPeriod = await _store.GetPeriodRecordAsync(request.PhongTroId, request.Thang, request.Nam, cancellationToken);
            var coHoaDonDaQuaNhap = prelimPeriod != null && await _store.HasLockedInvoiceAsync(prelimPeriod.DichVuDienNuocCuaPhongId, cancellationToken);
            var kyDaDuyet = prelimPeriod?.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet;

            var coHopDongThangTruoc = await _store.HasContractInMonthAsync(request.PhongTroId, kyTruoc.Thang, kyTruoc.Nam, cancellationToken);
            var kyTruocPeriod = await _store.GetPeriodRecordAsync(request.PhongTroId, kyTruoc.Thang, kyTruoc.Nam, cancellationToken);
            var kyTruocDaDuyet = kyTruocPeriod != null && !kyTruocPeriod.IsDeleted && kyTruocPeriod.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet;

            var facts = new MeterUploadFacts(
                Ky: kyHienTai,
                UtcNow: now,
                LaKhachThue: laKhachThue,
                CoKySau: coKySau,
                CoHoaDonDaQuaNhap: coHoaDonDaQuaNhap,
                KyDaDuyet: kyDaDuyet,
                CoHopDongThangTruoc: coHopDongThangTruoc,
                KyTruocDaDuyet: kyTruocDaDuyet);

            var decision = MeterUploadRules.Evaluate(facts);
            if (!decision.DuocPhep)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail(decision.LyDo!);
            }

            // Upload ảnh lên dịch vụ lưu trữ ngoài transaction
            MeterImageUploadResult uploadResult;
            try
            {
                uploadResult = await _storageService.UploadAsync(request.File, "meter_readings", cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải ảnh lên dịch vụ lưu trữ");
                return ServiceResult<MeterImageWorkflowResult>.Fail("Không thể tải ảnh lên dịch vụ lưu trữ. Vui lòng thử lại.");
            }

            // Transaction 1: Khóa phòng, khóa/tạo kỳ và lưu ảnh
            AnhChiSoDongHo image;
            DichVuDienNuocCuaPhong period;

            await using (var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    await _store.LockRoomsAsync(new[] { request.PhongTroId }, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    await tx.RollbackAsync(cancellationToken);
                    await CleanupUploadedStorageAsync(uploadResult.PublicId);
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Phòng trọ không tồn tại hoặc đã bị xóa.");
                }

                var lockedPeriod = await _store.GetPeriodForUpdateAsync(request.PhongTroId, request.Thang, request.Nam, cancellationToken);

                // Kiểm tra lại trong transaction
                if (await _store.HasSubsequentPeriodAsync(request.PhongTroId, request.Thang, request.Nam, cancellationToken))
                {
                    await tx.RollbackAsync(cancellationToken);
                    await CleanupUploadedStorageAsync(uploadResult.PublicId);
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ này đã có kỳ sau, không thể tải thêm ảnh.");
                }

                if (lockedPeriod != null && await _store.HasLockedInvoiceAsync(lockedPeriod.DichVuDienNuocCuaPhongId, cancellationToken))
                {
                    await tx.RollbackAsync(cancellationToken);
                    await CleanupUploadedStorageAsync(uploadResult.PublicId);
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ này đã có hóa đơn đã phát hành, không thể chỉnh sửa.");
                }

                if (laKhachThue && lockedPeriod != null && lockedPeriod.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet)
                {
                    await tx.RollbackAsync(cancellationToken);
                    await CleanupUploadedStorageAsync(uploadResult.PublicId);
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ này đã được chốt, không thể gửi thêm ảnh.");
                }

                if (lockedPeriod == null)
                {
                    period = await CreateDraftPeriodAsync(request.PhongTroId, request.Thang, request.Nam, branchId.Value, actorUserId, cancellationToken);
                }
                else
                {
                    period = lockedPeriod;
                }

                image = new AnhChiSoDongHo
                {
                    DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                    DichVuDienNuocCuaPhong = period,
                    LoaiDongHo = request.LoaiDongHo,
                    Url = uploadResult.Url,
                    PublicId = uploadResult.PublicId,
                    NguoiGuiId = actorUserId,
                    NgayGui = DateTime.UtcNow,
                    TrangThaiXuLy = TrangThaiXuLyAnhChiSo.MoiTaiLen
                };
                image.BatDauXuLy();
                await _store.AddImageAsync(image, cancellationToken);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    await CleanupUploadedStorageAsync(uploadResult.PublicId);
                    throw;
                }
                catch (Exception dbEx)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    await CleanupUploadedStorageAsync(uploadResult.PublicId);
                    _logger.LogError(dbEx, "Lỗi khi lưu thông tin ảnh vào cơ sở dữ liệu");
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Lỗi khi lưu thông tin ảnh vào cơ sở dữ liệu. Vui lòng thử lại.");
                }
            }

            return await ExecuteOcrPhaseAsync(
                image.AnhChiSoDongHoId,
                period.DichVuDienNuocCuaPhongId,
                image.PublicId ?? string.Empty,
                image.Url,
                image.LoaiDongHo,
                image.NgayXuLy!.Value,
                isRetry: false,
                cancellationToken);
        }

        public async Task<ServiceResult<MeterImageWorkflowResult>> RetryOcrAsync(int anhChiSoDongHoId, int actorUserId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (anhChiSoDongHoId <= 0)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh chỉ số không hợp lệ.");
            }

            var accessCtx = await _store.GetImageAccessContextAsync(anhChiSoDongHoId, cancellationToken);
            if (accessCtx == null || accessCtx.IsDeleted)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Không tìm thấy ảnh chỉ số.");
            }

            if (accessCtx.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaXacNhan)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đã được xác nhận. Để sửa số chính thức, dùng nhập tay kèm lý do khi duyệt kỳ.");
            }

            if (accessCtx.DuocChonLamChiSoChinhThuc)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đã là chỉ số chính thức, không thể thử nhận diện lại.");
            }

            var canRetry = await _employeeAccessService.CanPerformAsync(actorUserId, accessCtx.ChiNhanhId, EmployeeActionCodes.MeterRetryOcr, cancellationToken);
            if (!canRetry)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Bạn không có quyền thử lại nhận diện cho ảnh này.");
            }

            DateTime attemptStamp;
            string publicId;
            string url;
            LoaiDongHo loaiDongHo;
            int periodId = accessCtx.DichVuDienNuocCuaPhongId;

            // Pha 1: Khóa trong transaction 1
            await using (var tx1 = await _unitOfWork.BeginTransactionAsync(cancellationToken))
            {
                var period = await _store.GetPeriodByIdForUpdateAsync(periodId, cancellationToken);
                if (period == null || period.IsDeleted)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Không tìm thấy dữ liệu kỳ của ảnh.");
                }

                if (await _store.HasSubsequentPeriodAsync(period.PhongTroId, period.Thang, period.Nam, cancellationToken))
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ chỉ số đã bị khóa bởi kỳ sau.");
                }

                if (await _store.HasLockedInvoiceAsync(period.DichVuDienNuocCuaPhongId, cancellationToken))
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ chỉ số đã có hóa đơn không còn ở trạng thái nháp.");
                }

                var image = await _store.GetImageForUpdateAsync(anhChiSoDongHoId, cancellationToken);
                if (image == null || image.IsDeleted || image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaThayThe)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không còn hiệu lực để thử nhận diện lại.");
                }

                if (image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaXacNhan)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đã được xác nhận. Để sửa số chính thức, dùng nhập tay kèm lý do khi duyệt kỳ.");
                }

                if (image.DuocChonLamChiSoChinhThuc)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đã là chỉ số chính thức, không thể thử nhận diện lại.");
                }

                try
                {
                    image.ThuLaiXuLy(DateTime.UtcNow, TimeSpan.FromMinutes(_options.StaleProcessingMinutes));
                }
                catch (InvalidOperationException ex)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail(ex.Message);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx1.CommitAsync(cancellationToken);

                attemptStamp = image.NgayXuLy!.Value;
                publicId = image.PublicId ?? string.Empty;
                url = image.Url;
                loaiDongHo = image.LoaiDongHo;
            }

            return await ExecuteOcrPhaseAsync(anhChiSoDongHoId, periodId, publicId, url, loaiDongHo, attemptStamp, isRetry: true, cancellationToken);
        }

        private async Task<ServiceResult<MeterImageWorkflowResult>> ExecuteOcrPhaseAsync(
            int imageId,
            int periodId,
            string publicId,
            string url,
            LoaiDongHo loaiDongHo,
            DateTime attemptStamp,
            bool isRetry,
            CancellationToken cancellationToken)
        {
            MeterOcrResult? ocrResult = null;
            string? ocrExecutionError = null;

            // Pha 2: Gọi ngoài không giữ transaction
            try
            {
                var readResult = await _storageService.ReadAsync(publicId, url, cancellationToken);
                ocrResult = await _ocrService.ProcessImageAsync(readResult.Bytes, readResult.ContentType, loaiDongHo, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kỹ thuật khi chạy OCR nhận diện ảnh {ImageId}", imageId);
                ocrExecutionError = "Không thể nhận diện ảnh do lỗi hệ thống. Vui lòng thử lại.";
            }

            // Pha 3: Áp dụng trong transaction 2
            try
            {
                await using var tx2 = await _unitOfWork.BeginTransactionAsync(cancellationToken);

                var period = await _store.GetPeriodByIdForUpdateAsync(periodId, cancellationToken);
                var image = await _store.GetImageForUpdateAsync(imageId, cancellationToken);

                var isStampMatching = image?.NgayXuLy != null &&
                    image.NgayXuLy.Value == attemptStamp;

                if (image == null || image.IsDeleted || image.TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DangXuLy || !isStampMatching)
                {
                    await tx2.RollbackAsync(cancellationToken);
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đã được cập nhật bởi thao tác khác.");
                }

                if (ocrExecutionError != null)
                {
                    image.GhiNhanLoiXuLy(ocrExecutionError);
                }
                else if (ocrResult != null)
                {
                    if (ocrResult.Status == MeterOcrStatus.Readable)
                    {
                        image.GhiNhanKetQuaAI(ocrResult.SuggestedValue, ocrResult.Confidence);
                    }
                    else if (ocrResult.Status == MeterOcrStatus.Unreadable)
                    {
                        image.GhiNhanKetQuaAI(null, null, ocrResult.ErrorMessage);
                    }
                    else
                    {
                        image.GhiNhanLoiXuLy(ocrResult.ErrorMessage ?? (isRetry ? "Thử lại nhận diện thất bại." : "Nhận diện AI thất bại."));
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx2.CommitAsync(cancellationToken);

                var resultDto = MapToWorkflowResult(image, period);
                string message = isRetry
                    ? image.TrangThaiXuLy switch
                    {
                        TrangThaiXuLyAnhChiSo.KhongDocDuoc => "Thử lại hoàn tất nhưng ảnh vẫn không đọc được.",
                        TrangThaiXuLyAnhChiSo.Loi => "Thử lại nhận diện thất bại do lỗi hệ thống.",
                        _ => "Thử lại nhận diện thành công."
                    }
                    : image.TrangThaiXuLy switch
                    {
                        TrangThaiXuLyAnhChiSo.KhongDocDuoc => "Tải ảnh thành công. Không thể đọc rõ chỉ số từ ảnh.",
                        TrangThaiXuLyAnhChiSo.Loi => "Tải ảnh thành công. Nhận diện chỉ số gặp sự cố, vui lòng thử lại hoặc xác nhận thủ công.",
                        _ => "Tải ảnh và nhận diện thành công."
                    };

                return ServiceResult<MeterImageWorkflowResult>.Ok(resultDto, message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Lỗi khi lưu kết quả nhận diện cho ảnh {ImageId}", imageId);
                var currentImg = await _store.GetImageByIdAsync(imageId, CancellationToken.None);
                var currentPeriod = await _store.GetPeriodRecordByIdAsync(periodId, CancellationToken.None);

                if (currentImg == null)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail(
                        $"Lỗi khi lưu kết quả nhận diện ảnh. Vui lòng thử lại sau {_options.StaleProcessingMinutes} phút.");
                }

                var fallbackDto = MapToWorkflowResult(currentImg, currentPeriod);
                return ServiceResult<MeterImageWorkflowResult>.Ok(
                    fallbackDto,
                    $"Ảnh đã được lưu nhưng kết quả nhận diện chưa được ghi. Nhân viên có thể thử nhận diện lại sau {_options.StaleProcessingMinutes} phút.");
            }
        }

        public async Task<ServiceResult<MeterImageWorkflowResult>> ConfirmImageAsync(ConfirmMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Yêu cầu không hợp lệ.");
            }

            if (request.AnhChiSoDongHoId <= 0)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh chỉ số không hợp lệ.");
            }

            if (request.GiaTriXacNhan < 0)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số xác nhận không được âm.");
            }

            var bits = decimal.GetBits(request.GiaTriXacNhan);
            if (((bits[3] >> 16) & 0x7F) > 3)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số xác nhận không được có quá 3 chữ số thập phân.");
            }

            var accessCtx = await _store.GetImageAccessContextAsync(request.AnhChiSoDongHoId, cancellationToken);
            if (accessCtx == null || accessCtx.IsDeleted)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không còn hiệu lực để xác nhận.");
            }

            // Authorization
            var canReview = await _employeeAccessService.CanPerformAsync(actorUserId, accessCtx.ChiNhanhId, EmployeeActionCodes.MeterReview, cancellationToken);
            if (!canReview)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Bạn không có quyền xác nhận chỉ số cho chi nhánh này.");
            }

            // Bắt đầu transaction và khóa hàng
            await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // 1. Lock period row
            var period = await _store.GetPeriodByIdForUpdateAsync(accessCtx.DichVuDienNuocCuaPhongId, cancellationToken);
            if (period == null)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Không tìm thấy dữ liệu kỳ của ảnh.");
            }

            // 2. Lock target image row
            var image = await _store.GetImageForUpdateAsync(request.AnhChiSoDongHoId, cancellationToken);
            if (image == null || image.IsDeleted)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không còn hiệu lực để xác nhận.");
            }

            // 3. Lock sibling images cùng kỳ và loại đồng hồ
            var siblings = await _store.GetImagesForUpdateAsync(period.DichVuDienNuocCuaPhongId, image.LoaiDongHo, cancellationToken);

            // 4. Revalidate bên trong transaction
            if (await _store.HasSubsequentPeriodAsync(period.PhongTroId, period.Thang, period.Nam, cancellationToken))
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ này đã có kỳ sau, không thể chỉnh sửa.");
            }

            if (await _store.HasLockedInvoiceAsync(period.DichVuDienNuocCuaPhongId, cancellationToken))
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ này đã có hóa đơn đã phát hành, không thể chỉnh sửa.");
            }

            if (image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.CanChupLai ||
                image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaThayThe ||
                image.IsDeleted)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không còn hiệu lực để xác nhận.");
            }

            if (image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaXacNhan)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đã được xác nhận. Dùng chức năng sửa số đã xác nhận (kèm lý do).");
            }

            if (image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.MoiTaiLen || image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DangXuLy)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh đang được nhận diện, vui lòng chờ hoặc thử nhận diện lại.");
            }

            if ((image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.KhongDocDuoc || image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.Loi) && string.IsNullOrWhiteSpace(request.GhiChu))
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không đọc được hoặc lỗi bắt buộc phải có lý do/ghi chú xác nhận.");
            }

            if (image.LoaiDongHo == LoaiDongHo.Dien && request.GiaTriXacNhan < period.ChiSoDienCu)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
            }

            if (image.LoaiDongHo == LoaiDongHo.Nuoc && request.GiaTriXacNhan < period.ChiSoNuocCu)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
            }

            // Kiểm tra chỉ số của loại còn lại tránh exception khi CapNhatChiSo
            if (image.LoaiDongHo == LoaiDongHo.Dien)
            {
                if (period.ChiSoNuocMoi < period.ChiSoNuocCu)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số nước hiện tại không hợp lệ (nhỏ hơn chỉ số nước cũ).");
                }
            }
            else
            {
                if (period.ChiSoDienMoi < period.ChiSoDienCu)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số điện hiện tại không hợp lệ (nhỏ hơn chỉ số điện cũ).");
                }
            }

            // 5. Bỏ ảnh chính thức cũ bằng Domain method
            foreach (var sibling in siblings)
            {
                if (sibling.DuocChonLamChiSoChinhThuc && sibling.AnhChiSoDongHoId != image.AnhChiSoDongHoId)
                {
                    sibling.HuyChonChinhThuc();
                }
            }

            // 6. Xác nhận ảnh mới
            if (image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.KhongDocDuoc || image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.Loi)
            {
                image.XacNhanThuCong(request.GiaTriXacNhan, actorUserId, request.GhiChu!);
            }
            else if (image.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DocDuoc)
            {
                image.XacNhan(request.GiaTriXacNhan, actorUserId, request.GhiChu);
            }
            else
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không còn hiệu lực để xác nhận.");
            }

            // 7. Cập nhật chỉ số trên fresh locked period (giữ nguyên chỉ số loại còn lại)
            if (image.LoaiDongHo == LoaiDongHo.Dien)
            {
                period.CapNhatChiSo(request.GiaTriXacNhan, period.ChiSoNuocMoi);
            }
            else
            {
                period.CapNhatChiSo(period.ChiSoDienMoi, request.GiaTriXacNhan);
            }

            if (period.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet)
            {
                period.DuyetLai(actorUserId, period.GhiChuDuyet);
            }

            // 8. Tính lại hóa đơn nháp liên kết nếu có
            var linkedInvoices = await _hoaDonStore.GetInvoicesByMeterReadingIdsAsync(new[] { period.DichVuDienNuocCuaPhongId }, cancellationToken);
            RecalculateDraftInvoices(period, linkedInvoices);

            // 9. Cập nhật chỉ số cũ của các kỳ sau còn là bản nháp
            var cascadeError = await CascadeSubsequentPeriodsAsync(period, cancellationToken);
            if (cascadeError != null)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail(cascadeError);
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi tranh chấp hoặc cơ sở dữ liệu khi xác nhận ảnh chỉ số {ImageId}", request.AnhChiSoDongHoId);
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh chỉ số hoặc kỳ này đang được cập nhật bởi thao tác khác. Vui lòng thử lại.");
            }

            var resultDto = MapToWorkflowResult(image, period);
            return ServiceResult<MeterImageWorkflowResult>.Ok(resultDto, "Xác nhận chỉ số thành công.");
        }

        public async Task<ServiceResult<MeterImageWorkflowResult>> CorrectConfirmedImageAsync(CorrectConfirmedMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Yêu cầu không hợp lệ.");
            }

            if (request.AnhChiSoDongHoId <= 0)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh chỉ số không hợp lệ.");
            }

            if (request.GiaTriMoi < 0)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số xác nhận không được âm.");
            }

            var bits = decimal.GetBits(request.GiaTriMoi);
            if (((bits[3] >> 16) & 0x7F) > 3)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số mới không được có quá 3 chữ số thập phân.");
            }

            if (string.IsNullOrWhiteSpace(request.LyDo))
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Lý do sửa số không được để trống.");
            }

            var trimmedLyDo = request.LyDo.Trim();
            if (trimmedLyDo.Length > 500)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Lý do sửa số không được vượt quá 500 ký tự.");
            }

            var accessCtx = await _store.GetImageAccessContextAsync(request.AnhChiSoDongHoId, cancellationToken);
            if (accessCtx == null || accessCtx.IsDeleted)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không còn hiệu lực để sửa số.");
            }

            // Authorization: nhân viên có quyền MeterReview tại chi nhánh của phòng, hoặc Admin
            var canReview = await _employeeAccessService.CanPerformAsync(actorUserId, accessCtx.ChiNhanhId, EmployeeActionCodes.MeterReview, cancellationToken);
            if (!canReview)
            {
                return ServiceResult<MeterImageWorkflowResult>.Fail("Bạn không có quyền sửa chỉ số cho chi nhánh này.");
            }

            // Bắt đầu transaction và khóa hàng: phòng -> kỳ -> ảnh
            await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            DichVuDienNuocCuaPhong? period;
            AnhChiSoDongHo? image;

            try
            {
                // 1. Lock period row
                period = await _store.GetPeriodByIdForUpdateAsync(accessCtx.DichVuDienNuocCuaPhongId, cancellationToken);
                if (period == null || period.IsDeleted)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Không tìm thấy dữ liệu kỳ của ảnh.");
                }

                // 2. Lock target image row
                image = await _store.GetImageForUpdateAsync(request.AnhChiSoDongHoId, cancellationToken);
                if (image == null || image.IsDeleted)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh không còn hiệu lực để sửa số.");
                }

                // 3. Revalidate bên trong transaction
                if (image.TrangThaiXuLy != TrangThaiXuLyAnhChiSo.DaXacNhan || !image.DuocChonLamChiSoChinhThuc)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ ảnh đang là chỉ số chính thức mới có thể sửa số.");
                }

                if (image.GiaTriXacNhan.HasValue && request.GiaTriMoi == image.GiaTriXacNhan.Value)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Số mới trùng với số đã xác nhận.");
                }

                if (await _store.HasSubsequentPeriodAsync(period.PhongTroId, period.Thang, period.Nam, cancellationToken))
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ này đã có kỳ sau, không thể chỉnh sửa.");
                }

                if (await _store.HasLockedInvoiceAsync(period.DichVuDienNuocCuaPhongId, cancellationToken))
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Kỳ này đã có hóa đơn không còn ở trạng thái nháp, không thể chỉnh sửa.");
                }

                if (image.LoaiDongHo == LoaiDongHo.Dien && request.GiaTriMoi < period.ChiSoDienCu)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                }

                if (image.LoaiDongHo == LoaiDongHo.Nuoc && request.GiaTriMoi < period.ChiSoNuocCu)
                {
                    return ServiceResult<MeterImageWorkflowResult>.Fail("Chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
                }

                // 4. Cập nhật ảnh
                image.SuaGiaTriXacNhan(request.GiaTriMoi, actorUserId, trimmedLyDo);

                // 5. Cập nhật kỳ
                if (image.LoaiDongHo == LoaiDongHo.Dien)
                {
                    period.CapNhatChiSo(request.GiaTriMoi, period.ChiSoNuocMoi);
                }
                else
                {
                    period.CapNhatChiSo(period.ChiSoDienMoi, request.GiaTriMoi);
                }

                if (period.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet)
                {
                    period.DuyetLai(actorUserId, period.GhiChuDuyet);
                }

                // 6. Tính lại hóa đơn nháp liên kết
                var linkedInvoices = await _hoaDonStore.GetInvoicesByMeterReadingIdsAsync(new[] { period.DichVuDienNuocCuaPhongId }, cancellationToken);
                RecalculateDraftInvoices(period, linkedInvoices);

                // 6b. Cập nhật chỉ số cũ của các kỳ sau còn là bản nháp
                var cascadeError = await CascadeSubsequentPeriodsAsync(period, cancellationToken);
                if (cascadeError != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return ServiceResult<MeterImageWorkflowResult>.Fail(cascadeError);
                }

                // 7. SaveChanges và Commit
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi tranh chấp hoặc cơ sở dữ liệu khi sửa số ảnh đã xác nhận {ImageId}", request.AnhChiSoDongHoId);
                return ServiceResult<MeterImageWorkflowResult>.Fail("Ảnh chỉ số hoặc kỳ này đang được cập nhật bởi thao tác khác. Vui lòng thử lại.");
            }

            var resultDto = MapToWorkflowResult(image, period);
            return ServiceResult<MeterImageWorkflowResult>.Ok(resultDto, "Sửa chỉ số đã xác nhận thành công.");
        }

        public async Task<ServiceResult<MeterPeriodsApprovalResult>> ApprovePeriodsAsync(ApproveMeterPeriodsRequest request, int actorUserId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null || request.DanhSachPhong == null || request.DanhSachPhong.Count == 0)
            {
                return ServiceResult<MeterPeriodsApprovalResult>.Fail("Danh sách phòng chốt chỉ số không được để trống.");
            }

            if (request.Thang < 1 || request.Thang > 12 || request.Nam < 2000)
            {
                return ServiceResult<MeterPeriodsApprovalResult>.Fail("Thời gian kỳ không hợp lệ.");
            }

            // 0. Validate trùng phòng trong lô
            var duplicateRoom = request.DanhSachPhong
                .GroupBy(p => p.PhongTroId)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateRoom != null)
            {
                return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {duplicateRoom.Key} xuất hiện nhiều lần trong yêu cầu.");
            }

            // 1. Validate sơ bộ từng item
            foreach (var item in request.DanhSachPhong)
            {
                if (item.PhongTroId <= 0)
                {
                    return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng trọ ID {item.PhongTroId} không hợp lệ.");
                }

                if (!Enum.IsDefined(typeof(MeterReadingSubmissionMode), item.DienMode))
                {
                    return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Chế độ gửi chỉ số điện phòng {item.PhongTroId} không hợp lệ.");
                }

                if (!Enum.IsDefined(typeof(MeterReadingSubmissionMode), item.NuocMode))
                {
                    return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Chế độ gửi chỉ số nước phòng {item.PhongTroId} không hợp lệ.");
                }

                if (item.DienMode == MeterReadingSubmissionMode.Manual)
                {
                    if (!item.ChiSoDienMoiThuCong.HasValue)
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId} nhập tay chỉ số điện bắt buộc phải có giá trị.");
                    }
                    if (string.IsNullOrWhiteSpace(item.LyDoDienThuCong))
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Bắt buộc phải có lý do khi nhập chỉ số điện thủ công (Phòng {item.PhongTroId}).");
                    }
                    if (item.ChiSoDienMoiThuCong.Value < 0)
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Chỉ số điện mới phòng {item.PhongTroId} không được âm.");
                    }
                    var bitsDien = decimal.GetBits(item.ChiSoDienMoiThuCong.Value);
                    if (((bitsDien[3] >> 16) & 0x7F) > 3)
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Chỉ số điện mới phòng {item.PhongTroId} không được có quá 3 chữ số thập phân.");
                    }
                }

                if (item.NuocMode == MeterReadingSubmissionMode.Manual)
                {
                    if (!item.ChiSoNuocMoiThuCong.HasValue)
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId} nhập tay chỉ số nước bắt buộc phải có giá trị.");
                    }
                    if (string.IsNullOrWhiteSpace(item.LyDoNuocThuCong))
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Bắt buộc phải có lý do khi nhập chỉ số nước thủ công (Phòng {item.PhongTroId}).");
                    }
                    if (item.ChiSoNuocMoiThuCong.Value < 0)
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Chỉ số nước mới phòng {item.PhongTroId} không được âm.");
                    }
                    var bitsNuoc = decimal.GetBits(item.ChiSoNuocMoiThuCong.Value);
                    if (((bitsNuoc[3] >> 16) & 0x7F) > 3)
                    {
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Chỉ số nước mới phòng {item.PhongTroId} không được có quá 3 chữ số thập phân.");
                    }
                }
            }

            // 2. Resolve room và branch
            var distinctRoomIds = request.DanhSachPhong.Select(p => p.PhongTroId).Distinct().OrderBy(id => id).ToList();
            int? commonBranchId = null;

            foreach (var roomId in distinctRoomIds)
            {
                var branchId = await _store.GetBranchIdByRoomAsync(roomId, cancellationToken);
                if (!branchId.HasValue)
                {
                    return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng trọ {roomId} không tồn tại hoặc đã bị xóa.");
                }

                if (!commonBranchId.HasValue)
                {
                    commonBranchId = branchId.Value;
                }
                else if (commonBranchId.Value != branchId.Value)
                {
                    return ServiceResult<MeterPeriodsApprovalResult>.Fail("Danh sách phòng không thuộc cùng một chi nhánh.");
                }
            }

            // Authorization
            var canReview = await _employeeAccessService.CanPerformAsync(actorUserId, commonBranchId!.Value, EmployeeActionCodes.MeterReview, cancellationToken);
            if (!canReview)
            {
                return ServiceResult<MeterPeriodsApprovalResult>.Fail("Bạn không có quyền duyệt chỉ số cho chi nhánh này.");
            }

            // 3. Begin transaction cho toàn batch
            await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // Lock tất cả các phòng theo RoomId tăng dần để serialize tạo kỳ mới và tránh deadlock
                await _store.LockRoomsAsync(distinctRoomIds, cancellationToken);

                var results = new List<MeterPeriodApprovalResult>();
                var approvedPeriods = new List<DichVuDienNuocCuaPhong>();

                // Sắp xếp items theo RoomId tăng dần
                var orderedItems = request.DanhSachPhong.OrderBy(p => p.PhongTroId).ToList();

                foreach (var item in orderedItems)
                {
                    var period = await _store.GetPeriodForUpdateAsync(item.PhongTroId, request.Thang, request.Nam, cancellationToken);
                    if (period == null)
                    {
                        period = await CreateDraftPeriodAsync(item.PhongTroId, request.Thang, request.Nam, commonBranchId.Value, actorUserId, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }

                    // Lock sibling images
                    var dienImages = await _store.GetImagesForUpdateAsync(period.DichVuDienNuocCuaPhongId, LoaiDongHo.Dien, cancellationToken);
                    var nuocImages = await _store.GetImagesForUpdateAsync(period.DichVuDienNuocCuaPhongId, LoaiDongHo.Nuoc, cancellationToken);

                    // Revalidate trong transaction
                    if (await _store.HasSubsequentPeriodAsync(item.PhongTroId, request.Thang, request.Nam, cancellationToken))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId} kỳ này đã có kỳ sau, không thể duyệt.");
                    }

                    if (await _store.HasLockedInvoiceAsync(period.DichVuDienNuocCuaPhongId, cancellationToken))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId} kỳ này đã có hóa đơn đã phát hành, không thể duyệt.");
                    }

                    // Xử lý Điện
                    decimal finalChiSoDien;
                    MeterReadingEvidenceSource sourceDien;
                    var officialDien = dienImages.FirstOrDefault(a => a.DuocChonLamChiSoChinhThuc && !a.IsDeleted);

                    if (item.DienMode == MeterReadingSubmissionMode.Manual)
                    {
                        var val = item.ChiSoDienMoiThuCong!.Value;
                        if (val < period.ChiSoDienCu)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId}: chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                        }

                        var hadOldOfficialDien = false;
                        foreach (var img in dienImages.Where(a => a.DuocChonLamChiSoChinhThuc))
                        {
                            img.HuyChonChinhThuc();
                            hadOldOfficialDien = true;
                        }
                        if (hadOldOfficialDien)
                        {
                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                        }

                        finalChiSoDien = val;
                        sourceDien = MeterReadingEvidenceSource.NhapThuCong;
                    }
                    else // OfficialImage
                    {
                        if (officialDien == null || !officialDien.GiaTriXacNhan.HasValue)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId} chưa có ảnh điện chính thức hợp lệ.");
                        }

                        if (officialDien.GiaTriXacNhan.Value < period.ChiSoDienCu)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId}: chỉ số điện mới không được nhỏ hơn chỉ số điện cũ.");
                        }

                        finalChiSoDien = officialDien.GiaTriXacNhan.Value;
                        sourceDien = MeterReadingEvidenceSource.AnhXacNhan;
                    }

                    // Xử lý Nước
                    decimal finalChiSoNuoc;
                    MeterReadingEvidenceSource sourceNuoc;
                    var officialNuoc = nuocImages.FirstOrDefault(a => a.DuocChonLamChiSoChinhThuc && !a.IsDeleted);

                    if (item.NuocMode == MeterReadingSubmissionMode.Manual)
                    {
                        var val = item.ChiSoNuocMoiThuCong!.Value;
                        if (val < period.ChiSoNuocCu)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId}: chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
                        }

                        var hadOldOfficialNuoc = false;
                        foreach (var img in nuocImages.Where(a => a.DuocChonLamChiSoChinhThuc))
                        {
                            img.HuyChonChinhThuc();
                            hadOldOfficialNuoc = true;
                        }
                        if (hadOldOfficialNuoc)
                        {
                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                        }

                        finalChiSoNuoc = val;
                        sourceNuoc = MeterReadingEvidenceSource.NhapThuCong;
                    }
                    else // OfficialImage
                    {
                        if (officialNuoc == null || !officialNuoc.GiaTriXacNhan.HasValue)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId} chưa có ảnh nước chính thức hợp lệ.");
                        }

                        if (officialNuoc.GiaTriXacNhan.Value < period.ChiSoNuocCu)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId}: chỉ số nước mới không được nhỏ hơn chỉ số nước cũ.");
                        }

                        finalChiSoNuoc = officialNuoc.GiaTriXacNhan.Value;
                        sourceNuoc = MeterReadingEvidenceSource.AnhXacNhan;
                    }

                    // Cập nhật chỉ số
                    period.CapNhatChiSo(finalChiSoDien, finalChiSoNuoc);

                    // Tổng hợp ghi chú (không lặp lại chuỗi đã có)
                    var currentNote = period.GhiChuDuyet ?? string.Empty;
                    var newNoteParts = new List<string>();

                    if (item.DienMode == MeterReadingSubmissionMode.Manual && !string.IsNullOrWhiteSpace(item.LyDoDienThuCong))
                    {
                        var dienPart = $"[Điện thủ công: {item.LyDoDienThuCong.Trim()}]";
                        if (!currentNote.Contains(dienPart, StringComparison.Ordinal))
                        {
                            newNoteParts.Add(dienPart);
                        }
                    }

                    if (item.NuocMode == MeterReadingSubmissionMode.Manual && !string.IsNullOrWhiteSpace(item.LyDoNuocThuCong))
                    {
                        var nuocPart = $"[Nước thủ công: {item.LyDoNuocThuCong.Trim()}]";
                        if (!currentNote.Contains(nuocPart, StringComparison.Ordinal))
                        {
                            newNoteParts.Add(nuocPart);
                        }
                    }

                    string? fullNote;
                    if (newNoteParts.Count > 0)
                    {
                        fullNote = string.IsNullOrWhiteSpace(currentNote)
                            ? string.Join("; ", newNoteParts)
                            : $"{currentNote}; {string.Join("; ", newNoteParts)}";
                    }
                    else
                    {
                        fullNote = string.IsNullOrWhiteSpace(currentNote) ? null : currentNote;
                    }

                    if (period.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet)
                    {
                        period.DuyetLai(actorUserId, fullNote);
                    }
                    else
                    {
                        if (period.TrangThaiGhiNhan == TrangThaiGhiNhan.Nhap || period.TrangThaiGhiNhan == TrangThaiGhiNhan.TuChoi)
                        {
                            period.GuiDuyet();
                        }
                        period.Duyet(actorUserId, fullNote);
                    }

                    var cascadeError = await CascadeSubsequentPeriodsAsync(period, cancellationToken);
                    if (cascadeError != null)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return ServiceResult<MeterPeriodsApprovalResult>.Fail($"Phòng {item.PhongTroId}: {cascadeError}");
                    }

                    approvedPeriods.Add(period);

                    results.Add(new MeterPeriodApprovalResult
                    {
                        DichVuDienNuocCuaPhongId = period.DichVuDienNuocCuaPhongId,
                        PhongTroId = period.PhongTroId,
                        Thang = period.Thang,
                        Nam = period.Nam,
                        TrangThaiGhiNhan = period.TrangThaiGhiNhan,
                        ChiSoDienCu = period.ChiSoDienCu,
                        ChiSoDienMoi = period.ChiSoDienMoi,
                        NguonBangChungDien = sourceDien,
                        ChiSoNuocCu = period.ChiSoNuocCu,
                        ChiSoNuocMoi = period.ChiSoNuocMoi,
                        NguonBangChungNuoc = sourceNuoc,
                        NgayDuyet = period.NgayDuyet,
                        NguoiDuyetId = period.NguoiDuyetId,
                        GhiChuDuyet = period.GhiChuDuyet
                    });
                }

                // Tính lại hóa đơn nháp liên kết
                if (approvedPeriods.Count > 0)
                {
                    var periodIds = approvedPeriods.Select(p => p.DichVuDienNuocCuaPhongId).ToArray();
                    var linkedInvoices = await _hoaDonStore.GetInvoicesByMeterReadingIdsAsync(periodIds, cancellationToken);
                    foreach (var p in approvedPeriods)
                    {
                        var pInvoices = linkedInvoices.Where(inv => inv.DichVuDienNuocCuaPhongId == p.DichVuDienNuocCuaPhongId).ToList();
                        RecalculateDraftInvoices(p, pInvoices);
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return ServiceResult<MeterPeriodsApprovalResult>.Ok(new MeterPeriodsApprovalResult { Items = results }, "Duyệt chỉ số các phòng thành công.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogWarning(ex, "Lỗi nghiệp vụ khi thực hiện duyệt batch chỉ số điện nước kỳ {Thang}/{Nam}: {Message}", request.Thang, request.Nam, ex.Message);
                return ServiceResult<MeterPeriodsApprovalResult>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện duyệt batch chỉ số điện nước kỳ {Thang}/{Nam}", request.Thang, request.Nam);
                return ServiceResult<MeterPeriodsApprovalResult>.Fail("Đã có lỗi xảy ra trong quá trình duyệt chỉ số. Toàn bộ thao tác đã được hủy bỏ.");
            }
        }

        /// <summary>
        /// Sau khi chỉ số mới của <paramref name="period"/> thay đổi, cập nhật chỉ số cũ của các kỳ sau còn là bản nháp
        /// (kỳ sau đã duyệt hoặc có hóa đơn phát hành đã khóa kỳ này từ trước nên không tới đây).
        /// Kỳ sau chưa có số riêng (mới == cũ) thì mới cũng dịch theo. Kỳ sau đã có số mới nhỏ hơn số cũ mới thì trả về thông báo lỗi.
        /// </summary>
        private async Task<string?> CascadeSubsequentPeriodsAsync(DichVuDienNuocCuaPhong period, CancellationToken cancellationToken)
        {
            var later = await _store.GetSubsequentPeriodsForUpdateAsync(period.PhongTroId, period.Thang, period.Nam, cancellationToken);
            if (later.Count == 0) return null;

            decimal dien = period.ChiSoDienMoi;
            decimal nuoc = period.ChiSoNuocMoi;
            var changed = new List<DichVuDienNuocCuaPhong>();

            foreach (var next in later.OrderBy(p => p.Nam).ThenBy(p => p.Thang))
            {
                var touched = false;

                if (next.ChiSoDienCu != dien)
                {
                    var placeholder = next.ChiSoDienMoi == next.ChiSoDienCu;
                    if (!placeholder && next.ChiSoDienMoi < dien)
                    {
                        return $"Kỳ {next.Thang:00}/{next.Nam} đang có chỉ số điện mới ({next.ChiSoDienMoi}) nhỏ hơn chỉ số điện này ({dien}). Hãy sửa kỳ {next.Thang:00}/{next.Nam} trước.";
                    }

                    next.ChiSoDienCu = dien;
                    if (placeholder) next.ChiSoDienMoi = dien;
                    touched = true;
                }

                if (next.ChiSoNuocCu != nuoc)
                {
                    var placeholder = next.ChiSoNuocMoi == next.ChiSoNuocCu;
                    if (!placeholder && next.ChiSoNuocMoi < nuoc)
                    {
                        return $"Kỳ {next.Thang:00}/{next.Nam} đang có chỉ số nước mới ({next.ChiSoNuocMoi}) nhỏ hơn chỉ số nước này ({nuoc}). Hãy sửa kỳ {next.Thang:00}/{next.Nam} trước.";
                    }

                    next.ChiSoNuocCu = nuoc;
                    if (placeholder) next.ChiSoNuocMoi = nuoc;
                    touched = true;
                }

                if (touched)
                {
                    next.NgayCapNhat = DateTime.UtcNow;
                    changed.Add(next);
                }

                dien = next.ChiSoDienMoi;
                nuoc = next.ChiSoNuocMoi;
            }

            if (changed.Count > 0)
            {
                var ids = changed.Select(p => p.DichVuDienNuocCuaPhongId).ToArray();
                var invoices = await _hoaDonStore.GetInvoicesByMeterReadingIdsAsync(ids, cancellationToken);
                foreach (var next in changed)
                {
                    RecalculateDraftInvoices(next, invoices.Where(i => i.DichVuDienNuocCuaPhongId == next.DichVuDienNuocCuaPhongId).ToList());
                }
            }

            return null;
        }

        private void RecalculateDraftInvoices(DichVuDienNuocCuaPhong period, IReadOnlyList<HoaDon> invoices)
        {
            if (invoices == null || invoices.Count == 0) return;

            int daysInMonth = DateTime.DaysInMonth(period.Nam, period.Thang);
            foreach (var inv in invoices)
            {
                if (inv.TrangThaiPhatHanh != TrangThaiPhatHanhHoaDon.Nhap) continue;

                int soNgayO = daysInMonth;
                if (inv.HopDong != null)
                {
                    var tienPhong = _calculatorService.TinhTienPhong(
                        inv.HopDong.TienThuePhong,
                        inv.HopDong.ThoiDiemBatDau,
                        inv.HopDong.ThoiDiemKetThuc,
                        period.Thang,
                        period.Nam);
                    soNgayO = tienPhong.SoNgayO;
                }

                var dienData = _calculatorService.TinhTienDienNuoc(period.ChiSoDienMoi, period.ChiSoDienCu, period.DonGiaDien, "Tiền điện", soNgayO, daysInMonth);
                var nuocData = _calculatorService.TinhTienDienNuoc(period.ChiSoNuocMoi, period.ChiSoNuocCu, period.DonGiaNuoc, "Tiền nước", soNgayO, daysInMonth);

                var dienCt = inv.ChiTietHoaDonDichVus.FirstOrDefault(x => !x.IsDeleted && (x.DichVu?.LoaiDichVu == LoaiDichVu.Dien || (x.DichVu == null && (x.TenDichVu.StartsWith("Tiền điện", StringComparison.OrdinalIgnoreCase) || x.TenDichVu.StartsWith("Điện (", StringComparison.OrdinalIgnoreCase)))));
                if (dienCt == null && dienData.SoTien > 0)
                {
                    dienCt = new ChiTietHoaDon
                    {
                        HoaDonId = inv.HoaDonId,
                        HoaDon = inv,
                        TenDichVu = dienData.DienGiai,
                        DonGia = period.DonGiaDien,
                        SoLuong = decimal.Round(dienData.SoLuong, 3, MidpointRounding.AwayFromZero),
                        TongTien = dienData.SoTien
                    };
                    inv.ChiTietHoaDonDichVus.Add(dienCt);
                }
                else if (dienCt != null)
                {
                    dienCt.DonGia = period.DonGiaDien;
                    dienCt.SoLuong = decimal.Round(dienData.SoLuong, 3, MidpointRounding.AwayFromZero);
                    dienCt.TongTien = dienData.SoTien;
                    dienCt.TenDichVu = dienData.DienGiai;
                }

                var nuocCt = inv.ChiTietHoaDonDichVus.FirstOrDefault(x => !x.IsDeleted && (x.DichVu?.LoaiDichVu == LoaiDichVu.Nuoc || (x.DichVu == null && (x.TenDichVu.StartsWith("Tiền nước", StringComparison.OrdinalIgnoreCase) || x.TenDichVu.StartsWith("Nước (", StringComparison.OrdinalIgnoreCase)))));
                if (nuocCt == null && nuocData.SoTien > 0)
                {
                    nuocCt = new ChiTietHoaDon
                    {
                        HoaDonId = inv.HoaDonId,
                        HoaDon = inv,
                        TenDichVu = nuocData.DienGiai,
                        DonGia = period.DonGiaNuoc,
                        SoLuong = decimal.Round(nuocData.SoLuong, 3, MidpointRounding.AwayFromZero),
                        TongTien = nuocData.SoTien
                    };
                    inv.ChiTietHoaDonDichVus.Add(nuocCt);
                }
                else if (nuocCt != null)
                {
                    nuocCt.DonGia = period.DonGiaNuoc;
                    nuocCt.SoLuong = decimal.Round(nuocData.SoLuong, 3, MidpointRounding.AwayFromZero);
                    nuocCt.TongTien = nuocData.SoTien;
                    nuocCt.TenDichVu = nuocData.DienGiai;
                }

                inv.TongTien = inv.ChiTietHoaDonDichVus
                    .Where(x => !x.IsDeleted)
                    .Sum(x => x.TongTien);
                inv.NgayCapNhat = DateTime.UtcNow;

                _hoaDonStore.UpdateHoaDon(inv);
            }
        }

        private async Task<DichVuDienNuocCuaPhong> CreateDraftPeriodAsync(int phongTroId, int thang, int nam, int chiNhanhId, int actorUserId, CancellationToken ct)
        {
            var (prevDien, prevNuoc) = await _store.GetNearestPreviousReadingAsync(phongTroId, thang, nam, ct);
            var donGiaDien = await _store.GetServicePriceAsync("điện", chiNhanhId, ct);
            var donGiaNuoc = await _store.GetServicePriceAsync("nước", chiNhanhId, ct);

            var period = new DichVuDienNuocCuaPhong
            {
                PhongTroId = phongTroId,
                Thang = thang,
                Nam = nam,
                ChiSoDienCu = prevDien,
                ChiSoDienMoi = prevDien,
                DonGiaDien = donGiaDien,
                ChiSoNuocCu = prevNuoc,
                ChiSoNuocMoi = prevNuoc,
                DonGiaNuoc = donGiaNuoc,
                TrangThaiGhiNhan = TrangThaiGhiNhan.Nhap,
                NguoiTaoId = actorUserId,
                NgayTao = DateTime.UtcNow
            };

            await _store.AddPeriodRecordAsync(period, ct);
            return period;
        }

        private async Task CleanupUploadedStorageAsync(string publicId)
        {
            try
            {
                await _storageService.DeleteAsync(publicId, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Không thể dọn dẹp ảnh mồ côi {PublicId}", publicId);
            }
        }

        private static MeterImageWorkflowResult MapToWorkflowResult(AnhChiSoDongHo image, DichVuDienNuocCuaPhong? period)
        {
            return new MeterImageWorkflowResult
            {
                AnhChiSoDongHoId = image.AnhChiSoDongHoId,
                DichVuDienNuocCuaPhongId = period?.DichVuDienNuocCuaPhongId ?? image.DichVuDienNuocCuaPhongId,
                PhongTroId = period?.PhongTroId ?? (image.DichVuDienNuocCuaPhong?.PhongTroId ?? 0),
                Thang = period?.Thang ?? (image.DichVuDienNuocCuaPhong?.Thang ?? 0),
                Nam = period?.Nam ?? (image.DichVuDienNuocCuaPhong?.Nam ?? 0),
                LoaiDongHo = image.LoaiDongHo,
                Url = image.Url,
                TrangThaiXuLy = image.TrangThaiXuLy,
                GiaTriAIGoiY = image.GiaTriAIGoiY,
                GiaTriXacNhan = image.GiaTriXacNhan,
                DoTinCay = image.DoTinCay,
                ThongBaoLoi = image.ThongBaoLoi,
                NgayGui = image.NgayGui,
                NgayXuLy = image.NgayXuLy,
                NgayXacNhan = image.NgayXacNhan,
                DuocChonLamChiSoChinhThuc = image.DuocChonLamChiSoChinhThuc,
                GhiChuXacNhan = image.GhiChuXacNhan
            };
        }
    }
}
