using System;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.AiAssistants.DTOs
{
    // Dòng dữ liệu store trả về cho công cụ AI; thời gian giữ nguyên UTC.
    public sealed record AiRoomRow(int PhongTroId, string SoPhong, int TangLau, int ChiNhanhId, string TenChiNhanh, decimal GiaThue);

    public sealed record AiUnpaidInvoiceRow(int HoaDonId, string MaHoaDon, int PhongTroId, string SoPhong, string TenChiNhanh, string KhachThue, int Thang, int Nam, decimal TongTien, decimal DaThu);

    public sealed record AiRevenueTotalRow(decimal TongTien, int SoGiaoDich);

    public sealed record AiBranchRevenueRow(int ChiNhanhId, string TenChiNhanh, decimal TongTien, int SoGiaoDich);

    public sealed record AiExpiringContractRow(string SoPhong, string TenChiNhanh, string KhachThue, DateTime ThoiDiemKetThucUtc);

    public sealed record AiTenantLookupRow(string SoPhong, string TenChiNhanh, string HoVaTen, string SoDienThoai, TrangThaiHopDong TrangThaiHopDong);

    public sealed record AiMeterReadingRow(int PhongTroId, string SoPhong, string TenChiNhanh, int Thang, int Nam, decimal ChiSoDienCu, decimal ChiSoDienMoi, decimal ChiSoNuocCu, decimal ChiSoNuocMoi);

    public sealed record AiTenantContractRow(string MaHopDong, int PhongTroId, string SoPhong, string TenChiNhanh, DateTime ThoiDiemBatDauUtc, DateTime? ThoiDiemKetThucUtc, decimal TienThuePhong, decimal TienCocPhong);
}
