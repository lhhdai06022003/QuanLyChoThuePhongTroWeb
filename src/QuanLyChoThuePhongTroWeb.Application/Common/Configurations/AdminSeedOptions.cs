using QuanLyChoThuePhongTroWeb.Application.Common.Security;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    // Mật khẩu tạo tài khoản admin lần đầu (spec bảo mật xác thực §2.1). Không có mật khẩu mặc định:
    // để trống thì không tạo admin, chỉ ghi Warning khi khởi động.
    public class AdminSeedOptions
    {
        public const string SectionName = "AdminSeed";

        // Biến môi trường AdminSeed__Password. Chỉ dùng khi DB chưa có tài khoản admin.
        public string? Password { get; set; }

        public void Validate()
        {
            if (string.IsNullOrEmpty(Password))
            {
                return;
            }

            // Không đưa mật khẩu vào thông báo lỗi.
            var loi = PasswordPolicy.Validate(Password, "admin", null);
            if (loi != null)
            {
                throw new System.InvalidOperationException($"Cấu hình AdminSeed:Password không hợp lệ: {loi}");
            }
        }
    }
}
