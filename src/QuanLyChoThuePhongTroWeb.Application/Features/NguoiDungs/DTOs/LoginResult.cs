namespace QuanLyChoThuePhongTroWeb.Application.Features.NguoiDungs.DTOs
{
    public enum LoginStatus
    {
        Sai = 0,
        TamKhoa = 1,
        ThanhCong = 2
    }

    public class LoginResult
    {
        public LoginStatus Status { get; set; }
        public UserAuthDto? User { get; set; }

        // Số phút còn bị khóa (làm tròn lên), chỉ có giá trị khi Status = TamKhoa.
        public int SoPhutCho { get; set; }

        public static LoginResult Sai() => new() { Status = LoginStatus.Sai };

        public static LoginResult TamKhoa(int soPhutCho) => new() { Status = LoginStatus.TamKhoa, SoPhutCho = soPhutCho };

        public static LoginResult ThanhCong(UserAuthDto user) => new() { Status = LoginStatus.ThanhCong, User = user };
    }
}
