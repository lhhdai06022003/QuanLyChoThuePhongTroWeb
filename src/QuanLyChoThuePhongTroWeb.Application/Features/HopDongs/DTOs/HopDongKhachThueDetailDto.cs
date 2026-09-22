using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public sealed record HopDongThanhVienKhachThueDto
    {
        public string hoVaTen { get; init; } = string.Empty;
        public string soDienThoai { get; init; } = string.Empty;
        public string cccd { get; init; } = string.Empty;
        public string ngayVao { get; init; } = string.Empty;
    }

    public sealed record HopDongDichVuKhachThueDto
    {
        public string tenDichVu { get; init; } = string.Empty;
        public decimal donGia { get; init; }
        public string donVi { get; init; } = string.Empty;
        public decimal soLuong { get; init; }
    }

    public sealed record HopDongKhachThueDetailDto
    {
        public string maHopDong { get; init; } = string.Empty;
        public string thoiDiemBatDau { get; init; } = string.Empty;
        public string thoiDiemKetThuc { get; init; } = string.Empty;
        public decimal tienCocPhong { get; init; }
        public decimal tienThuePhong { get; init; }
        public string? tenNguoiDaiDien { get; init; }
        public string? soDienThoaiDaiDien { get; init; }
        public string? cccdDaiDien { get; init; }
        public IReadOnlyList<HopDongThanhVienKhachThueDto> members { get; init; } = Array.Empty<HopDongThanhVienKhachThueDto>();
        public IReadOnlyList<HopDongDichVuKhachThueDto> services { get; init; } = Array.Empty<HopDongDichVuKhachThueDto>();
    }
}
