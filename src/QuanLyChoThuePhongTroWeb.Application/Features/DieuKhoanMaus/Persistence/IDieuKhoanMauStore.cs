using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.DTOs;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.Persistence
{
    public interface IDieuKhoanMauStore
    {
        Task<List<DieuKhoanMauRes>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<DieuKhoanMau?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        void Add(DieuKhoanMau entity);
        void Update(DieuKhoanMau entity);
    }
}
