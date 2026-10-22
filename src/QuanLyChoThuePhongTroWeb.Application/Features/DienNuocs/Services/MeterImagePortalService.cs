using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public class MeterImagePortalService : IMeterImagePortalService
    {
        private readonly IMeterImageQueryStore _queryStore;
        private readonly IMeterImageStore _meterImageStore;
        private readonly IMeterReadingWorkflowService _workflowService;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly MeterImageOptions _options;
        private readonly TimeProvider _time;

        public MeterImagePortalService(
            IMeterImageQueryStore queryStore,
            IMeterImageStore meterImageStore,
            IMeterReadingWorkflowService workflowService,
            IEmployeeAccessService employeeAccessService,
            MeterImageOptions options,
            TimeProvider? timeProvider = null)
        {
            _queryStore = queryStore ?? throw new ArgumentNullException(nameof(queryStore));
            _meterImageStore = meterImageStore ?? throw new ArgumentNullException(nameof(meterImageStore));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _employeeAccessService = employeeAccessService ?? throw new ArgumentNullException(nameof(employeeAccessService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _time = timeProvider ?? TimeProvider.System;
        }

        public async Task<ServiceResult<IReadOnlyList<MeterRoomOptionRes>>> GetTenantRoomsAsync(int actorUserId, int thang, int nam, CancellationToken ct = default)
        {
            if (actorUserId <= 0 || thang < 1 || thang > 12 || nam < 2000)
            {
                return ServiceResult<IReadOnlyList<MeterRoomOptionRes>>.Fail("Tham số không hợp lệ.");
            }

            var utcNow = _time.GetUtcNow().UtcDateTime;
            var ky = new MeterPeriod(thang, nam);
            if (MeterPeriodPolicy.IsFuture(ky, utcNow))
            {
                return ServiceResult<IReadOnlyList<MeterRoomOptionRes>>.Fail("Không thể xem kỳ chưa tới.");
            }

            var (startUtc, endUtc) = MeterPeriodPolicy.MonthRangeUtc(ky);
            var rooms = await _queryStore.GetTenantRoomsAsync(actorUserId, startUtc, endUtc, ct);
            var result = rooms.Select(r => new MeterRoomOptionRes
            {
                PhongTroId = r.PhongTroId,
                TenPhong = r.SoPhong
            }).ToList();

            return ServiceResult<IReadOnlyList<MeterRoomOptionRes>>.Ok(result);
        }

        public async Task<ServiceResult<TenantMeterPeriodRes>> GetTenantPeriodAsync(int actorUserId, int phongTroId, int thang, int nam, CancellationToken ct = default)
        {
            if (actorUserId <= 0 || phongTroId <= 0 || thang < 1 || thang > 12 || nam < 2000)
            {
                return ServiceResult<TenantMeterPeriodRes>.Fail("Tham số không hợp lệ.");
            }

            var utcNow = _time.GetUtcNow().UtcDateTime;
            var ky = new MeterPeriod(thang, nam);
            if (MeterPeriodPolicy.IsFuture(ky, utcNow))
            {
                return ServiceResult<TenantMeterPeriodRes>.Fail("Không thể xem kỳ chưa tới.");
            }

            var isAuthorized = await _meterImageStore.HasActiveContractForTenantInPeriodAsync(phongTroId, actorUserId, thang, nam, ct);
            if (!isAuthorized)
            {
                return ServiceResult<TenantMeterPeriodRes>.Fail("Bạn không có quyền xem ảnh chỉ số của phòng này.");
            }

            var room = await _queryStore.GetRoomAsync(phongTroId, ct);
            if (room == null)
            {
                return ServiceResult<TenantMeterPeriodRes>.Fail("Phòng trọ không tồn tại hoặc đã bị xóa.");
            }

            var period = await _queryStore.GetPeriodAsync(phongTroId, thang, nam, ct);
            IReadOnlyList<MeterImageSnapshot> images = period != null
                ? await _queryStore.GetImagesAsync(period.DichVuDienNuocCuaPhongId, ct)
                : Array.Empty<MeterImageSnapshot>();

            var anhDien = images.Where(i => i.LoaiDongHo == LoaiDongHo.Dien).Select(MapToTenantImageItem).ToList();
            var anhNuoc = images.Where(i => i.LoaiDongHo == LoaiDongHo.Nuoc).Select(MapToTenantImageItem).ToList();

            var kyTruoc = MeterPeriodPolicy.Previous(ky);
            var coKySau = await _meterImageStore.HasSubsequentPeriodAsync(phongTroId, thang, nam, ct);
            var coHoaDonDaQuaNhap = period != null && await _meterImageStore.HasLockedInvoiceAsync(period.DichVuDienNuocCuaPhongId, ct);
            var kyDaDuyet = period?.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet;
            var coHopDongThangTruoc = await _meterImageStore.HasContractInMonthAsync(phongTroId, kyTruoc.Thang, kyTruoc.Nam, ct);
            var kyTruocPeriod = await _meterImageStore.GetPeriodRecordAsync(phongTroId, kyTruoc.Thang, kyTruoc.Nam, ct);
            var kyTruocDaDuyet = kyTruocPeriod != null && !kyTruocPeriod.IsDeleted && kyTruocPeriod.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet;

            var facts = new MeterUploadFacts(
                Ky: ky,
                UtcNow: utcNow,
                LaKhachThue: true,
                CoKySau: coKySau,
                CoHoaDonDaQuaNhap: coHoaDonDaQuaNhap,
                KyDaDuyet: kyDaDuyet,
                CoHopDongThangTruoc: coHopDongThangTruoc,
                KyTruocDaDuyet: kyTruocDaDuyet);

            var uploadDecision = MeterUploadRules.Evaluate(facts);

            return ServiceResult<TenantMeterPeriodRes>.Ok(new TenantMeterPeriodRes
            {
                PhongTroId = phongTroId,
                TenPhong = room.SoPhong,
                Thang = thang,
                Nam = nam,
                AnhDien = anhDien,
                AnhNuoc = anhNuoc,
                CoTheTaiAnh = uploadDecision.DuocPhep,
                LyDoKhongTheTai = uploadDecision.LyDo
            });
        }

        public async Task<ServiceResult<StaffMeterPeriodRes>> GetStaffPeriodAsync(int actorUserId, int phongTroId, int thang, int nam, CancellationToken ct = default)
        {
            if (actorUserId <= 0 || phongTroId <= 0 || thang < 1 || thang > 12 || nam < 2000)
            {
                return ServiceResult<StaffMeterPeriodRes>.Fail("Tham số không hợp lệ.");
            }

            var room = await _queryStore.GetRoomAsync(phongTroId, ct);
            if (room == null)
            {
                return ServiceResult<StaffMeterPeriodRes>.Fail("Phòng trọ không tồn tại hoặc đã bị xóa.");
            }

            var hasReadPermission = await _employeeAccessService.CanPerformAsync(actorUserId, room.ChiNhanhId, EmployeeActionCodes.MeterRead, ct);
            if (!hasReadPermission)
            {
                return ServiceResult<StaffMeterPeriodRes>.Fail("Bạn không có quyền xem ảnh chỉ số của chi nhánh này.");
            }

            var utcNow = _time.GetUtcNow().UtcDateTime;
            var ky = new MeterPeriod(thang, nam);
            var kyTruoc = MeterPeriodPolicy.Previous(ky);

            var period = await _queryStore.GetPeriodAsync(phongTroId, thang, nam, ct);

            bool coBanGhiKy = period != null;
            bool kyDaDuyet = period?.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet;
            decimal dienCu, dienMoi, nuocCu, nuocMoi;

            if (period != null)
            {
                dienCu = period.ChiSoDienCu;
                dienMoi = period.ChiSoDienMoi;
                nuocCu = period.ChiSoNuocCu;
                nuocMoi = period.ChiSoNuocMoi;
            }
            else
            {
                var (prevDien, prevNuoc) = await _meterImageStore.GetNearestPreviousReadingAsync(phongTroId, thang, nam, ct);
                dienCu = prevDien;
                dienMoi = prevDien;
                nuocCu = prevNuoc;
                nuocMoi = prevNuoc;
            }

            var coKySau = await _meterImageStore.HasSubsequentPeriodAsync(phongTroId, thang, nam, ct);
            var coHoaDonDaQuaNhap = period != null && await _meterImageStore.HasLockedInvoiceAsync(period.DichVuDienNuocCuaPhongId, ct);
            bool kyBiKhoa = coKySau || coHoaDonDaQuaNhap;
            string? lyDoKhoa = coKySau
                ? MeterUploadRules.ReasonRule3SubsequentPeriod
                : (coHoaDonDaQuaNhap ? "Kỳ này đã có hóa đơn đã phát hành, không thể chỉnh sửa." : null);

            var coHopDongThangTruoc = await _meterImageStore.HasContractInMonthAsync(phongTroId, kyTruoc.Thang, kyTruoc.Nam, ct);
            var kyTruocPeriod = await _meterImageStore.GetPeriodRecordAsync(phongTroId, kyTruoc.Thang, kyTruoc.Nam, ct);
            var kyTruocDaDuyet = kyTruocPeriod != null && !kyTruocPeriod.IsDeleted && kyTruocPeriod.TrangThaiGhiNhan == TrangThaiGhiNhan.DaDuyet;

            var facts = new MeterUploadFacts(
                Ky: ky,
                UtcNow: utcNow,
                LaKhachThue: false,
                CoKySau: coKySau,
                CoHoaDonDaQuaNhap: coHoaDonDaQuaNhap,
                KyDaDuyet: kyDaDuyet,
                CoHopDongThangTruoc: coHopDongThangTruoc,
                KyTruocDaDuyet: kyTruocDaDuyet);

            var uploadDecision = MeterUploadRules.Evaluate(facts);
            var canUploadStaff = await _employeeAccessService.CanPerformAsync(actorUserId, room.ChiNhanhId, EmployeeActionCodes.MeterUpload, ct);
            bool coTheTaiAnh = uploadDecision.DuocPhep && canUploadStaff;
            string? lyDoKhongTheTai = !uploadDecision.DuocPhep
                ? uploadDecision.LyDo
                : (!canUploadStaff ? "Bạn không có quyền tải ảnh chỉ số." : null);

            var canRetry = await _employeeAccessService.CanPerformAsync(actorUserId, room.ChiNhanhId, EmployeeActionCodes.MeterRetryOcr, ct);
            var canReview = await _employeeAccessService.CanPerformAsync(actorUserId, room.ChiNhanhId, EmployeeActionCodes.MeterReview, ct);

            IReadOnlyList<MeterImageSnapshot> images = period != null
                ? await _queryStore.GetImagesAsync(period.DichVuDienNuocCuaPhongId, ct)
                : Array.Empty<MeterImageSnapshot>();

            var anhDien = images.Where(i => i.LoaiDongHo == LoaiDongHo.Dien)
                .Select(i => MapToStaffImageItem(i, kyBiKhoa, canRetry, canReview, utcNow))
                .ToList();

            var anhNuoc = images.Where(i => i.LoaiDongHo == LoaiDongHo.Nuoc)
                .Select(i => MapToStaffImageItem(i, kyBiKhoa, canRetry, canReview, utcNow))
                .ToList();

            return ServiceResult<StaffMeterPeriodRes>.Ok(new StaffMeterPeriodRes
            {
                PhongTroId = phongTroId,
                TenPhong = room.SoPhong,
                Thang = thang,
                Nam = nam,
                CoBanGhiKy = coBanGhiKy,
                KyDaDuyet = kyDaDuyet,
                ChiSoDienCu = dienCu,
                ChiSoNuocCu = nuocCu,
                ChiSoDienMoi = dienMoi,
                ChiSoNuocMoi = nuocMoi,
                KyBiKhoa = kyBiKhoa,
                LyDoKhoa = lyDoKhoa,
                CoTheTaiAnh = coTheTaiAnh,
                LyDoKhongTheTai = lyDoKhongTheTai,
                AnhDien = anhDien,
                AnhNuoc = anhNuoc
            });
        }

        public async Task<ServiceResult<MeterImageItemRes>> UploadAsync(MeterImageUploadCommand command, int actorUserId, CancellationToken ct = default)
        {
            if (command == null || command.File == null)
            {
                return ServiceResult<MeterImageItemRes>.Fail("Yêu cầu không hợp lệ.");
            }

            if (!Enum.IsDefined(typeof(AppLoaiDongHo), command.LoaiDongHo))
            {
                return ServiceResult<MeterImageItemRes>.Fail("Loại đồng hồ không hợp lệ.");
            }

            var wfReq = new UploadMeterImageRequest
            {
                PhongTroId = command.PhongTroId,
                Thang = command.Thang,
                Nam = command.Nam,
                LoaiDongHo = (LoaiDongHo)command.LoaiDongHo,
                File = command.File
            };

            var wfRes = await _workflowService.UploadImageAsync(wfReq, actorUserId, ct);
            if (!wfRes.Success)
            {
                return ServiceResult<MeterImageItemRes>.Fail(wfRes.Message);
            }

            var branchId = await _meterImageStore.GetBranchIdByRoomAsync(command.PhongTroId, ct);
            var isStaff = branchId.HasValue && await _employeeAccessService.CanPerformAsync(actorUserId, branchId.Value, EmployeeActionCodes.MeterUpload, ct);

            var resItem = isStaff ? MapWorkflowResultToStaffItem(wfRes.Data!) : MapWorkflowResultToTenantItem(wfRes.Data!);
            return ServiceResult<MeterImageItemRes>.Ok(resItem, wfRes.Message);
        }

        public async Task<ServiceResult<MeterImageItemRes>> RetryOcrAsync(int anhChiSoDongHoId, int actorUserId, CancellationToken ct = default)
        {
            var wfRes = await _workflowService.RetryOcrAsync(anhChiSoDongHoId, actorUserId, ct);
            if (!wfRes.Success)
            {
                return ServiceResult<MeterImageItemRes>.Fail(wfRes.Message);
            }

            var resItem = MapWorkflowResultToStaffItem(wfRes.Data!);
            return ServiceResult<MeterImageItemRes>.Ok(resItem, wfRes.Message);
        }

        public async Task<ServiceResult<MeterImageItemRes>> ConfirmAsync(ConfirmMeterImageRequest request, int actorUserId, CancellationToken ct = default)
        {
            var wfRes = await _workflowService.ConfirmImageAsync(request, actorUserId, ct);
            if (!wfRes.Success)
            {
                return ServiceResult<MeterImageItemRes>.Fail(wfRes.Message);
            }

            var resItem = MapWorkflowResultToStaffItem(wfRes.Data!);
            return ServiceResult<MeterImageItemRes>.Ok(resItem, wfRes.Message);
        }

        public async Task<ServiceResult<MeterImageItemRes>> CorrectAsync(CorrectConfirmedMeterImageRequest request, int actorUserId, CancellationToken ct = default)
        {
            var wfRes = await _workflowService.CorrectConfirmedImageAsync(request, actorUserId, ct);
            if (!wfRes.Success)
            {
                return ServiceResult<MeterImageItemRes>.Fail(wfRes.Message);
            }

            var resItem = MapWorkflowResultToStaffItem(wfRes.Data!);
            return ServiceResult<MeterImageItemRes>.Ok(resItem, wfRes.Message);
        }

        private static MeterImageItemRes MapToTenantImageItem(MeterImageSnapshot img)
        {
            return new MeterImageItemRes
            {
                AnhChiSoDongHoId = img.AnhChiSoDongHoId,
                LoaiDongHo = (AppLoaiDongHo)img.LoaiDongHo,
                Url = img.Url,
                TrangThai = (AppTrangThaiAnhChiSo)img.TrangThaiXuLy,
                GiaTriAIGoiY = img.GiaTriAIGoiY,
                GiaTriXacNhan = img.GiaTriXacNhan,
                LaChinhThuc = img.DuocChonLamChiSoChinhThuc,
                NgayGuiUtc = img.NgayGui,
                DoTinCay = null,
                ThongBaoLoi = null,
                GhiChuXacNhan = null,
                NguoiGuiLaKhachThue = false,
                CoTheThuLai = false,
                CoTheXacNhan = false,
                CanLyDoKhiXacNhan = false,
                CoTheSuaSo = false
            };
        }

        private MeterImageItemRes MapToStaffImageItem(MeterImageSnapshot img, bool kyBiKhoa, bool canRetry, bool canReview, DateTime now)
        {
            bool coTheThuLai = false;
            bool coTheXacNhan = false;
            bool canLyDo = false;
            bool coTheSuaSo = false;

            if (!kyBiKhoa)
            {
                var isStale = (img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.MoiTaiLen || img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DangXuLy)
                              && (img.NgayXuLy ?? img.NgayGui) <= now.AddMinutes(-_options.StaleProcessingMinutes);

                coTheThuLai = canRetry && !img.DuocChonLamChiSoChinhThuc && (img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.KhongDocDuoc || img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.Loi || isStale);
                coTheXacNhan = canReview && (img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DocDuoc || img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.KhongDocDuoc || img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.Loi);
                canLyDo = img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.KhongDocDuoc || img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.Loi;
                coTheSuaSo = canReview && img.TrangThaiXuLy == TrangThaiXuLyAnhChiSo.DaXacNhan && img.DuocChonLamChiSoChinhThuc;
            }

            return new MeterImageItemRes
            {
                AnhChiSoDongHoId = img.AnhChiSoDongHoId,
                LoaiDongHo = (AppLoaiDongHo)img.LoaiDongHo,
                Url = img.Url,
                TrangThai = (AppTrangThaiAnhChiSo)img.TrangThaiXuLy,
                GiaTriAIGoiY = img.GiaTriAIGoiY,
                GiaTriXacNhan = img.GiaTriXacNhan,
                LaChinhThuc = img.DuocChonLamChiSoChinhThuc,
                NgayGuiUtc = img.NgayGui,
                DoTinCay = img.DoTinCay,
                ThongBaoLoi = img.ThongBaoLoi,
                GhiChuXacNhan = img.GhiChuXacNhan,
                NguoiGuiLaKhachThue = img.NguoiGuiLaKhachThue,
                CoTheThuLai = coTheThuLai,
                CoTheXacNhan = coTheXacNhan,
                CanLyDoKhiXacNhan = canLyDo,
                CoTheSuaSo = coTheSuaSo
            };
        }

        private static MeterImageItemRes MapWorkflowResultToStaffItem(MeterImageWorkflowResult data)
        {
            return new MeterImageItemRes
            {
                AnhChiSoDongHoId = data.AnhChiSoDongHoId,
                LoaiDongHo = (AppLoaiDongHo)data.LoaiDongHo,
                Url = data.Url,
                TrangThai = (AppTrangThaiAnhChiSo)data.TrangThaiXuLy,
                GiaTriAIGoiY = data.GiaTriAIGoiY,
                GiaTriXacNhan = data.GiaTriXacNhan,
                LaChinhThuc = data.DuocChonLamChiSoChinhThuc,
                NgayGuiUtc = data.NgayGui,
                DoTinCay = data.DoTinCay,
                ThongBaoLoi = data.ThongBaoLoi,
                GhiChuXacNhan = data.GhiChuXacNhan,
                NguoiGuiLaKhachThue = false,
                CoTheThuLai = false,
                CoTheXacNhan = false,
                CanLyDoKhiXacNhan = false,
                CoTheSuaSo = false
            };
        }

        private static MeterImageItemRes MapWorkflowResultToTenantItem(MeterImageWorkflowResult data)
        {
            return new MeterImageItemRes
            {
                AnhChiSoDongHoId = data.AnhChiSoDongHoId,
                LoaiDongHo = (AppLoaiDongHo)data.LoaiDongHo,
                Url = data.Url,
                TrangThai = (AppTrangThaiAnhChiSo)data.TrangThaiXuLy,
                GiaTriAIGoiY = data.GiaTriAIGoiY,
                GiaTriXacNhan = data.GiaTriXacNhan,
                LaChinhThuc = data.DuocChonLamChiSoChinhThuc,
                NgayGuiUtc = data.NgayGui,
                DoTinCay = null,
                ThongBaoLoi = null,
                GhiChuXacNhan = null,
                NguoiGuiLaKhachThue = false,
                CoTheThuLai = false,
                CoTheXacNhan = false,
                CanLyDoKhiXacNhan = false,
                CoTheSuaSo = false
            };
        }
    }
}
