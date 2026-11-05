using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence
{
    // Store riêng cho thanh lý, gia hạn và tra cứu lịch sử thuê (không đụng IHopDongStore).
    public interface IHopDongLifecycleStore
    {
        // Đọc (AsNoTracking). Hợp đồng chưa xóa; kèm PhongTro (+ChiNhanh), NguoiThue.
        Task<HopDong?> GetHopDongAsync(int hopDongId, CancellationToken ct = default);
        // Khóa FOR UPDATE, entity được track. null nếu đã xóa.
        Task<HopDong?> LockHopDongAsync(int hopDongId, CancellationToken ct = default);
        // Hóa đơn chưa xóa của các hợp đồng, FOR UPDATE ORDER BY HoaDonId, được track.
        Task<IReadOnlyList<HoaDon>> LockInvoicesOfContractsAsync(IReadOnlyCollection<int> hopDongIds, CancellationToken ct = default);
        // Hóa đơn chưa xóa của các hợp đồng (không khóa), dùng cho preview/biên bản.
        Task<IReadOnlyList<HoaDon>> GetInvoicesOfContractsAsync(IReadOnlyCollection<int> hopDongIds, CancellationToken ct = default);
        // HoaDonId → tổng SoTienThanhToan của LichSuThanhToan chưa xóa.
        Task<IReadOnlyDictionary<int, decimal>> GetPaidTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default);
        // HoaDonId → tổng cấn trừ (PhuongThuc = CanTruTienCoc, chưa xóa).
        Task<IReadOnlyDictionary<int, decimal>> GetOffsetTotalsAsync(IReadOnlyCollection<int> hoaDonIds, CancellationToken ct = default);
        // Lượt thanh toán đang hoạt động (YeuCauThanhToanHoaDon.DangHoatDong(nowUtc)), được track.
        Task<IReadOnlyList<YeuCauThanhToanHoaDon>> GetActivePaymentRequestsAsync(IReadOnlyCollection<int> hoaDonIds, DateTime nowUtc, CancellationToken ct = default);
        void AddLedgerEntry(LichSuThanhToan entry);
        // true nếu còn hóa đơn chưa xóa của hợp đồng có (Nam, Thang) >= (nam, thang).
        Task<bool> HasInvoiceFromPeriodAsync(int hopDongId, int thang, int nam, CancellationToken ct = default);
        Task<PhongTro?> GetPhongTroForUpdateAsync(int phongTroId, CancellationToken ct = default);
        Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken ct = default);
        Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int excludeHopDongId, CancellationToken ct = default);
        // Thành viên chưa xóa, NgayChuyenDi == null, được track.
        Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersAsync(int hopDongId, CancellationToken ct = default);
        // Thành viên chưa xóa (mọi trạng thái), kèm NguoiThue, AsNoTracking.
        Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetMembersAsync(int hopDongId, CancellationToken ct = default);
        // DangKyDichVu của phòng có NgayKetThuc == null, được track.
        Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesOfRoomAsync(int phongTroId, CancellationToken ct = default);
        Task<IReadOnlyList<HopDongDieuKhoan>> GetTermsAsync(int hopDongId, CancellationToken ct = default);
        Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermTemplatesByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
        // Số hợp đồng chưa xóa có mã khớp {maGoc}-GH{n}.
        Task<int> CountActiveRenewalsAsync(string maGoc, CancellationToken ct = default);
        // Kể cả hợp đồng đã xóa.
        Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken ct = default);
        // Hợp đồng chưa xóa có MaHopDong == maGoc hoặc StartsWith(maGoc + "-GH"); service lọc lại bằng HopDongCodeChain.BelongsTo.
        Task<IReadOnlyList<HopDong>> GetChainAsync(string maGoc, CancellationToken ct = default);
        Task AddHopDongAsync(HopDong hopDong, CancellationToken ct = default);
        Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken ct = default);
        Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken ct = default);
        // Hợp đồng chưa xóa, khác DaHuy, mà nguoiThueId đứng tên hoặc có dòng thành viên chưa xóa; lọc allowedBranchIds (null = mọi chi nhánh).
        Task<IReadOnlyList<LichSuThueRow>> GetRentalHistoryAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        // Hóa đơn DaGui chưa xóa còn nợ của hợp đồng DaKetThuc chưa xóa do nguoiThueId đứng tên; lọc allowedBranchIds.
        Task<IReadOnlyList<NoCuRow>> GetOldDebtsAsync(int nguoiThueId, IReadOnlyCollection<int>? allowedBranchIds, CancellationToken ct = default);
        // Chưa xóa, AsNoTracking.
        Task<NguoiThue?> GetNguoiThueAsync(int nguoiThueId, CancellationToken ct = default);
    }
}
