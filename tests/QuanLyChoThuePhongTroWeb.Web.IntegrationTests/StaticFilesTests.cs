using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Web.IntegrationTests.Fixtures;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    [Collection(WebTestCollection.Name)]
    public class StaticFilesTests
    {
        private readonly CustomWebApplicationFactory _factory;

        public StaticFilesTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static HttpRequestMessage Get(string url, string? acceptEncoding)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (acceptEncoding != null)
            {
                request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
            }
            return request;
        }

        [Fact]
        public async Task StaticFiles_Favicon_ShouldReturnSuccessStatusCode()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/favicon.ico");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Content.Headers.ContentLength > 0);
        }

        // Nén CSS/JS (docs/features/toi-uu-hieu-nang, bước 5): nội dung sau giải nén phải giống hệt bản gốc.
        [Theory]
        [InlineData("/Theme/tabler/dist/css/tabler.min.css", "text/css")]
        [InlineData("/lib/jquery/dist/jquery.min.js", "javascript")]
        public async Task CssAndJs_AreGzipped_WhenClientAccepts_AndContentIsUnchanged(string url, string contentType)
        {
            var client = _factory.CreateClient();

            var raw = await client.SendAsync(Get(url, null));
            var gzip = await client.SendAsync(Get(url, "gzip"));

            Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
            Assert.Equal(HttpStatusCode.OK, gzip.StatusCode);
            Assert.Empty(raw.Content.Headers.ContentEncoding);
            Assert.Contains("gzip", gzip.Content.Headers.ContentEncoding);
            Assert.Contains(contentType, gzip.Content.Headers.ContentType!.MediaType);

            var rawBytes = await raw.Content.ReadAsByteArrayAsync();
            var compressed = await gzip.Content.ReadAsByteArrayAsync();
            using var unzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
            using var output = new MemoryStream();
            await unzip.CopyToAsync(output);

            Assert.True(compressed.Length < rawBytes.Length / 2, $"{url}: {compressed.Length} / {rawBytes.Length} byte");
            Assert.Equal(rawBytes, output.ToArray());
        }

        [Fact]
        public async Task Css_IsBrotliCompressed_WhenClientPrefersBrotli()
        {
            var client = _factory.CreateClient();

            var response = await client.SendAsync(Get("/css/site.css", "br, gzip"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("br", response.Content.Headers.ContentEncoding);
        }

        // HTML có token chống giả mạo: không nén để tránh tấn công BREACH.
        [Fact]
        public async Task HtmlPage_IsNotCompressed()
        {
            var client = _factory.CreateClient();

            var response = await client.SendAsync(Get("/QuanLyNhaTro/DangNhap", "br, gzip"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(response.Content.Headers.ContentEncoding);
            Assert.Contains("__RequestVerificationToken", await response.Content.ReadAsStringAsync());
        }

        // File gắn ?v= (asp-append-version, đổi theo nội dung) được cache lâu; file không có phiên bản giữ nguyên như cũ.
        [Fact]
        public async Task VersionedFile_IsCachedLong_UnversionedFile_Unchanged()
        {
            var client = _factory.CreateClient();

            var versioned = await client.GetAsync("/css/site.css?v=abc123");
            var plain = await client.GetAsync("/css/site.css");

            Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
            Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
            Assert.Equal(System.TimeSpan.FromDays(365), versioned.Headers.CacheControl?.MaxAge);
            Assert.True(versioned.Headers.CacheControl!.Public);
            Assert.Null(plain.Headers.CacheControl);
        }
    }
}
