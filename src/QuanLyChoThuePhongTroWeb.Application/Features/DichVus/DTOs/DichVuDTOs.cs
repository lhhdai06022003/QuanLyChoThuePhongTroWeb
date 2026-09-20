using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DichVus.DTOs
{
    public class DichVuReq
    {
        public int DichVuId { get; set; }

        [Required(ErrorMessage = "Tên dịch vụ không được để trống.")]
        [StringLength(100, ErrorMessage = "Tên dịch vụ tối đa 100 ký tự.")]
        public string TenDichVu { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đơn vị tính không được để trống.")]
        [StringLength(50, ErrorMessage = "Đơn vị tính tối đa 50 ký tự.")]
        public string DonVi { get; set; } = string.Empty;

        public string GhiChu { get; set; } = string.Empty;
    }

    public class DichVuRes
    {
        public int DichVuId { get; set; }
        public string TenDichVu { get; set; } = string.Empty;
        public string DonVi { get; set; } = string.Empty;
        public string GhiChu { get; set; } = string.Empty;
    }

    public class DichVuChiNhanhReq
    {
        public int DichVuChiNhanhId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chi nhánh.")]
        public int ChiNhanhId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn dịch vụ.")]
        public int DichVuId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá dịch vụ phải lớn hơn hoặc bằng 0.")]
        public double GiaDichVu { get; set; }

        public bool MacDinh { get; set; } = false;
    }

    public class DichVuChiNhanhRes
    {
        public int DichVuChiNhanhId { get; set; }
        public int ChiNhanhId { get; set; }
        public string TenChiNhanh { get; set; } = string.Empty;
        public int DichVuId { get; set; }
        public string TenDichVu { get; set; } = string.Empty;
        public double GiaDichVu { get; set; }
        public bool MacDinh { get; set; }
    }

    public class DangKyDichVuItem
    {
        public int DichVuChiNhanhId { get; set; }
        public bool IsSelected { get; set; }
        public int SoLuong { get; set; } = 1;
        public DateTime? NgayBatDau { get; set; }
    }

    public class DangKyDichVuReq
    {
        [Required(ErrorMessage = "Vui lòng chọn phòng trọ.")]
        public int PhongTroId { get; set; }

        public List<DangKyDichVuItem> DichVus { get; set; } = new();
    }

    public class DichVuChiNhanhWithDangKyRes
    {
        public int DichVuChiNhanhId { get; set; }
        public string TenDichVu { get; set; } = string.Empty;
        public string DonVi { get; set; } = string.Empty;
        public double GiaDichVu { get; set; }
        public bool MacDinh { get; set; }
        public bool IsSelected { get; set; }
        public int SoLuong { get; set; } = 1;
        public int? DangKyDichVuId { get; set; }
        public DateTime? NgayBatDau { get; set; }
    }
}
