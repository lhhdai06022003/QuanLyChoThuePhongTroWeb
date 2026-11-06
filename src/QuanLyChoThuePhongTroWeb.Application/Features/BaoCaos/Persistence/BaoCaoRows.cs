using System;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.BaoCaos.Persistence
{
    public sealed record BaoCaoHoaDonRow(int HoaDonId, int Thang, int Nam, int ChiNhanhId, string TenChiNhanh, decimal TongTien);

    public sealed record BaoCaoThanhToanRow(int HoaDonId, decimal SoTien, PhuongThucThanhToan PhuongThuc, string MaGiaoDich, string GhiChu);

    public sealed record BaoCaoGiuChoRow(
        int YeuCauGiuChoId,
        int ChiNhanhId,
        string TenChiNhanh,
        TrangThaiYeuCauGiuCho TrangThai,
        decimal DaNhan,
        decimal DaApDung,
        bool CoQuyetDinh,
        TrangThaiHoanTien? TrangThaiQuyetDinh,
        DateTime? NgayQuyetDinhUtc,
        decimal DaHoan,
        decimal CocGiuLai = 0m,
        DateTime? NgayGiuLaiUtc = null,
        bool DangGiuChoTuPhucVu = false);

    public sealed record BaoCaoHoaDonNoRow(
        int HoaDonId,
        int Thang,
        int Nam,
        DateTime? HanThanhToanUtc,
        decimal TongTien,
        decimal DaThu,
        int NguoiThueId,
        string HoVaTen,
        string SoPhong,
        int ChiNhanhId,
        string TenChiNhanh,
        bool DaTraPhong);

    public sealed record BaoCaoPhongRow(int PhongTroId, string SoPhong, int ChiNhanhId, string TenChiNhanh, DateTime NgayTaoUtc, TrangThaiPhong TrangThai);

    public sealed record BaoCaoHopDongRow(
        int HopDongId,
        string MaHopDong,
        int PhongTroId,
        int ChiNhanhId,
        DateTime BatDauUtc,
        DateTime? KetThucUtc,
        TrangThaiHopDong TrangThai);

    public sealed record BaoCaoDienNuocRow(
        int PhongTroId,
        string SoPhong,
        int ChiNhanhId,
        string TenChiNhanh,
        int Thang,
        int Nam,
        decimal DienCu,
        decimal DienMoi,
        decimal DonGiaDien,
        decimal NuocCu,
        decimal NuocMoi,
        decimal DonGiaNuoc);
}
