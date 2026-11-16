using System.IO;
using System.Linq;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Không cần DB. Gửi email phải xác minh chứng chỉ SMTP theo mặc định của MailKit: không ghi đè
    // ServerCertificateValidationCallback (trước đây "=> true" chấp nhận mọi chứng chỉ, mở đường nghe lén
    // mật khẩu SMTP khi chạy trên Internet; docs/deployment/README.md, mục bảo mật).
    public class SmtpCertificateValidationGuardTests
    {
        private static string InfrastructureRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "QuanLyChoThuePhongTroWeb.Infrastructure");
        }

        [Fact]
        public void NoSourceFile_OverridesSmtpCertificateValidation()
        {
            var root = InfrastructureRoot();
            var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .Where(path => File.ReadAllText(path).Contains("ServerCertificateValidationCallback"))
                .Select(path => Path.GetRelativePath(root, path))
                .ToList();

            Assert.Empty(offenders);
        }
    }
}
