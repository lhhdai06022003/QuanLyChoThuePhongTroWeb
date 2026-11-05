using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services
{
    public interface IThanhLyHopDongService
    {
        Task<ServiceResult<ThanhLyHopDongPreviewDto>> GetPreviewAsync(int actorId, int hopDongId, CancellationToken ct = default);
        Task<ServiceResult<ThanhLyHopDongResult>> ThanhLyAsync(int actorId, ThanhLyHopDongRequest request, CancellationToken ct = default);
        Task<ServiceResult<byte[]>> XuatBienBanAsync(int actorId, int hopDongId, string? ghiChu, CancellationToken ct = default);
    }
}
