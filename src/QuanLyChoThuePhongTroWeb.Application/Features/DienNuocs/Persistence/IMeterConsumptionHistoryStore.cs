using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence
{
    public sealed record MeterConsumption(decimal Dien, decimal Nuoc);

    // CacKyTronThang: tiêu thụ các kỳ hợp đồng phủ trọn tháng, mới → cũ, tối đa soKyToiDa.
    // SoNgayPhu: số ngày lịch Việt Nam hợp đồng phủ trong tháng đang xét.
    // HopDongId null khi phòng không có đúng một hợp đồng hiệu lực trong tháng (khi đó danh sách rỗng, SoNgayPhu = SoNgayCuaThang).
    public sealed record MeterConsumptionHistory(
        IReadOnlyList<MeterConsumption> CacKyTronThang,
        int SoNgayPhu,
        int SoNgayCuaThang,
        int? HopDongId);

    public interface IMeterConsumptionHistoryStore
    {
        Task<MeterConsumptionHistory> GetAsync(int phongTroId, int thang, int nam, int soKyToiDa, CancellationToken ct = default);
    }
}
