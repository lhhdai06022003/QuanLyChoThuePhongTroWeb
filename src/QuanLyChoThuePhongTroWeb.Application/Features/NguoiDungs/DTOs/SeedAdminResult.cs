namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs
{
    public enum SeedAdminResult
    {
        // Đã có tài khoản admin chưa xóa: không làm gì.
        DaCo = 0,
        // Vừa tạo admin bằng AdminSeed:Password.
        DaTao = 1,
        // Chưa có admin và chưa cấu hình AdminSeed:Password: không tạo.
        ThieuMatKhau = 2
    }
}
