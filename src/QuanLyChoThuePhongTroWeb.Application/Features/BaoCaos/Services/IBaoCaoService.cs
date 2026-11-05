using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Services
{
    public interface IBaoCaoService
    {
        Task<ServiceResult<BaoCaoDoanhThuDto>> GetDoanhThuAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default);
        Task<ServiceResult<BaoCaoCongNoDto>> GetCongNoAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default);
        Task<ServiceResult<BaoCaoCongSuatDto>> GetCongSuatPhongAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default);
        Task<ServiceResult<BaoCaoHopDongDto>> GetHopDongAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default);
        Task<ServiceResult<BaoCaoDienNuocDto>> GetDienNuocAsync(int actorId, BaoCaoFilter filter, CancellationToken cancellationToken = default);
        Task<ServiceResult<BaoCaoFileDto>> XuatExcelAsync(int actorId, BaoCaoTab tab, BaoCaoFilter filter, CancellationToken cancellationToken = default);
    }
}
