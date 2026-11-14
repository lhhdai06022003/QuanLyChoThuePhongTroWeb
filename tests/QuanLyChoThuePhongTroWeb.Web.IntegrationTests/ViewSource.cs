using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // JS của các màn lớn đã tách khỏi view sang wwwroot/js (docs/features/toi-uu-hieu-nang, bước 15).
    // Test soát tĩnh từng đọc riêng file .cshtml phải đọc kèm file JS của màn, nếu không sẽ bỏ sót phần mã đã chuyển đi.
    internal static class ViewSource
    {
        // Đường dẫn view (tính từ thư mục project Web, dùng '/') → file JS riêng của màn trong wwwroot/js.
        public static readonly IReadOnlyDictionary<string, string> OwnScripts = new Dictionary<string, string>
        {
            ["Areas/QuanLyNhaTro/Views/DienNuoc/Index.cshtml"] = "chot-dien-nuoc.js"
        };

        public static string WebRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuanLyChoThuePhongTroWeb.sln")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "QuanLyChoThuePhongTroWeb.Web");
        }

        public static string Read(params string[] relative) =>
            File.ReadAllText(Path.Combine(new[] { WebRoot() }.Concat(relative).ToArray()));

        // Nội dung view nối với file JS riêng của màn (nếu màn đã tách JS).
        public static string ReadWithOwnScript(params string[] relative)
        {
            var content = Read(relative);
            var key = string.Join("/", relative.SelectMany(p => p.Split('/', '\\')));
            return OwnScripts.TryGetValue(key, out var js)
                ? content + "\n" + Read("wwwroot", "js", js)
                : content;
        }

        // HTML của trang đã render nối với các file JS riêng của màn mà trang nạp (<script src="/js/<file>?v=...">),
        // cho test kiểm chuỗi JS trên trang trả về.
        public static string WithOwnScripts(string html)
        {
            var result = html;
            foreach (var js in OwnScripts.Values.Distinct())
            {
                if (html.Contains($"src=\"/js/{js}"))
                {
                    result += "\n" + Read("wwwroot", "js", js);
                }
            }
            return result;
        }
    }
}
