using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class FakeMeterConsumptionHistoryStore : IMeterConsumptionHistoryStore
    {
        // Mặc định: không có lịch sử, hợp đồng phủ trọn tháng.
        public MeterConsumptionHistory History { get; set; } =
            new(new List<MeterConsumption>(), 31, 31, 10);

        // Đặt để mô phỏng lỗi khi đọc lịch sử.
        public System.Exception? ThrowOnGet { get; set; }

        public Task<MeterConsumptionHistory> GetAsync(int phongTroId, int thang, int nam, int soKyToiDa, CancellationToken ct = default)
            => ThrowOnGet != null ? Task.FromException<MeterConsumptionHistory>(ThrowOnGet) : Task.FromResult(History);
    }
}
