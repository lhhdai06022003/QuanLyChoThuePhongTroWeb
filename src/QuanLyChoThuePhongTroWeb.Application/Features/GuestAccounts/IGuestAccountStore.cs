using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GuestAccounts;

public interface IGuestAccountStore
{
    Task<int?> TryCreateAsync(NguoiDung user, KhachVangLai profile,
        CancellationToken cancellationToken = default);
}
