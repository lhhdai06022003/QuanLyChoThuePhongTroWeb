using System.Text.RegularExpressions;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // HTML không nén và không cache (chống BREACH), nên JS nhúng trong view bị tải lại ở mọi lần mở trang.
    // Các màn lớn giữ JS ở wwwroot/js (nén Brotli, cache một năm nhờ ?v=); view chỉ còn phần truyền dữ liệu từ Razor.
    public class ViewInlineScriptGuardTests
    {
        // Số dòng JS nhúng tối đa còn lại trong view: đủ cho khối cấu hình lấy giá trị từ Razor.
        private const int MaxInlineLines = 40;

        public static IEnumerable<object[]> Views() => ViewSource.OwnScripts.Select(kv => new object[] { kv.Key, kv.Value });

        [Theory]
        [MemberData(nameof(Views))]
        public void LargeView_KeepsItsScriptInAVersionedStaticFile(string view, string js)
        {
            var content = ViewSource.Read(view.Split('/'));

            Assert.Matches($"<script src=\"~/js/{Regex.Escape(js)}\" asp-append-version=\"true\"( defer)?></script>", content);
            Assert.True(File.Exists(Path.Combine(ViewSource.WebRoot(), "wwwroot", "js", js)), $"Thiếu wwwroot/js/{js}");

            var inlineLines = Regex.Matches(content, @"<script(?![^>]*\bsrc=)[^>]*>(.*?)</script>", RegexOptions.Singleline)
                .Sum(m => m.Groups[1].Value.Split('\n').Count(l => l.Trim().Length > 0));
            Assert.True(inlineLines <= MaxInlineLines, $"{view}: còn {inlineLines} dòng JS nhúng (tối đa {MaxInlineLines}); chuyển sang wwwroot/js/{js}.");
        }

        // File JS tĩnh không qua Razor: còn biểu thức Razor thì trình duyệt nhận nguyên văn và lỗi cú pháp.
        [Theory]
        [MemberData(nameof(Views))]
        public void ExtractedScript_HasNoRazorLeftovers(string view, string js)
        {
            var content = ViewSource.Read("wwwroot", "js", js);

            Assert.DoesNotMatch(@"@(Model|ViewBag|ViewData|Html|Url|Json|User)\b|@\(|@\{|@@", content);
            Assert.DoesNotContain("<script", content);
            Assert.False(string.IsNullOrWhiteSpace(view));
        }
    }
}
