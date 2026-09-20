using System;
using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Features.PhongTros.DTOs
{
    public sealed record QuickContractMemberDto
    {
        public string hoVaTen { get; init; } = string.Empty;
        public string soDienThoai { get; init; } = string.Empty;
        public string cccd { get; init; } = string.Empty;
        public string ngayVao { get; init; } = string.Empty;
    }

    public sealed record QuickContractServiceDto
    {
        public string tenDichVu { get; init; } = string.Empty;
        public double donGia { get; init; }
        public string donVi { get; init; } = string.Empty;
        public double soLuong { get; init; }
    }

    public sealed record QuickContractDto
    {
        public int hopDongId { get; init; }
        public string maHopDong { get; init; } = string.Empty;
        public string thoiDiemBatDau { get; init; } = string.Empty;
        public string thoiDiemKetThuc { get; init; } = string.Empty;
        public double tienCocPhong { get; init; }
        public double tienThuePhong { get; init; }
        public string tenNguoiDaiDien { get; init; } = string.Empty;
        public string soDienThoaiDaiDien { get; init; } = string.Empty;
        public string cccdDaiDien { get; init; } = string.Empty;
        public IReadOnlyList<QuickContractMemberDto> members { get; init; } = Array.Empty<QuickContractMemberDto>();
        public IReadOnlyList<QuickContractServiceDto> services { get; init; } = Array.Empty<QuickContractServiceDto>();
    }
}
