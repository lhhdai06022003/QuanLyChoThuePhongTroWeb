using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeDienNuocStore : IDienNuocStore
    {
        public Dictionary<int, int> RoomBranches { get; set; } = new() { { 101, 1 } };
        public Dictionary<int, string> RoomNames { get; set; } = new() { { 101, "101" } };
        public List<int> LockedRooms { get; } = new();
        public List<DichVuDienNuocCuaPhong> SavedRecords { get; } = new();

        public Task<IReadOnlyList<HopDong>> GetActiveContractsInBranchAsync(int chiNhanhId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HopDong>>(new List<HopDong>());

        public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetCurrentMonthRecordsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(new Dictionary<int, DichVuDienNuocCuaPhong>());

        public Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetPreviousMonthRecordsAsync(IReadOnlyList<int> roomIds, int prevThang, int prevNam, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>>(new Dictionary<int, DichVuDienNuocCuaPhong>());

        public Task<ISet<int>> GetLockedRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default)
            => Task.FromResult<ISet<int>>(new HashSet<int>(LockedRooms));

        public Task<(decimal ChiSoDienMoi, decimal ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default)
            => Task.FromResult((0m, 0m));

        public Task<decimal> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default)
            => Task.FromResult(3500m);

        public Task<IReadOnlyDictionary<int, string>> GetRoomNumbersAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<int, string>>(RoomNames);

        public Task<IReadOnlyDictionary<int, int>> GetRoomBranchIdsAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default)
        {
            var dict = new Dictionary<int, int>();
            foreach (var id in roomIds)
            {
                if (RoomBranches.TryGetValue(id, out var branchId))
                {
                    dict[id] = branchId;
                }
            }
            return Task.FromResult<IReadOnlyDictionary<int, int>>(dict);
        }

        public void UpdateRecord(DichVuDienNuocCuaPhong record)
        {
            SavedRecords.Add(record);
        }

        public Task AddRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default)
        {
            SavedRecords.Add(record);
            return Task.CompletedTask;
        }
    }
}
