using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.YeuCauSuCos.DTOs
{
    public class YeuCauSuCoRes
    {
        public int Id { get; set; }
        public int PhongTroId { get; set; }
        public int NguoiThueId { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public string? HinhAnhUrl { get; set; }
        public AppTrangThaiSuCo TrangThai { get; set; }
        public decimal ChiPhiSuaChua { get; set; }
        public bool CongVaoHoaDon { get; set; }
        public string? LyDoTuChoi { get; set; }
        public string? GhiChuAdmin { get; set; }
        public DateTime NgayGui { get; set; }
        public DateTime? NgayXuLy { get; set; }

        public PhongTroInfo PhongTro { get; set; } = new PhongTroInfo();
        public NguoiThueInfo NguoiThue { get; set; } = new NguoiThueInfo();
    }

    public class PhongTroInfo
    {
        public string SoPhong { get; set; } = string.Empty;
        public ChiNhanhInfo ChiNhanh { get; set; } = new ChiNhanhInfo();
    }

    public class ChiNhanhInfo
    {
        public string TenChiNhanh { get; set; } = string.Empty;
    }

    public class NguoiThueInfo
    {
        public string HoVaTen { get; set; } = string.Empty;
        public string SoDienThoai { get; set; } = string.Empty;
    }

    public class CreateYeuCauSuCoReq
    {
        public int PhongTroId { get; set; }
        public int NguoiThueId { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public string? HinhAnhUrl { get; set; }
        public AppTrangThaiSuCo TrangThai { get; set; } = AppTrangThaiSuCo.ChoTiepNhan;
    }
}
