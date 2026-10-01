using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;

namespace QuanLyChoThuePhongTroWeb.Models.ChiSoDienNuoc
{
    public class StaffMeterImageVm
    {
        public int Id { get; set; }
        public int Loai { get; set; }
        public string Url { get; set; } = string.Empty;
        public int TrangThai { get; set; }
        public string NhanTrangThai { get; set; } = string.Empty;
        public string? GiaTriAI { get; set; }
        public decimal? GiaTriAISo { get; set; }
        public string? DoTinCay { get; set; }
        public string? GiaTriXacNhan { get; set; }
        public bool LaChinhThuc { get; set; }
        public string NgayGui { get; set; } = string.Empty;
        public string? ThongBaoLoi { get; set; }
        public string? GhiChuXacNhan { get; set; }
        public string NguonGui { get; set; } = string.Empty;
        public bool CoTheThuLai { get; set; }
        public bool CoTheXacNhan { get; set; }
        public bool CanLyDo { get; set; }
        public bool CoTheSuaSo { get; set; }
    }

    public class StaffMeterPeriodVm
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
        public IReadOnlyList<StaffMeterImageVm> AnhDien { get; set; } = Array.Empty<StaffMeterImageVm>();
        public IReadOnlyList<StaffMeterImageVm> AnhNuoc { get; set; } = Array.Empty<StaffMeterImageVm>();
    }

    public class TenantMeterImageVm
    {
        public int Id { get; set; }
        public int Loai { get; set; }
        public string Url { get; set; } = string.Empty;
        public string NhanTrangThai { get; set; } = string.Empty;
        // Màu chấm trạng thái của Tabler (blue/orange/green/secondary), đi cùng NhanTrangThai.
        public string MauTrangThai { get; set; } = "secondary";
        public bool LaChinhThuc { get; set; }
        public string NgayGui { get; set; } = string.Empty;
    }

    public class TenantMeterPeriodVm
    {
        public int PhongTroId { get; set; }
        public string TenPhong { get; set; } = string.Empty;
        public int Thang { get; set; }
        public int Nam { get; set; }
        public bool CoTheTaiAnh { get; set; }
        public string? LyDoKhongTheTai { get; set; }
        public IReadOnlyList<TenantMeterImageVm> AnhDien { get; set; } = Array.Empty<TenantMeterImageVm>();
        public IReadOnlyList<TenantMeterImageVm> AnhNuoc { get; set; } = Array.Empty<TenantMeterImageVm>();
    }

    public class ConfirmMeterImageBodyReq
    {
        public decimal GiaTriXacNhan { get; set; }
        public string? GhiChu { get; set; }
    }

    public class CorrectMeterImageBodyReq
    {
        public decimal GiaTriMoi { get; set; }
        public string LyDo { get; set; } = string.Empty;
    }

    public class UploadMeterImageFormReq
    {
        public int PhongTroId { get; set; }
        public int Thang { get; set; }
        public int Nam { get; set; }
        public int LoaiDongHo { get; set; }
        public IFormFile? File { get; set; }
    }

    public static class MeterImageVmMapper
    {
        private static readonly CultureInfo ViVn = new("vi-VN");

        public static string FormatMeterNumber(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("#,##0.###", ViVn) : string.Empty;
        }

        public static string FormatVnDateTime(DateTime utcTime)
        {
            return utcTime.AddHours(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        public static string GetStaffStatusLabel(AppTrangThaiAnhChiSo status)
        {
            return status switch
            {
                AppTrangThaiAnhChiSo.MoiTaiLen => "Mới tải lên",
                AppTrangThaiAnhChiSo.DangXuLy => "Đang nhận diện",
                AppTrangThaiAnhChiSo.DocDuoc => "Đọc được",
                AppTrangThaiAnhChiSo.Loi => "Lỗi nhận diện",
                AppTrangThaiAnhChiSo.KhongDocDuoc => "Không đọc được",
                AppTrangThaiAnhChiSo.CanChupLai => "Cần chụp lại",
                AppTrangThaiAnhChiSo.DaXacNhan => "Đã xác nhận",
                AppTrangThaiAnhChiSo.DaThayThe => "Đã thay thế",
                _ => status.ToString()
            };
        }

        public static string GetTenantStatusLabel(AppTrangThaiAnhChiSo status, decimal? giaTriAI, decimal? giaTriXacNhan)
        {
            return status switch
            {
                AppTrangThaiAnhChiSo.MoiTaiLen or AppTrangThaiAnhChiSo.DangXuLy => "Đang nhận diện",
                AppTrangThaiAnhChiSo.DocDuoc => $"AI gợi ý: {FormatMeterNumber(giaTriAI)} (chưa phải số chính thức)",
                AppTrangThaiAnhChiSo.KhongDocDuoc or AppTrangThaiAnhChiSo.Loi or AppTrangThaiAnhChiSo.CanChupLai => "Không đọc được, vui lòng chụp lại",
                AppTrangThaiAnhChiSo.DaXacNhan => $"Đã xác nhận: {FormatMeterNumber(giaTriXacNhan)}",
                AppTrangThaiAnhChiSo.DaThayThe => "Đã được thay bằng ảnh khác",
                _ => status.ToString()
            };
        }

        // Cùng cách gom trạng thái với GetTenantStatusLabel để nhãn và màu luôn khớp.
        public static string GetTenantStatusColor(AppTrangThaiAnhChiSo status)
        {
            return status switch
            {
                AppTrangThaiAnhChiSo.MoiTaiLen or AppTrangThaiAnhChiSo.DangXuLy or AppTrangThaiAnhChiSo.DocDuoc => "blue",
                AppTrangThaiAnhChiSo.KhongDocDuoc or AppTrangThaiAnhChiSo.Loi or AppTrangThaiAnhChiSo.CanChupLai => "orange",
                AppTrangThaiAnhChiSo.DaXacNhan => "green",
                _ => "secondary"
            };
        }

        public static StaffMeterImageVm ToStaffVm(MeterImageItemRes dto)
        {
            var doTinCayStr = dto.DoTinCay.HasValue
                ? $"{Math.Round(dto.DoTinCay.Value * 100):0}%"
                : null;

            return new StaffMeterImageVm
            {
                Id = dto.AnhChiSoDongHoId,
                Loai = (int)dto.LoaiDongHo,
                Url = dto.Url,
                TrangThai = (int)dto.TrangThai,
                NhanTrangThai = GetStaffStatusLabel(dto.TrangThai),
                GiaTriAI = dto.GiaTriAIGoiY.HasValue ? FormatMeterNumber(dto.GiaTriAIGoiY) : null,
                GiaTriAISo = dto.GiaTriAIGoiY,
                DoTinCay = doTinCayStr,
                GiaTriXacNhan = dto.GiaTriXacNhan.HasValue ? FormatMeterNumber(dto.GiaTriXacNhan) : null,
                LaChinhThuc = dto.LaChinhThuc,
                NgayGui = FormatVnDateTime(dto.NgayGuiUtc),
                ThongBaoLoi = dto.ThongBaoLoi,
                GhiChuXacNhan = dto.GhiChuXacNhan,
                NguonGui = dto.NguoiGuiLaKhachThue ? "Khách thuê" : "Nhân viên",
                CoTheThuLai = dto.CoTheThuLai,
                CoTheXacNhan = dto.CoTheXacNhan,
                CanLyDo = dto.CanLyDoKhiXacNhan,
                CoTheSuaSo = dto.CoTheSuaSo
            };
        }

        public static StaffMeterPeriodVm ToStaffVm(StaffMeterPeriodRes dto)
        {
            return new StaffMeterPeriodVm
            {
                PhongTroId = dto.PhongTroId,
                TenPhong = dto.TenPhong,
                Thang = dto.Thang,
                Nam = dto.Nam,
                CoBanGhiKy = dto.CoBanGhiKy,
                KyDaDuyet = dto.KyDaDuyet,
                ChiSoDienCu = dto.ChiSoDienCu,
                ChiSoNuocCu = dto.ChiSoNuocCu,
                ChiSoDienMoi = dto.ChiSoDienMoi,
                ChiSoNuocMoi = dto.ChiSoNuocMoi,
                KyBiKhoa = dto.KyBiKhoa,
                LyDoKhoa = dto.LyDoKhoa,
                CoTheTaiAnh = dto.CoTheTaiAnh,
                LyDoKhongTheTai = dto.LyDoKhongTheTai,
                AnhDien = dto.AnhDien.Select(ToStaffVm).ToList(),
                AnhNuoc = dto.AnhNuoc.Select(ToStaffVm).ToList()
            };
        }

        public static TenantMeterImageVm ToTenantVm(MeterImageItemRes dto)
        {
            return new TenantMeterImageVm
            {
                Id = dto.AnhChiSoDongHoId,
                Loai = (int)dto.LoaiDongHo,
                Url = dto.Url,
                NhanTrangThai = GetTenantStatusLabel(dto.TrangThai, dto.GiaTriAIGoiY, dto.GiaTriXacNhan),
                MauTrangThai = GetTenantStatusColor(dto.TrangThai),
                LaChinhThuc = dto.LaChinhThuc,
                NgayGui = FormatVnDateTime(dto.NgayGuiUtc)
            };
        }

        public static TenantMeterPeriodVm ToTenantVm(TenantMeterPeriodRes dto)
        {
            return new TenantMeterPeriodVm
            {
                PhongTroId = dto.PhongTroId,
                TenPhong = dto.TenPhong,
                Thang = dto.Thang,
                Nam = dto.Nam,
                CoTheTaiAnh = dto.CoTheTaiAnh,
                LyDoKhongTheTai = dto.LyDoKhongTheTai,
                AnhDien = dto.AnhDien.Select(ToTenantVm).ToList(),
                AnhNuoc = dto.AnhNuoc.Select(ToTenantVm).ToList()
            };
        }
    }
}
