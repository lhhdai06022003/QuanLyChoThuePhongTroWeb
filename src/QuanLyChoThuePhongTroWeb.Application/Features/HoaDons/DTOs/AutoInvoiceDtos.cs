using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;

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
        LoiKhiTao,
        ThieuDonGia,
        // Thêm ở cuối để giữ số cũ.
        NghiSaiChuSo,
        TieuThuThapBatThuong
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
        // Chỉ có khi RunForRoomAsync được gọi với hoanGuiEmail = true: email tạm tính chưa gửi, caller gửi nền.
        public PendingProvisionalEmail? EmailChoGui { get; init; }
    }
}
