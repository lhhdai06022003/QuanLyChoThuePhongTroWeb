using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Services
{
    public class UserSessionService : IUserSessionService
    {
        private readonly IUserSessionStore _store;

        public UserSessionService(IUserSessionStore store)
        {
            _store = store;
        }

        public async Task<bool> IsSessionValidAsync(int nguoiDungId, AppRole role, CancellationToken cancellationToken = default)
        {
            if (nguoiDungId <= 0 || !Enum.IsDefined(typeof(AppRole), role))
            {
                return false;
            }

            var snapshot = await _store.GetSessionSnapshotAsync(nguoiDungId, cancellationToken);
            if (snapshot == null || snapshot.IsDeleted || !snapshot.IsActive)
            {
                return false;
            }

            return (int)snapshot.Role == (int)role;
        }
    }
}
