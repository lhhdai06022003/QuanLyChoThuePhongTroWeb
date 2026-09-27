using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence
{
    public interface IInvoiceIssuanceStore
    {
        // SELECT ... FROM hop_dong WHERE "HopDongId" = ANY(@ids) ORDER BY "HopDongId" FOR UPDATE; trả về id khóa được
        Task<IReadOnlyList<int>> LockContractsAsync(IReadOnlyList<int> hopDongIds, CancellationToken ct = default);

        // FOR UPDATE, !IsDeleted, Include ChiTietHoaDonDichVus + DichVuDienNuocCuaPhong + HopDong
        Task<HoaDon?> GetInvoiceForUpdateAsync(int hoaDonId, CancellationToken ct = default);

        // Đếm bản DaHuy (IsDeleted = true) cùng hợp đồng/kỳ, để cấp hậu tố -R{n}
        Task<int> CountCancelledInvoicesAsync(int hopDongId, int thang, int nam, CancellationToken ct = default);

        // Có hóa đơn khác (!IsDeleted) cùng hợp đồng/kỳ, không tính excludeHoaDonId
        Task<bool> HasOtherActiveInvoiceAsync(int hopDongId, int thang, int nam, int excludeHoaDonId, CancellationToken ct = default);

        Task AddInvoiceAsync(HoaDon hoaDon, CancellationToken ct = default);

        // D1: sự cố tính phí có NgayXuLy trong [startUtc, endUtc], thuộc các phòng đã cho, DaHoanThanh, CongVaoHoaDon, ChiPhi > 0, !IsDeleted
        Task<IReadOnlyList<YeuCauSuCo>> GetBillableIncidentsInPeriodAsync(IReadOnlyList<int> roomIds, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);

        // D1: hóa đơn đang hoạt động (!IsDeleted) của hợp đồng có phòng/khách/tháng cho trước, FOR UPDATE, Include ChiTiet; null nếu chưa có
        Task<HoaDon?> GetActiveInvoiceForTenantRoomPeriodForUpdateAsync(int phongTroId, int nguoiThueId, int thang, int nam, CancellationToken ct = default);

        // Đọc không khóa: kỳ chỉ số và chi nhánh của hóa đơn, để biết cần khóa kỳ nào trước
        Task<(int? MeterPeriodId, int ChiNhanhId)?> GetInvoiceLockTargetsAsync(int hoaDonId, CancellationToken ct = default);

        // SELECT ... FROM dich_vu_dien_nuoc_cua_phong WHERE id = @id FOR UPDATE (AsNoTracking, chỉ để giữ khóa và đọc trạng thái)
        Task<TrangThaiGhiNhan?> LockMeterPeriodAsync(int meterPeriodId, CancellationToken ct = default);

        // D1 (Task 3b): Tìm hợp đồng đang/đã hiệu lực cho phòng và khách thuê trong các kỳ (tháng/năm)
        Task<IReadOnlyList<int>> GetContractIdsForTenantRoomPeriodsAsync(int phongTroId, int nguoiThueId, IReadOnlyList<(int Thang, int Nam)> periods, CancellationToken ct = default);
    }
}
