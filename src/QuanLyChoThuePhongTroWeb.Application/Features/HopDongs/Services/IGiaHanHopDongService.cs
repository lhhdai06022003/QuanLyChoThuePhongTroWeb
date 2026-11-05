using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public interface IGiaHanHopDongService
    {
        Task<ServiceResult<GiaHanHopDongFormDto>> GetFormAsync(int actorId, int hopDongId, CancellationToken ct = default);
        Task<ServiceResult<GiaHanHopDongResult>> GiaHanAsync(int actorId, GiaHanHopDongRequest request, CancellationToken ct = default);
    }
}
