using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services
{
    public interface IMeterAnomalyCheckService
    {
        Task<ServiceResult<MeterAnomalyCheckResult>> KiemTraAsync(int actorId, MeterAnomalyCheckRequest request, CancellationToken ct = default);
    }
}
