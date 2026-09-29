using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Persistence
{
    public interface IHopDongStore
    {
        Task<DataTableResponse<HopDongRes>> GetDataTableResponseAsync(HopDongFilterReq request, CancellationToken cancellationToken = default);
        Task<HopDongDetailRes?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<HopDong?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<HopDong?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<string?> GetMaChiNhanhAsync(int chiNhanhId, CancellationToken cancellationToken = default);
        Task<int> CountContractsWithPrefixAsync(string prefix, CancellationToken cancellationToken = default);
        Task<bool> ExistsContractWithCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<bool> IsRoomRentedAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<bool> IsTenantActiveInAnotherContractAsync(int nguoiThueId, int? excludeHopDongId = null, CancellationToken cancellationToken = default);
        Task<bool> IsRoomRentedExcludingContractAsync(int phongTroId, int excludeHopDongId, CancellationToken cancellationToken = default);
        Task<PhongTro?> GetPhongTroByIdAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetOverlappingLivingMemberNamesAsync(IReadOnlyList<int> memberIds, CancellationToken cancellationToken = default);
        Task<NguoiThue?> GetNguoiThueByIdAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<bool> ExistsActiveAccountForTenantAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DieuKhoanMau>> GetActiveTermsByIdsAsync(IReadOnlyList<int> termIds, CancellationToken cancellationToken = default);
        Task<bool> HasInvoicesAsync(int hopDongId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ChiTietThanhVienHopDong>> GetActiveMembersByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<HopDongDieuKhoan>> GetHopDongDieuKhoansByHopDongIdAsync(int hopDongId, CancellationToken cancellationToken = default);
        Task<HopDongPrintRes?> GetPrintDataAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<HopDong>> GetHopDongsByNguoiThueIdAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<HopDongKhachThueDetailDto?> GetChiTietHopDongKhachThueAsync(int id, int nguoiThueId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<HopDong>> GetExpiredActiveContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DangKyDichVu>> GetActiveServicesByRoomIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<HopDong>> GetActiveExpiringContractsAsync(CancellationToken cancellationToken = default);

        Task AddAsync(HopDong hopDong, CancellationToken cancellationToken = default);
        Task AddMembersAsync(IEnumerable<ChiTietThanhVienHopDong> members, CancellationToken cancellationToken = default);
        Task AddTermsAsync(IEnumerable<HopDongDieuKhoan> terms, CancellationToken cancellationToken = default);
        Task AddNguoiDungAsync(NguoiDung nguoiDung, CancellationToken cancellationToken = default);
        void Update(HopDong hopDong);
        void UpdatePhongTro(PhongTro phongTro);
        void RemoveTerms(IEnumerable<HopDongDieuKhoan> terms);
    }
}
