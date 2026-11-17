using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.BackgroundJobs.State
{
    // File ghi nhớ "đã gửi nhắc hóa đơn / cảnh báo hợp đồng". Mặc định nằm ở ContentRootPath; trong container thư mục đó
    // mất mỗi lần deploy nên job có thể gửi lại email cũ. Đặt BackgroundJobs:StatePath (biến BackgroundJobs__StatePath)
    // trỏ vào ổ bền, ví dụ /home/state trên Azure App Service.
    internal static class BackgroundJobStatePath
    {
        public const string StatePathKey = "BackgroundJobs:StatePath";

        public static string Resolve(IHostEnvironment env, IConfiguration? configuration, string fileName)
        {
            var statePath = configuration?[StatePathKey];
            if (string.IsNullOrWhiteSpace(statePath))
            {
                return Path.Combine(env.ContentRootPath, fileName);
            }

            return Path.Combine(Directory.CreateDirectory(statePath.Trim()).FullName, fileName);
        }
    }
}
