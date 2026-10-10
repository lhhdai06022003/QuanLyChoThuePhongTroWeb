using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    public enum AutoInvoiceTrigger
    {
        KhachTaiAnh = 1,
        NhanVienTaiAnh,
        XacNhanAnh,
        SuaSoDaXacNhan,
        ChotKy
    }

    public enum AutoInvoiceReason
    {
        // Dừng im lặng (chỉ ghi log)
        ThieuChiSo = 1,
        DaCoHoaDon,
        KhongCoHopDong,
        // Dừng có thông báo
        DoTinCayThap,
        KhongDocDuoc,
        ChiSoGiam,
        TieuThuBatThuong,
        KyTruocChuaChot,
        KhachKhongCoKenhNhan,
        ChiNhanhKhongCoNhanVien,
        NguoiThucHienThieuQuyen,
        NhieuHopDongTrongKy,
        LoiKhiTao
    }

    public enum AutoInvoiceOutcome
    {
        DaGuiTamTinh = 1,
        DungImLang,
        DungCoThongBao
    }

    public sealed class AutoInvoiceRunResult
    {
        public AutoInvoiceOutcome Outcome { get; init; }
        public IReadOnlyList<AutoInvoiceReason> LyDos { get; init; } = Array.Empty<AutoInvoiceReason>();
        public int? HoaDonId { get; init; }
        public int? NguoiThucHienId { get; init; }
        public DateTime? HanPhanHoiUtc { get; init; }
    }
}
