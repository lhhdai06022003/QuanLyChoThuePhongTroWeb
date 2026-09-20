using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.ThanhVienHopDongs.DTOs
{
    public class ThanhVienHopDongReq
    {
        [Required(ErrorMessage = "Vui lòng chọn hợp đồng.")]
        public int HopDongId { get; set; }

        public int? NguoiThueId { get; set; } // Nếu chọn từ khách có sẵn

        // Các thông tin cho việc tạo khách mới nếu không chọn khách cũ
        public string? HoVaTen { get; set; }
        public string? SoDienThoai { get; set; }
        public string? CCCD { get; set; }

        [Required(ErrorMessage = "Ngày bắt đầu ở không được để trống.")]
        public DateTime NgayVao { get; set; }
    }
}
