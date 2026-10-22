using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QuanLyChoThuePhongTroWeb.Application.Common.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.DTOs
{
    public class HopDongReq
    {
        public int HopDongId { get; set; }

        [StringLength(50, ErrorMessage = "Mã hợp đồng tối đa 50 ký tự.")]
        public string? MaHopDong { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phòng trọ.")]
        public int PhongTroId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn người đại diện thuê phòng.")]
        public int NguoiThueId { get; set; }

        [Required(ErrorMessage = "Thời điểm bắt đầu không được để trống.")]
        public DateTime ThoiDiemBatDau { get; set; }

        public DateTime? ThoiDiemKetThuc { get; set; }

        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Tiền cọc phòng phải lớn hơn hoặc bằng 0.")]
        public decimal TienCocPhong { get; set; }

        [Range(1, (double)decimal.MaxValue, ErrorMessage = "Tiền thuê phòng phải lớn hơn 0.")]
        public decimal TienThuePhong { get; set; }

        public AppTrangThaiHopDong TrangThaiHopDong { get; set; } = AppTrangThaiHopDong.DangHoatDong;

        public bool NguoiDungCoOPhongKhong { get; set; } = true;

        // Danh sách những người ở ghép (nếu có)
        public List<int>? ThanhVienKhacIds { get; set; }

        // Danh sách các ID điều khoản mẫu được chọn
        public List<int>? DieuKhoanMauIds { get; set; }
    }
}
