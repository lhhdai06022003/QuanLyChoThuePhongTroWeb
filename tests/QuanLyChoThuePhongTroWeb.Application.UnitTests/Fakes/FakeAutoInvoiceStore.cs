using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeAutoInvoiceStore : IAutoInvoiceStore
    {
        // Lần đọc thứ n trả Contexts[n - 1]; hết danh sách thì trả phần tử cuối.
        public List<AutoInvoiceRoomContext?> Contexts { get; } = new();
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

        public void DiscardPendingChanges()
        {
            DiscardCalls++;
            Log?.Add("Discard");
        }
    }
}
