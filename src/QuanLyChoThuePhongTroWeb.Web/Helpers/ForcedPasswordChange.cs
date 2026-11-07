using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    // Ép đổi mật khẩu bằng claim trong cookie (spec bảo mật xác thực §2.2): gắn lúc đăng nhập, gỡ khi đổi mật khẩu xong.
    public static class ForcedPasswordChange
    {
        public const string ClaimType = "BatBuocDoiMatKhau";

        public const string ManagerChangeUrl = "/QuanLyNhaTro/DoiMatKhau";
        public const string TenantChangeUrl = "/KhachThue/HoSo?doiMatKhau=1";

        public static bool IsRequired(ClaimsPrincipal? user) =>
            user?.Identity?.IsAuthenticated == true && user.HasClaim(c => c.Type == ClaimType && c.Value == "1");

        // Trang đổi mật khẩu theo cổng của người dùng.
        public static string ChangeUrl(ClaimsPrincipal user) =>
            user.IsInRole("KhachThue") ? TenantChangeUrl : ManagerChangeUrl;

        // Phát hành lại cookie không còn claim ép đổi, giữ nguyên IsPersistent và hạn của phiên ghi nhớ.
        public static async Task ReissueWithoutClaimAsync(HttpContext httpContext)
        {
            var auth = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Principal?.Identity is not ClaimsIdentity oldIdentity)
            {
                return;
            }

            var identity = new ClaimsIdentity(
                oldIdentity.Claims.Where(c => c.Type != ClaimType),
                oldIdentity.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme,
                oldIdentity.NameClaimType,
                oldIdentity.RoleClaimType);

            var isPersistent = auth.Properties?.IsPersistent == true;
            var props = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = isPersistent ? auth.Properties!.ExpiresUtc : null
            };

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);
        }
    }
}
