using System;
using System.Linq;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public enum InvoiceLineKind
    {
        Khac = 0,
        Dien = 1,
        Nuoc = 2
    }

    /// <summary>
    /// Nhận diện dòng điện/nước trên hóa đơn. Dòng do hệ thống sinh ra không gắn DichVuId (tiền điện nước tính theo chỉ số của phòng),
    /// nên nhận diện theo loại dịch vụ nếu có, còn lại theo tên bắt đầu bằng "điện"/"nước" hoặc "tiền điện"/"tiền nước". Nhận cả tên đã bị
    /// nhân viên sửa khi chỉnh hóa đơn ("Điện tháng 9 (…)") để lần tính lại cập nhật đúng dòng đó thay vì thêm dòng thứ hai gây tính tiền trùng.
    /// </summary>
    public static class InvoiceMeterLine
    {
        private static readonly string[] NonMeterNamePrefixes = { "điện thoại", "nước uống", "nước lọc", "nước giải khát" };

        public static bool IsMeterLine(LoaiDichVu? loaiDichVu, string? tenDichVu, LoaiDichVu loai, string keyword)
        {
            if (loaiDichVu.HasValue) return loaiDichVu.Value == loai;

            var name = (tenDichVu ?? string.Empty).Trim();
            if (name.StartsWith("Tiền ", StringComparison.OrdinalIgnoreCase)) name = name.Substring(5).TrimStart();
            if (!name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)) return false;

            // Sau từ khóa phải là hết tên hoặc ký tự không phải chữ, và loại các dịch vụ thường gặp chỉ tình cờ bắt đầu bằng từ khóa.
            if (name.Length > keyword.Length && char.IsLetter(name[keyword.Length])) return false;
            return !NonMeterNamePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        public static InvoiceLineKind Classify(LoaiDichVu? loaiDichVu, string? tenDichVu)
        {
            if (IsMeterLine(loaiDichVu, tenDichVu, LoaiDichVu.Dien, "điện")) return InvoiceLineKind.Dien;
            if (IsMeterLine(loaiDichVu, tenDichVu, LoaiDichVu.Nuoc, "nước")) return InvoiceLineKind.Nuoc;
            return InvoiceLineKind.Khac;
        }
    }
}
