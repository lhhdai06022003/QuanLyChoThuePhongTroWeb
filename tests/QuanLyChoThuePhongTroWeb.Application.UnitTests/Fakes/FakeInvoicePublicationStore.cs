using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    /// <summary>
    /// Fake của IInvoicePublicationStore dùng cho test Application (không có DB thật).
    /// Không mô phỏng FOR UPDATE / ChangeTracker.Clear thật (đó là hành vi của Infrastructure);
    /// chỉ đếm số lần đọc và ghi lại các ThongBao đã thêm.
    /// </summary>
    public class FakeInvoicePublicationStore : IInvoicePublicationStore
    {
        public Dictionary<int, InvoicePublicationSnapshot> Snapshots { get; } = new();
        public Dictionary<int, HoaDon> Invoices { get; } = new();
        public Dictionary<int, InvoiceRecipientInfo> Recipients { get; } = new();
        public List<ThongBao> AddedNotifications { get; } = new();

        public int GetSnapshotCalls { get; private set; }
        public int LockInvoiceCalls { get; private set; }
        public int GetRecipientCalls { get; private set; }

        public Task<InvoicePublicationSnapshot?> GetSnapshotAsync(int hoaDonId, CancellationToken ct = default)
        {
            GetSnapshotCalls++;
            Snapshots.TryGetValue(hoaDonId, out var snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<HoaDon?> LockInvoiceAsync(int hoaDonId, CancellationToken ct = default)
        {
            LockInvoiceCalls++;
            Invoices.TryGetValue(hoaDonId, out var hoaDon);
            return Task.FromResult(hoaDon);
        }

        public Task<InvoiceRecipientInfo?> GetRecipientAsync(int hopDongId, CancellationToken ct = default)
        {
            GetRecipientCalls++;
            Recipients.TryGetValue(hopDongId, out var recipient);
            return Task.FromResult<InvoiceRecipientInfo?>(recipient);
        }

        public void AddNotification(ThongBao thongBao)
        {
            if (thongBao.Id == 0)
            {
                thongBao.Id = AddedNotifications.Count + 1;
            }
            AddedNotifications.Add(thongBao);
        }
    }
}
