using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Băm giả "hash:<mật khẩu>"; hash bắt đầu bằng "legacy:" coi như hash cũ cần băm lại. Đếm số lần kiểm mật khẩu.
    public class FakePasswordService : IPasswordService
    {
        public int VerifyCount { get; private set; }

        public string HashPassword(string password) => "hash:" + password;

        public bool VerifyPassword(string hashedPassword, string providedPassword, out bool needsRehash)
        {
            VerifyCount++;
            needsRehash = false;
            if (hashedPassword == "legacy:" + providedPassword)
            {
                needsRehash = true;
                return true;
            }

            return hashedPassword == "hash:" + providedPassword;
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword) =>
            VerifyPassword(hashedPassword, providedPassword, out _);
    }
}
