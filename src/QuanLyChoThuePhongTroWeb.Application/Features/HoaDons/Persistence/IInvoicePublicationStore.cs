using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence
{
    public sealed class InvoicePublicationSnapshot
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public int HopDongId { get; init; }
        public int ChiNhanhId { get; init; }
        public TrangThaiPhatHanhHoaDon TrangThaiPhatHanh { get; init; }
        public bool IsDeleted { get; init; }
    }

    public sealed class InvoiceRecipientInfo
    {
        public int NguoiThueId { get; init; }
        public string HoVaTen { get; init; } = string.Empty;
        public string? Email { get; init; }
        public int? NguoiDungId { get; init; }
    }

    public interface IInvoicePublicationStore
    {
        // AsNoTracking, KHÔNG lọc IsDeleted, chi nhánh = HopDong.PhongTro.ChiNhanhId
        Task<InvoicePublicationSnapshot?> GetSnapshotAsync(int hoaDonId, CancellationToken ct = default);

        /// <summary>
        /// Khóa hàng hoa_don bằng SELECT ... FOR UPDATE; entity trả về được tracking, KHÔNG lọc IsDeleted, không Include.
        /// Lưu ý: bản cài đặt gọi ChangeTracker.Clear() trên DbContext dùng chung của scope trước khi khóa,
        /// nên mọi entity đang được tracking (kể cả thay đổi chưa lưu) đều bị bỏ.
        /// Chỉ gọi khi scope không còn thay đổi chưa lưu nào cần giữ.
        /// </summary>
        Task<HoaDon?> LockInvoiceAsync(int hoaDonId, CancellationToken ct = default);

        // Khách đứng tên HopDong.NguoiThueId
        Task<InvoiceRecipientInfo?> GetRecipientAsync(int hopDongId, CancellationToken ct = default);

        void AddNotification(ThongBao thongBao);
    }
}
