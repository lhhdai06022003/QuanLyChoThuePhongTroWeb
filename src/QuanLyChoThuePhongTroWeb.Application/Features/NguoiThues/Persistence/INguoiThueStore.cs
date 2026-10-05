using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence
{
    // allowedBranchIds: null = không giới hạn (Admin); có giá trị = chỉ người thuê nhìn thấy được với các chi nhánh đó
    // (có hợp đồng tại chi nhánh, kể cả hợp đồng cũ hay ở ghép, hoặc chưa có hợp đồng nào).
    public interface INguoiThueStore
    {
        Task<IEnumerable<NguoiThue>> GetAllAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<NguoiThue>> GetAvailableAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<bool> IsVisibleAsync(int nguoiThueId, IReadOnlyCollection<int> allowedBranchIds, CancellationToken cancellationToken = default);
        Task<NguoiThue?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsDuplicateAsync(string cccd, string soDienThoai, string email, int excludeId = 0, CancellationToken cancellationToken = default);
        Task<bool> IsDaiDienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<bool> IsThanhVienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SelectOptionDto>> GetDropdownListAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SelectOptionDto>> GetDropdownChuaCoPhongAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SelectOptionDto>> GetDropdownCoHopDongAsync(IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(string searchTerm, IReadOnlyCollection<int>? allowedBranchIds = null, CancellationToken cancellationToken = default);
        void Add(NguoiThue entity);
    }
}
