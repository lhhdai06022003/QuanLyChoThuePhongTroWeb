using System;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Security
{
    // Đếm lần đăng nhập sai theo tên đăng nhập và theo địa chỉ máy khách (spec bảo mật xác thực §2.6).
    public interface ILoginAttemptThrottle
    {
        // Thời gian khóa còn lại; null khi không bị khóa.
        TimeSpan? GetLockRemaining(string tenDangNhap, string? clientKey);

        void RecordFailure(string tenDangNhap, string? clientKey);

        // Xóa bộ đếm của tên đăng nhập; không xóa bộ đếm theo địa chỉ máy khách.
        void RecordSuccess(string tenDangNhap);
    }
}
