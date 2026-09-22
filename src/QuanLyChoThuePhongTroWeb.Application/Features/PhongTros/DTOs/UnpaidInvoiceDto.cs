using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs
{
    public sealed record UnpaidInvoiceDetailDto
    {
        public string tenDichVu { get; init; } = string.Empty;
        public decimal donGia { get; init; }
        public decimal soLuong { get; init; }
        public string donVi { get; init; } = string.Empty;
        public decimal tongTien { get; init; }
    }

    public sealed record UnpaidInvoiceDto
    {
        public int hoaDonId { get; init; }
        public string maHoaDon { get; init; } = string.Empty;
        public int thang { get; init; }
        public int nam { get; init; }
        public decimal tongTien { get; init; }
        public string tenPhong { get; init; } = string.Empty;
        public string tenNguoiThue { get; init; } = string.Empty;
        public IReadOnlyList<UnpaidInvoiceDetailDto> chiTiets { get; init; } = Array.Empty<UnpaidInvoiceDetailDto>();
    }
}
