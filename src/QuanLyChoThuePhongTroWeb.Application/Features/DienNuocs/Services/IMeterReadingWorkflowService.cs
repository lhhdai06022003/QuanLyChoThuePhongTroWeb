using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public interface IMeterReadingWorkflowService
    {
        Task<ServiceResult<MeterImageWorkflowResult>> UploadImageAsync(UploadMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default);
        Task<ServiceResult<MeterImageWorkflowResult>> RetryOcrAsync(int anhChiSoDongHoId, int actorUserId, CancellationToken cancellationToken = default);
        Task<ServiceResult<MeterImageWorkflowResult>> ConfirmImageAsync(ConfirmMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default);
        Task<ServiceResult<MeterImageWorkflowResult>> CorrectConfirmedImageAsync(CorrectConfirmedMeterImageRequest request, int actorUserId, CancellationToken cancellationToken = default);
        Task<ServiceResult<MeterPeriodsApprovalResult>> ApprovePeriodsAsync(ApproveMeterPeriodsRequest request, int actorUserId, CancellationToken cancellationToken = default);
        // Đổi chỉ số kỳ đã DaDuyet cho "Điều chỉnh và gửi lại". Chạy trong transaction do caller mở: không Begin/Commit/Rollback, có SaveChanges.
        // Caller phải đã đưa hóa đơn của kỳ về Nhap hoặc hủy, và đã SaveChanges, trước khi gọi.
        Task<ServiceResult<MeterAdjustmentResult>> AdjustReadingsInCurrentTransactionAsync(MeterAdjustmentRequest request, int actorUserId, CancellationToken cancellationToken = default);
    }
}
