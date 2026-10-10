using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs
{
    public sealed class ProvisionalInvoiceEmailData
    {
        public string MaHoaDon { get; init; } = string.Empty;
        public string TenNguoiThue { get; init; } = string.Empty;
        public string TenPhong { get; init; } = string.Empty;
        public string TenChiNhanh { get; init; } = string.Empty;
        public string SoDienThoaiChiNhanh { get; init; } = string.Empty;
        public int Thang { get; init; }
        public int Nam { get; init; }
        public decimal TongTien { get; init; }
        public IReadOnlyList<ChiTietHoaDonRes> ChiTietHoaDons { get; init; } = Array.Empty<ChiTietHoaDonRes>();
        public decimal ChiSoDienCu { get; init; }
        public decimal ChiSoDienMoi { get; init; }
        public decimal ChiSoNuocCu { get; init; }
        public decimal ChiSoNuocMoi { get; init; }
        public DateTime HanTuChotUtc { get; init; }
    }
}
