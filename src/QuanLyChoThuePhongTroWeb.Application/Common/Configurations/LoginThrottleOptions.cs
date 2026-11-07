namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    // Giới hạn số lần đăng nhập sai (spec bảo mật xác thực §2.6). Không cấu hình thì dùng mặc định dưới đây.
    public class LoginThrottleOptions
    {
        public const string SectionName = "LoginThrottle";

        public int MaxFailuresPerUsername { get; set; } = 5;

        // 0 = tắt bộ đếm theo địa chỉ máy khách.
        public int MaxFailuresPerClient { get; set; } = 20;

        public int WindowMinutes { get; set; } = 15;
        public int LockMinutes { get; set; } = 15;

        // Trần số khóa giữ trong bộ nhớ để chuỗi tên ngẫu nhiên không làm phình bộ nhớ.
        public int MaxTrackedKeys { get; set; } = 10_000;

        public void Validate()
        {
            if (MaxFailuresPerUsername < 1)
            {
                throw new System.InvalidOperationException("Cấu hình LoginThrottle:MaxFailuresPerUsername phải lớn hơn hoặc bằng 1.");
            }

            if (MaxFailuresPerClient < 0)
            {
                throw new System.InvalidOperationException("Cấu hình LoginThrottle:MaxFailuresPerClient không được âm (0 là tắt).");
            }

            if (WindowMinutes < 1)
            {
                throw new System.InvalidOperationException("Cấu hình LoginThrottle:WindowMinutes phải lớn hơn hoặc bằng 1.");
            }

            if (LockMinutes < 1)
            {
                throw new System.InvalidOperationException("Cấu hình LoginThrottle:LockMinutes phải lớn hơn hoặc bằng 1.");
            }

            if (MaxTrackedKeys < 100)
            {
                throw new System.InvalidOperationException("Cấu hình LoginThrottle:MaxTrackedKeys phải lớn hơn hoặc bằng 100.");
            }
        }
    }
}
