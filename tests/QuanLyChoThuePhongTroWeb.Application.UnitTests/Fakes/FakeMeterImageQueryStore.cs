using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeMeterImageQueryStore : IMeterImageQueryStore
    {
        public List<MeterRoomRef> Rooms { get; } = new();
        public Dictionary<int, MeterRoomRef> RoomDetails { get; } = new();
        public Dictionary<(int PhongTroId, int Thang, int Nam), MeterPeriodSnapshot> Periods { get; } = new();
        public Dictionary<int, List<MeterImageSnapshot>> Images { get; } = new();

        public Task<IReadOnlyList<MeterRoomRef>> GetTenantRoomsAsync(int tenantUserId, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<MeterRoomRef>>(Rooms);
        }

        public Task<MeterRoomRef?> GetRoomAsync(int phongTroId, CancellationToken ct = default)
        {
            RoomDetails.TryGetValue(phongTroId, out var room);
            return Task.FromResult(room);
        }

        public Task<MeterPeriodSnapshot?> GetPeriodAsync(int phongTroId, int thang, int nam, CancellationToken ct = default)
        {
            Periods.TryGetValue((phongTroId, thang, nam), out var period);
            return Task.FromResult(period);
        }

        public Task<IReadOnlyList<MeterImageSnapshot>> GetImagesAsync(int dichVuDienNuocCuaPhongId, CancellationToken ct = default)
        {
            if (Images.TryGetValue(dichVuDienNuocCuaPhongId, out var list))
            {
                return Task.FromResult<IReadOnlyList<MeterImageSnapshot>>(list);
            }
            return Task.FromResult<IReadOnlyList<MeterImageSnapshot>>(Array.Empty<MeterImageSnapshot>());
        }
    }
}
