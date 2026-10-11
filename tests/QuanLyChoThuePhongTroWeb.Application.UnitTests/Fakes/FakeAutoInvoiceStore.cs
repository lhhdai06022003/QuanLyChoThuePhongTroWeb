using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeAutoInvoiceStore : IAutoInvoiceStore
    {
        // Lần đọc thứ n trả Contexts[n - 1]; hết danh sách thì trả phần tử cuối.
        public List<AutoInvoiceRoomContext?> Contexts { get; } = new();
        // Có khóa (phòng, tháng, năm) thì ưu tiên trả theo khóa, bất kể thứ tự gọi.
        public Dictionary<(int PhongTroId, int Thang, int Nam), AutoInvoiceRoomContext?> ContextsByKey { get; } = new();
        public List<(int PhongTroId, int Thang, int Nam)> RequestedKeys { get; } = new();
        public int? FallbackActorId { get; set; }
        public Exception? ThrowOnGetContext { get; set; }
        public int GetContextCalls { get; private set; }
        public int DiscardCalls { get; private set; }
        public int? LastSoKyTrungBinh { get; private set; }
        public List<string>? Log { get; set; }

        public Task<AutoInvoiceRoomContext?> GetRoomContextAsync(int phongTroId, int thang, int nam, int soKyTrungBinh, CancellationToken ct = default)
        {
            GetContextCalls++;
            LastSoKyTrungBinh = soKyTrungBinh;
            Log?.Add("GetContext");
            if (ThrowOnGetContext != null)
            {
                throw ThrowOnGetContext;
            }

            RequestedKeys.Add((phongTroId, thang, nam));
            if (ContextsByKey.TryGetValue((phongTroId, thang, nam), out var byKey))
            {
                return Task.FromResult(byKey);
            }

            if (Contexts.Count == 0)
            {
                return Task.FromResult<AutoInvoiceRoomContext?>(null);
            }

            var index = Math.Min(GetContextCalls, Contexts.Count) - 1;
            return Task.FromResult(Contexts[index]);
        }

        public Task<int?> FindFallbackActorAsync(int chiNhanhId, CancellationToken ct = default)
        {
            Log?.Add("FindFallbackActor");
            return Task.FromResult(FallbackActorId);
        }

        public List<AutoInvoiceDueItem> DueItems { get; } = new();
        public Dictionary<int, int?> LastSubmitters { get; } = new();
        public Dictionary<int, AutoInvoiceAdjustmentSnapshot> AdjustmentSnapshots { get; } = new();

        public Task<IReadOnlyList<AutoInvoiceDueItem>> GetDueProvisionalInvoicesAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            Log?.Add("GetDue");
            return Task.FromResult<IReadOnlyList<AutoInvoiceDueItem>>(DueItems
                .Where(i => i.HanPhanHoiUtc <= nowUtc)
                .OrderBy(i => i.HanPhanHoiUtc)
                .ThenBy(i => i.HoaDonId)
                .ToList());
        }

        public Task<int?> GetLastProvisionalSubmitterIdAsync(int hoaDonId, CancellationToken ct = default)
        {
            return Task.FromResult(LastSubmitters.TryGetValue(hoaDonId, out var id) ? id : null);
        }

        public Task<AutoInvoiceAdjustmentSnapshot?> GetAdjustmentSnapshotAsync(int hoaDonId, CancellationToken ct = default)
        {
            return Task.FromResult(AdjustmentSnapshots.TryGetValue(hoaDonId, out var snap) ? snap : null);
        }

        public void DiscardPendingChanges()
        {
            DiscardCalls++;
            Log?.Add("Discard");
        }
    }
}
