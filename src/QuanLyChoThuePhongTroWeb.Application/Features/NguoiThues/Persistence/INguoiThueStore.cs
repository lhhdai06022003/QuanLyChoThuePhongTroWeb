using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiThues.Persistence
{
    public interface INguoiThueStore
    {
        Task<IEnumerable<NguoiThue>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<NguoiThue>> GetAvailableAsync(CancellationToken cancellationToken = default);
        Task<NguoiThue?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsDuplicateAsync(string cccd, string soDienThoai, string email, int excludeId = 0, CancellationToken cancellationToken = default);
        Task<bool> IsDaiDienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<bool> IsThanhVienHopDongHoatDongAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SelectOptionDto>> GetDropdownListAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SelectOptionDto>> GetDropdownChuaCoPhongAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SelectOptionDto>> GetDropdownCoHopDongAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<NguoiThueAutocompleteDto>> SearchAutocompleteAsync(string searchTerm, CancellationToken cancellationToken = default);
        void Add(NguoiThue entity);
    }
}
