using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    // Cảnh báo số bất thường khi nhân viên nhập (không chặn, chỉ báo để nhân viên kiểm lại).
    public class MeterAnomalyCheckService : IMeterAnomalyCheckService
    {
        private readonly IMeterImageStore _store;
        private readonly IMeterConsumptionHistoryStore _historyStore;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly AutoInvoiceOptions _options;

        public MeterAnomalyCheckService(
            IMeterImageStore store,
            IMeterConsumptionHistoryStore historyStore,
            IEmployeeAccessService employeeAccessService,
            AutoInvoiceOptions options)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
            _employeeAccessService = employeeAccessService ?? throw new ArgumentNullException(nameof(employeeAccessService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<ServiceResult<MeterAnomalyCheckResult>> KiemTraAsync(int actorId, MeterAnomalyCheckRequest request, CancellationToken ct = default)
        {
            if (actorId <= 0 || request == null || request.PhongTroId <= 0 || request.Thang < 1 || request.Thang > 12 || request.Nam < 2000)
            {
                return ServiceResult<MeterAnomalyCheckResult>.Fail("Dữ liệu không hợp lệ.");
            }

            var branchId = await _store.GetBranchIdByRoomAsync(request.PhongTroId, ct);
            if (branchId == null)
            {
                return ServiceResult<MeterAnomalyCheckResult>.NotFound("Không tìm thấy phòng.");
            }

            if (!await _employeeAccessService.CanPerformAsync(actorId, branchId.Value, EmployeeActionCodes.MeterRead, ct))
            {
                return ServiceResult<MeterAnomalyCheckResult>.Forbidden("Bạn không có quyền xem chỉ số tại chi nhánh này.");
            }

            if (request.ChiSoDienMoi == null && request.ChiSoNuocMoi == null)
            {
                return ServiceResult<MeterAnomalyCheckResult>.Ok(new MeterAnomalyCheckResult());
            }

            decimal dienCu, nuocCu;
            var period = await _store.GetPeriodRecordAsync(request.PhongTroId, request.Thang, request.Nam, ct);
            if (period != null)
            {
                dienCu = period.ChiSoDienCu;
                nuocCu = period.ChiSoNuocCu;
            }
            else
            {
                (dienCu, nuocCu) = await _store.GetNearestPreviousReadingAsync(request.PhongTroId, request.Thang, request.Nam, ct);
            }

            var lichSu = await _historyStore.GetAsync(request.PhongTroId, request.Thang, request.Nam, _options.SoKyLichSuToiDa, ct);

            return ServiceResult<MeterAnomalyCheckResult>.Ok(new MeterAnomalyCheckResult
            {
                Dien = request.ChiSoDienMoi.HasValue ? ToWarning(MeterAnomalyPolicy.Evaluate(LoaiDongHo.Dien, dienCu, request.ChiSoDienMoi.Value, lichSu, _options)) : null,
                Nuoc = request.ChiSoNuocMoi.HasValue ? ToWarning(MeterAnomalyPolicy.Evaluate(LoaiDongHo.Nuoc, nuocCu, request.ChiSoNuocMoi.Value, lichSu, _options)) : null
            });
        }

        private static MeterAnomalyWarning? ToWarning(MeterAnomalyResult? r)
            => r == null ? null : new MeterAnomalyWarning { MaLyDo = r.LyDo.ToString(), MoTa = r.MoTa };
    }
}
