using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyChoThuePhongTroWeb.Application.Features.DieuKhoanMaus.DTOs
{
    public class DieuKhoanMauReq
    {
        public int DieuKhoanMauId { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [StringLength(255, ErrorMessage = "Tiêu đề không được vượt quá 255 ký tự")]
        public string TieuDe { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nội dung không được để trống")]
        public string NoiDung { get; set; } = string.Empty;
    }

    public class DieuKhoanMauRes
    {
        public int DieuKhoanMauId { get; set; }
        public string TieuDe { get; set; } = string.Empty;
        public string NoiDung { get; set; } = string.Empty;
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCapNhat { get; set; }
    }
}
