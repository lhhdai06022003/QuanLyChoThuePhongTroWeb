using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.Services
{
    public interface IAiAssistantService
    {
        Task<ServiceResult> ChatQuanLyAsync(int actorId, ChatRequest request, CancellationToken cancellationToken = default);
        Task<ServiceResult> ChatKhachThueAsync(int nguoiDungId, int nguoiThueId, ChatRequest request, CancellationToken cancellationToken = default);
    }
}
