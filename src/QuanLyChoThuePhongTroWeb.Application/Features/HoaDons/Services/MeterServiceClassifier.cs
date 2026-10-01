using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    /// <summary>
    /// Nhận diện dịch vụ điện/nước. Tiền điện nước đã được tính theo chỉ số công tơ,
    /// nên dịch vụ này không được tính thêm lần nữa như dịch vụ cố định.
    /// Giao diện quản lý dịch vụ không cho chọn LoaiDichVu, nên dịch vụ tạo tay luôn là Khac;
    /// vì vậy phải nhận diện thêm theo tên chuẩn và đơn vị tính.
    /// </summary>
    public static class MeterServiceClassifier
    {
        private static readonly string[] ElectricNames =
        {
            "điện", "dien", "tiền điện", "tien dien", "điện sinh hoạt", "dien sinh hoat"
        };

        private static readonly string[] WaterNames =
        {
            "nước", "nuoc", "tiền nước", "tien nuoc", "nước sinh hoạt", "nuoc sinh hoat"
        };

        public static bool IsMeterBased(LoaiDichVu loaiDichVu, string? tenDichVu, string? donVi)
        {
            if (loaiDichVu == LoaiDichVu.Dien || loaiDichVu == LoaiDichVu.Nuoc)
                return true;

            var ten = (tenDichVu ?? string.Empty).Trim().ToLowerInvariant();
            if (ElectricNames.Contains(ten) || WaterNames.Contains(ten))
                return true;

            var unit = (donVi ?? string.Empty).Trim().ToLowerInvariant();
            return unit is "kwh" or "m3" or "m³";
        }
    }
}
