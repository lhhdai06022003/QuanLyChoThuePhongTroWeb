using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs
{
    // Một dòng khoản phí trong yêu cầu điều chỉnh. ChiTietHoaDonId rỗng hoặc 0 là dòng mới.
    public sealed class AdjustInvoiceLine
    {
        public int? ChiTietHoaDonId { get; init; }
        public string? TenDichVu { get; init; }
        public decimal DonGia { get; init; }
        public decimal SoLuong { get; init; }
    }

    public sealed class AdjustInvoiceRequest
    {
        public int HoaDonId { get; init; }
        // null = giữ chỉ số hiện tại của kỳ.
        public decimal? ChiSoDienMoi { get; init; }
        public decimal? ChiSoNuocMoi { get; init; }
        // null = không sửa dòng (chỉ tính lại theo chỉ số mới nếu có đổi).
        // Có giá trị = danh sách dòng cuối cùng của hóa đơn, thay toàn bộ dòng hiện có.
        public IReadOnlyList<AdjustInvoiceLine>? ChiTiets { get; init; }
    }

    // Loại dòng để màn điều chỉnh biết dòng nào tính lại được từ chỉ số: "dien", "nuoc" hoặc "khac".
    public sealed class InvoiceAdjustmentLine
    {
        public int ChiTietHoaDonId { get; init; }
        public string TenDichVu { get; init; } = string.Empty;
        public decimal DonGia { get; init; }
        public decimal SoLuong { get; init; }
        public decimal TongTien { get; init; }
        public string Loai { get; init; } = "khac";
        public string DonVi { get; init; } = string.Empty;
    }

    public sealed class InvoiceAdjustmentInfo
    {
        public int HoaDonId { get; init; }
        public string MaHoaDon { get; init; } = string.Empty;
        public bool CoTheDieuChinh { get; init; }
        public string? LyDoKhoa { get; init; }
        public decimal ChiSoDienCu { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocCu { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
        public DateTime? HanPhanHoiUtc { get; init; }
        public decimal TongTien { get; init; }
        public IReadOnlyList<InvoiceAdjustmentLine> ChiTiets { get; init; } = Array.Empty<InvoiceAdjustmentLine>();
    }

    // Dòng điện/nước tính lại theo chỉ số mới (chưa lưu). Null khi thành tiền bằng 0 (hóa đơn tạo mới cũng bỏ dòng này).
    public sealed class InvoiceMeterLinesPreview
    {
        public InvoiceAdjustmentLine? Dien { get; init; }
        public InvoiceAdjustmentLine? Nuoc { get; init; }
        public decimal TieuThuDien { get; init; }
        public decimal TieuThuNuoc { get; init; }
    }

    public sealed class InvoiceAdjustmentResult
    {
        public int HoaDonIdCu { get; init; }
        public int HoaDonIdMoi { get; init; }
        public string MaHoaDonMoi { get; init; } = string.Empty;
        public decimal TongTienMoi { get; init; }
        public DateTime HanPhanHoiUtc { get; init; }
        public bool DaTaoHoaDonThayThe { get; init; }
    }
}
