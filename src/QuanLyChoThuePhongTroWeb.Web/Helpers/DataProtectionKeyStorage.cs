using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Khóa Data Protection mã hóa cookie đăng nhập và token antiforgery. Mặc định ASP.NET Core giữ khóa trong
    // thư mục người dùng của process; trong container thư mục đó mất mỗi lần deploy/khởi động lại, làm mọi người
    // bị đăng xuất và form đang mở báo lỗi antiforgery. Đặt DataProtection:KeysPath (biến môi trường
    // DataProtection__KeysPath) trỏ vào ổ bền, ví dụ /home/DataProtection-Keys trên Azure App Service.
    // Không đặt thì giữ hành vi mặc định (máy phát triển, test).
    public static class DataProtectionKeyStorage
    {
        public const string KeysPathKey = "DataProtection:KeysPath";

        public static IServiceCollection AddPersistentDataProtectionKeys(this IServiceCollection services, IConfiguration configuration)
        {
            var keysPath = configuration[KeysPathKey];
            if (string.IsNullOrWhiteSpace(keysPath))
            {
                return services;
            }

            services.AddDataProtection()
                .PersistKeysToFileSystem(Directory.CreateDirectory(keysPath.Trim()));
            return services;
        }
    }
}
