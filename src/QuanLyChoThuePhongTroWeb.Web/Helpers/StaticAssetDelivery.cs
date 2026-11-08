using System;
using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Tải file tĩnh nhanh hơn (docs/features/toi-uu-hieu-nang, bước 5):
    // - Nén CSS, JS, SVG bằng Brotli/Gzip. Không nén HTML hay JSON vì có token chống giả mạo
    //   và dữ liệu người dùng (tấn công BREACH); file tĩnh không chứa bí mật nên nén được cả trên HTTPS.
    // - File gắn ?v= (asp-append-version, đổi theo nội dung) cho trình duyệt cache một năm.
    public static class StaticAssetDelivery
    {
        private static readonly string[] CompressibleTypes =
        {
            "text/css",
            "text/javascript",
            "application/javascript",
            "image/svg+xml"
        };

        public static IServiceCollection AddStaticAssetCompression(this IServiceCollection services)
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.MimeTypes = CompressibleTypes;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });
            services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
            return services;
        }

        public static StaticFileOptions FileOptions() => new()
        {
            OnPrepareResponse = context =>
            {
                if (context.Context.Request.Query.ContainsKey("v"))
                {
                    context.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=31536000";
                }
            }
        };
    }
}
