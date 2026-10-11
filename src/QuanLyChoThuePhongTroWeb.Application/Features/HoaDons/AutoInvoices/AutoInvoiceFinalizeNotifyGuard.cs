using System.Collections.Concurrent;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.AutoInvoices
{
    // Job tự chốt chạy lặp nên một hóa đơn lỗi kéo dài không được báo nhân viên mỗi vòng:
    // chỉ báo một lần cho mỗi cặp (hóa đơn, lý do) trong một lần chạy ứng dụng (bộ nhớ trong).
    public sealed class AutoInvoiceFinalizeNotifyGuard
    {
        private readonly ConcurrentDictionary<int, string> _reported = new();

        public bool ShouldNotify(int hoaDonId, string lyDo)
        {
            var changed = false;
            _reported.AddOrUpdate(
                hoaDonId,
                _ =>
                {
                    changed = true;
                    return lyDo;
                },
                (_, old) =>
                {
                    if (old == lyDo)
                    {
                        changed = false;
                        return old;
                    }

                    changed = true;
                    return lyDo;
                });
            return changed;
        }

        public void Forget(int hoaDonId) => _reported.TryRemove(hoaDonId, out _);
    }
}
