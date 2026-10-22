using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeUserSessionStore : IUserSessionStore
    {
        public Dictionary<int, UserSessionSnapshotDto> Snapshots { get; } = new();

        public Task<UserSessionSnapshotDto?> GetSessionSnapshotAsync(int nguoiDungId, CancellationToken ct = default)
        {
            Snapshots.TryGetValue(nguoiDungId, out var snapshot);
            return Task.FromResult(snapshot);
        }
    }
}
