using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Dashboard.Persistence
{
    // allowedBranchIds: null = không giới hạn (Admin); danh sách = chỉ các chi nhánh đó (rỗng thì không có dữ liệu).
    public interface IDashboardStore
    {
        Task<IReadOnlyList<BranchLookupItemDto>> GetActiveBranchesAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<(int Trong, int DaThue, int BaoTri)> GetRoomStatusCountsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<int> CountActiveContractsAsync(int? branchId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        // Chỉ hóa đơn DaGui, chưa xóa. DaThu: tổng sổ chưa xóa của các hóa đơn đó trong tháng. ChoThu: phần chưa thanh toán đủ.
        Task<IReadOnlyList<DashboardInvoiceStatDto>> GetInvoiceMonthlyStatsAsync(int? branchId, int year, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        // Hóa đơn DaGui, chưa xóa, chưa thanh toán đủ (mọi kỳ), đọc một lần: Chờ thu của kỳ month/year,
        // số phòng đang nợ, quá hạn so với nowUtc.
        Task<DashboardCongNoDto> GetCongNoAsync(int? branchId, int year, int month, DateTime nowUtc, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        // Sổ TienMat chưa xóa có người xác nhận, NgayThanhToan trong [tuUtc, denUtc), gom theo người xác nhận (tên = TenDangNhap).
        // nguoiXacNhanId != null thì chỉ người đó.
        Task<IReadOnlyList<DashboardTienMatNguoiDto>> GetTienMatTheoNguoiAsync(int? branchId, DateTime tuUtc, DateTime denUtc, int? nguoiXacNhanId, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardRecentPaymentDto>> GetRecentPaymentsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DashboardRecentContractDto>> GetRecentContractsAsync(int? branchId, int count = 5, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
    }
}
