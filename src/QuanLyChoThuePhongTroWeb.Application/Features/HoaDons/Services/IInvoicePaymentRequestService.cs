using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    // Phía khách thuê (spec §23.1). nguoiThueId lấy từ claim, actorUserId là NguoiDungId để ghi lịch sử.
    // Hóa đơn không thuộc khách trả NotFound để không lộ sự tồn tại.
    public interface IInvoicePaymentRequestService
    {
        Task<ServiceResult<PaymentPanelDto>> GetPaymentPanelAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default);
        Task<ServiceResult<PaymentRequestDto>> CreateAsync(int hoaDonId, int nguoiThueId, int actorUserId, decimal? soTien, CancellationToken ct = default);
        Task<ServiceResult<bool>> CancelAsync(int yeuCauId, int nguoiThueId, int actorUserId, CancellationToken ct = default);
        Task<ServiceResult<PaymentProofDto>> SubmitProofAsync(SubmitPaymentProofRequest request, int nguoiThueId, int actorUserId, CancellationToken ct = default);
        Task<ServiceResult<byte[]>> GetQrAsync(int yeuCauId, int nguoiThueId, CancellationToken ct = default);
        Task<ServiceResult<MeterImageReadResult>> GetProofImageAsync(int minhChungId, int nguoiThueId, CancellationToken ct = default);
    }
}
