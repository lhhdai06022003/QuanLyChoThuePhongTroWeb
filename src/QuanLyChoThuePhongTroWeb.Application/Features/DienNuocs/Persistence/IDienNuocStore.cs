using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence
{
    public interface IDienNuocStore
    {
        Task<IReadOnlyList<HopDong>> GetActiveContractsInBranchAsync(int chiNhanhId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetCurrentMonthRecordsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<int, DichVuDienNuocCuaPhong>> GetPreviousMonthRecordsAsync(IReadOnlyList<int> roomIds, int prevThang, int prevNam, CancellationToken cancellationToken = default);
        Task<ISet<int>> GetLockedRoomIdsAsync(IReadOnlyList<int> roomIds, int thang, int nam, CancellationToken cancellationToken = default);
        Task<(decimal ChiSoDienMoi, decimal ChiSoNuocMoi)> GetNearestPreviousReadingAsync(int phongTroId, int thang, int nam, CancellationToken cancellationToken = default);
        Task<decimal> GetServicePriceAsync(string serviceKeyword, int chiNhanhId, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<int, string>> GetRoomNumbersAsync(IReadOnlyList<int> roomIds, CancellationToken cancellationToken = default);

        Task AddRecordAsync(DichVuDienNuocCuaPhong record, CancellationToken cancellationToken = default);
        void UpdateRecord(DichVuDienNuocCuaPhong record);
    }
}
