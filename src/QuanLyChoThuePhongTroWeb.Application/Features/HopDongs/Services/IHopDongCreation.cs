using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;

public interface IHopDongCreation
{
    // The caller checks access, locks the room and owns the transaction.
    Task<ServiceResult<int>> CreateInTransactionAsync(HopDongReq input,
        CancellationToken cancellationToken = default);
}
