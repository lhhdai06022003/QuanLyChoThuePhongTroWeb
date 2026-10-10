using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuanLyChoThuePhongTroWeb.Application.Features.RoomDepositedNotices;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.Persistence.Features.RoomDepositedNotices;

public sealed class RoomNoticeStore(ApplicationDbContext context) : IRoomNoticeStore
{
    public async Task<RoomNoticePreview?> GetPreviewAsync(int roomId,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var room = await context.PhongTros.AsNoTracking()
            .Where(r => r.PhongTroId == roomId && !r.IsDeleted &&
                !r.ChiNhanh.IsDeleted && context.YeuCauGiuChos.Any(h =>
                    h.PhongTroId == roomId &&
                    (h.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho ||
                     h.TrangThai == TrangThaiYeuCauGiuCho.DaChuyenHopDong)))
            .Select(r => new { r.PhongTroId, r.SoPhong, r.ChiNhanhId,
                r.ChiNhanh.TenChiNhanh })
            .SingleOrDefaultAsync(cancellationToken);
        if (room is null)
            return null;

        var viewings = await context.YeuCauXemPhongs.AsNoTracking()
            .Where(v => v.PhongTroId == roomId && v.Email != null &&
                (v.TrangThai == TrangThaiYeuCauXemPhong.ChoNhanVienXuLy ||
                 v.TrangThai == TrangThaiYeuCauXemPhong.DaXacNhanLich) &&
                (v.ThoiGianXacNhan ?? v.ThoiGianMongMuon) > nowUtc &&
                !context.YeuCauGiuChos.Any(h => h.PhongTroId == roomId &&
                    h.KhachVangLaiId == v.KhachVangLaiId &&
                    (h.TrangThai == TrangThaiYeuCauGiuCho.DangGiuCho ||
                     h.TrangThai == TrangThaiYeuCauGiuCho.DaChuyenHopDong)))
            .Select(v => new { v.HoTen, v.Email })
            .ToListAsync(cancellationToken);
        var holds = await context.YeuCauGiuChos.AsNoTracking()
            .Where(h => h.PhongTroId == roomId &&
                (h.TrangThai == TrangThaiYeuCauGiuCho.MoiTao ||
                 h.TrangThai == TrangThaiYeuCauGiuCho.ChoXacNhanTien ||
                 h.TrangThai == TrangThaiYeuCauGiuCho.ChoThanhToan &&
                    h.HanThanhToan > nowUtc))
            .Select(h => new { h.KhachVangLai.HoTen, h.KhachVangLai.Email })
            .ToListAsync(cancellationToken);

        var recipients = viewings
            .Select(v => new RoomNoticeRecipient(v.HoTen, v.Email!, "Lịch xem"))
            .Concat(holds.Where(h => h.Email != null)
                .Select(h => new RoomNoticeRecipient(h.HoTen, h.Email!, "Giữ chỗ")))
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .GroupBy(r => r.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First() with { Email = group.Key })
            .OrderBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var digestText = roomId + "|" + string.Join("|", recipients
            .Select(r => r.Email.ToUpperInvariant() + ":" + r.Name));
        var digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(digestText)));
        return new RoomNoticePreview(roomId, room.ChiNhanhId, room.SoPhong,
            room.TenChiNhanh, recipients, digest);
    }
}
