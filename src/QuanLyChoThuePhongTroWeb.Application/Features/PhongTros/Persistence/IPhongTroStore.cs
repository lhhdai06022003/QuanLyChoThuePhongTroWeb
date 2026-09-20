using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.Persistence
{
    public interface IPhongTroStore
    {
        Task<IReadOnlyList<SelectOptionDto>> GetChiNhanhDropdownAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PhongTroListItemDto>> GetDanhSachPhongTroAsync(CancellationToken cancellationToken = default);
        Task<PhongTro?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsSoPhongAsync(string soPhong, int chiNhanhId, int excludeId = 0, CancellationToken cancellationToken = default);
        Task<bool> HasActiveHopDongAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<int> GetActiveMembersCountForActiveContractAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<List<PhongTro>> GetDanhSachPhongTroConTrongAsync(CancellationToken cancellationToken = default);
        Task<List<PhongCardRes>> GetSoDoPhongAsync(int chiNhanhId, CancellationToken cancellationToken = default);
        Task<QuickContractDto?> GetQuickContractAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<UnpaidInvoiceDto?> GetUnpaidInvoiceAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<List<ChiNhanh>> GetAllActiveChiNhanhsAsync(CancellationToken cancellationToken = default);
        Task<List<(int ChiNhanhId, string SoPhong)>> GetAllRoomNumbersAsync(CancellationToken cancellationToken = default);
        void Add(PhongTro phongTro);
        void Update(PhongTro phongTro);
    }
}
