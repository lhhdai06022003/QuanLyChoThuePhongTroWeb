using System;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs
{
    public class UserAuthDto
    {
        public int NguoiDungId { get; set; }
        public string TenDangNhap { get; set; } = string.Empty;
        public AppRole Role { get; set; }
        public int? NguoiThueId { get; set; }
        public string? HoVaTen { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
    }

    public class NguoiDungRes
    {
        public int NguoiDungId { get; set; }
        public string TenDangNhap { get; set; } = string.Empty;
        public AppRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? LanDangNhapCuoi { get; set; }
        public int? NguoiThueId { get; set; }
        public string? TenNguoiThue { get; set; }
    }

    public class CreateNguoiDungReq
    {
        public string TenDangNhap { get; set; } = string.Empty;
        public string MatKhau { get; set; } = string.Empty;
        public string? MatKhauHash
        {
            get => MatKhau;
            set { if (!string.IsNullOrEmpty(value)) MatKhau = value; }
        }
        public AppRole Role { get; set; }
        public int? NguoiThueId { get; set; }
    }

    public class EditNguoiDungReq
    {
        public int NguoiDungId { get; set; }
        public AppRole Role { get; set; }
        public bool IsActive { get; set; }
        public string? MatKhauMoi { get; set; }
        public string? MatKhauHash
        {
            get => MatKhauMoi;
            set { if (!string.IsNullOrEmpty(value)) MatKhauMoi = value; }
        }
        public int? NguoiThueId { get; set; }
    }
}
