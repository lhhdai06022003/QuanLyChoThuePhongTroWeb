using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.Persistence
{
    public interface IYeuCauSuCoStore
    {
        Task<List<YeuCauSuCo>> GetAllAsync(int? chiNhanhId, TrangThaiSuCo? trangThai, int? soThang = 6, CancellationToken cancellationToken = default);
        Task<List<YeuCauSuCo>> GetByNguoiThueAsync(int nguoiThueId, CancellationToken cancellationToken = default);
        Task<YeuCauSuCo?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        void Add(YeuCauSuCo model);
        void Update(YeuCauSuCo model);
    }
}
