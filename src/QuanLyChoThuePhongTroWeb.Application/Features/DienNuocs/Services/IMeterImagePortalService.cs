using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public interface IMeterImagePortalService
    {
        Task<ServiceResult<IReadOnlyList<MeterRoomOptionRes>>> GetTenantRoomsAsync(int actorUserId, int thang, int nam, CancellationToken ct = default);
        Task<ServiceResult<TenantMeterPeriodRes>> GetTenantPeriodAsync(int actorUserId, int phongTroId, int thang, int nam, CancellationToken ct = default);
        Task<ServiceResult<StaffMeterPeriodRes>> GetStaffPeriodAsync(int actorUserId, int phongTroId, int thang, int nam, CancellationToken ct = default);
        Task<ServiceResult<MeterImageItemRes>> UploadAsync(MeterImageUploadCommand command, int actorUserId, CancellationToken ct = default);
        Task<ServiceResult<MeterImageItemRes>> RetryOcrAsync(int anhChiSoDongHoId, int actorUserId, CancellationToken ct = default);
        Task<ServiceResult<MeterImageItemRes>> ConfirmAsync(ConfirmMeterImageRequest request, int actorUserId, CancellationToken ct = default);
        Task<ServiceResult<MeterImageItemRes>> CorrectAsync(CorrectConfirmedMeterImageRequest request, int actorUserId, CancellationToken ct = default);
    }
}
