using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public interface IHopDongTraCuuService
    {
        Task<ServiceResult<HopDongChiTietDto>> GetChiTietAsync(int actorId, int hopDongId, CancellationToken ct = default);
        Task<ServiceResult<LichSuThueDto>> GetLichSuThueAsync(int actorId, int nguoiThueId, CancellationToken ct = default);
        Task<ServiceResult<NoCuDto>> GetNoCuAsync(int actorId, int nguoiThueId, CancellationToken ct = default);
    }
}
