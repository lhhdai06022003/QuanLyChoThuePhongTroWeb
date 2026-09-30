using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence
{
    public interface IMeterImageStore
    {
        Task<int?> GetBranchIdByRoomAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<bool> HasActiveContractForTenantInPeriodAsync(int phongTroId, int tenantUserId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<bool> HasContractInMonthAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<MeterImageAccessContext?> GetImageAccessContextAsync(int imageId, CancellationToken cancellationToken = default);
        Task LockRoomsAsync(IEnumerable<int> roomIds, CancellationToken cancellationToken = default);
        Task<DichVuDienNuocCuaPhong?> GetPeriodRecordAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<DichVuDienNuocCuaPhong?> GetPeriodRecordWithLockAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<DichVuDienNuocCuaPhong?> GetPeriodForUpdateAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<DichVuDienNuocCuaPhong?> GetPeriodRecordByIdAsync(int periodRecordId, CancellationToken cancellationToken = default);
        Task<DichVuDienNuocCuaPhong?> GetPeriodRecordByIdWithLockAsync(int periodRecordId, CancellationToken cancellationToken = default);
        Task<DichVuDienNuocCuaPhong?> GetPeriodByIdForUpdateAsync(int periodRecordId, CancellationToken cancellationToken = default);
        Task<AnhChiSoDongHo?> GetImageByIdAsync(int imageId, CancellationToken cancellationToken = default);
        Task<AnhChiSoDongHo?> GetImageByIdWithLockAsync(int imageId, CancellationToken cancellationToken = default);
        Task<AnhChiSoDongHo?> GetImageForUpdateAsync(int imageId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesByPeriodAndTypeAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AnhChiSoDongHo>> GetImagesForUpdateAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default);
        Task<AnhChiSoDongHo?> GetOfficialImageAsync(int periodRecordId, LoaiDongHo loaiDongHo, CancellationToken cancellationToken = default);
        // True khi có kỳ sau đã khóa kỳ này: kỳ sau đã DaDuyet hoặc đã có hóa đơn khác Nhap/DaHuy.
        // Kỳ sau chỉ là bản nháp (chưa duyệt, chưa có hóa đơn phát hành) không khóa kỳ này.
        Task<bool> HasSubsequentPeriodAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        // Các kỳ sau (chưa xóa) theo thứ tự tăng dần, đã khóa hàng để cập nhật chỉ số cũ dây chuyền.
        Task<IReadOnlyList<DichVuDienNuocCuaPhong>> GetSubsequentPeriodsForUpdateAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<bool> HasNonDraftInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default);
        Task<bool> HasLockedInvoiceAsync(int periodRecordId, CancellationToken cancellationToken = default);
        Task<(decimal ChiSoDienMoi, decimal ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<decimal> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default);
        Task AddPeriodRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default);
        void UpdatePeriodRecord(DichVuDienNuocCuaPhong record);
        Task AddImageAsync(AnhChiSoDongHo image, CancellationToken cancellationToken = default);
        void UpdateImage(AnhChiSoDongHo image);
    }
}
