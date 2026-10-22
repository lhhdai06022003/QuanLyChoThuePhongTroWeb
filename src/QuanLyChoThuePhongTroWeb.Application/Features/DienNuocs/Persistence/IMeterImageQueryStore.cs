using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence
{
    public sealed record MeterRoomRef(int PhongTroId, string SoPhong, int ChiNhanhId);

    public sealed record MeterPeriodSnapshot(
        int DichVuDienNuocCuaPhongId,
        TrangThaiGhiNhan TrangThaiGhiNhan,
        decimal ChiSoDienCu,
        decimal ChiSoDienMoi,
        decimal ChiSoNuocCu,
        decimal ChiSoNuocMoi);

    public sealed record MeterImageSnapshot(
        int AnhChiSoDongHoId,
        LoaiDongHo LoaiDongHo,
        string Url,
        TrangThaiXuLyAnhChiSo TrangThaiXuLy,
        decimal? GiaTriAIGoiY,
        double? DoTinCay,
        decimal? GiaTriXacNhan,
        bool DuocChonLamChiSoChinhThuc,
        DateTime NgayGui,
        DateTime? NgayXuLy,
        string? ThongBaoLoi,
        string? GhiChuXacNhan,
        bool NguoiGuiLaKhachThue);

    public interface IMeterImageQueryStore
    {
        // Phòng (chưa xóa) mà user khách thuê (IsActive, chưa xóa, role KhachThue, có NguoiThue chưa xóa) là người đại diện
        // hoặc thành viên (chưa xóa) của hợp đồng (chưa xóa, khác DaHuy) giao với [startUtc, endExclusiveUtc). Sắp theo SoPhong.
        Task<IReadOnlyList<MeterRoomRef>> GetTenantRoomsAsync(int tenantUserId, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct = default);
        Task<MeterRoomRef?> GetRoomAsync(int phongTroId, CancellationToken ct = default);
        Task<MeterPeriodSnapshot?> GetPeriodAsync(int phongTroId, int thang, int nam, CancellationToken ct = default);
        // Ảnh chưa xóa của kỳ, sắp NgayGui giảm dần rồi AnhChiSoDongHoId giảm dần.
        Task<IReadOnlyList<MeterImageSnapshot>> GetImagesAsync(int dichVuDienNuocCuaPhongId, CancellationToken ct = default);
    }
}
