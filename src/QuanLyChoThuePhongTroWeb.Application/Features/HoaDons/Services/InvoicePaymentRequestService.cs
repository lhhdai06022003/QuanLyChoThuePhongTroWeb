using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    // Từ spec thanh toán trực tuyến §8 khách không còn tự tạo lượt, hủy lượt hay nộp minh chứng; service chỉ còn đọc ảnh
    // minh chứng của các lượt cũ để khách xem lại trong lịch sử.
    public class InvoicePaymentRequestService : IInvoicePaymentRequestService
    {
        private readonly IInvoicePaymentStore _store;
        private readonly IMeterImageStorageService _storage;
        private readonly ILogger<InvoicePaymentRequestService> _logger;

        public InvoicePaymentRequestService(
            IInvoicePaymentStore store,
            IMeterImageStorageService storage,
            ILogger<InvoicePaymentRequestService> logger)
        {
            _store = store;
            _storage = storage;
            _logger = logger;
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
