using System;
using System.Collections.Generic;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs
{
    public class MeterImageItemRes
    {
        public int AnhChiSoDongHoId { get; set; }
        public AppLoaiDongHo LoaiDongHo { get; set; }
        public string Url { get; set; } = string.Empty;
        public AppTrangThaiAnhChiSo TrangThai { get; set; }
        public decimal? GiaTriAIGoiY { get; set; }
        public decimal? GiaTriXacNhan { get; set; }
        public bool LaChinhThuc { get; set; }
        public DateTime NgayGuiUtc { get; set; }
        // Chỉ điền cho nhân viên/Admin; khách luôn null/false:
        public double? DoTinCay { get; set; }
        public string? ThongBaoLoi { get; set; }
        public string? GhiChuXacNhan { get; set; }
        public bool NguoiGuiLaKhachThue { get; set; }
        public bool CoTheThuLai { get; set; }
        public bool CoTheXacNhan { get; set; }
        public bool CanLyDoKhiXacNhan { get; set; }
        public bool CoTheSuaSo { get; set; }
    }

    public class MeterRoomOptionRes
    {
        public int PhongTroId { get; set; }
        public string TenPhong { get; set; } = string.Empty;
    }

    public class TenantMeterPeriodRes
    {
        public int PhongTroId { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public IReadOnlyList<MeterImageItemRes> AnhDien { get; set; } = Array.Empty<MeterImageItemRes>();
        public IReadOnlyList<MeterImageItemRes> AnhNuoc { get; set; } = Array.Empty<MeterImageItemRes>();
        public bool CoTheTaiAnh { get; set; }
        public string? LyDoKhongTheTai { get; set; }
    }

    public class StaffMeterPeriodRes
    {
        public int PhongTroId { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public bool CoBanGhiKy { get; set; }
        public bool KyDaDuyet { get; set; }
        public decimal ChiSoDienCu { get; set; }
        public decimal ChiSoNuocCu { get; set; }
        public decimal ChiSoDienMoi { get; set; }
        public decimal ChiSoNuocMoi { get; set; }
        public bool KyBiKhoa { get; set; }
        public string? LyDoKhoa { get; set; }
        public bool CoTheTaiAnh { get; set; }
        public string? LyDoKhongTheTai { get; set; }
        public IReadOnlyList<MeterImageItemRes> AnhDien { get; set; } = Array.Empty<MeterImageItemRes>();
        public IReadOnlyList<MeterImageItemRes> AnhNuoc { get; set; } = Array.Empty<MeterImageItemRes>();
    }

    public class MeterImageUploadCommand
    {
        public int PhongTroId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public AppLoaiDongHo LoaiDongHo { get; set; }
        public UploadFile File { get; set; } = null!;
    }
}
