using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;

public interface IGuestRegistrationService
{
    Task<ServiceResult<int>> RegisterAsync(GuestRegistrationRequest request,
        CancellationToken cancellationToken = default);
}
