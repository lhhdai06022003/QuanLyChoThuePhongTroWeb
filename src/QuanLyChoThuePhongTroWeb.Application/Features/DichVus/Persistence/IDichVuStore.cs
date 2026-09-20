using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DichVus.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DichVus.Persistence
{
    public interface IDichVuStore
    {
        Task<DataTableResponse<DichVuRes>> GetPagedDichVuAsync(DataTableRequest request, CancellationToken cancellationToken = default);
        Task<DichVu?> GetDichVuByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsDichVuNameAsync(string name, int excludeId = 0, CancellationToken cancellationToken = default);
        Task<bool> IsDichVuInUseAsync(int dichVuId, CancellationToken cancellationToken = default);
        void AddDichVu(DichVu entity);
        void UpdateDichVu(DichVu entity);

        Task<DataTableResponse<DichVuChiNhanhRes>> GetPagedDichVuChiNhanhAsync(DataTableRequest request, int chiNhanhId, CancellationToken cancellationToken = default);
        Task<DichVuChiNhanh?> GetDichVuChiNhanhByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsDichVuChiNhanhAsync(int chiNhanhId, int dichVuId, int excludeId = 0, CancellationToken cancellationToken = default);
        void AddDichVuChiNhanh(DichVuChiNhanh entity);
        void UpdateDichVuChiNhanh(DichVuChiNhanh entity);

        Task<PhongTro?> GetPhongTroByIdAsync(int phongTroId, CancellationToken cancellationToken = default);
        Task<List<DichVuChiNhanh>> GetActiveDichVuChiNhanhsAsync(int chiNhanhId, CancellationToken cancellationToken = default);
        Task<List<DangKyDichVu>> GetActiveDangKyDichVusAsync(int phongTroId, CancellationToken cancellationToken = default);
        void AddDangKyDichVu(DangKyDichVu entity);
    }
}
