using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QuanLyChoThuePhongTroWeb.Web.Helpers;

namespace QuanLyChoThuePhongTroWeb.Filters
{
    // Chặn người đang có claim BatBuocDoiMatKhau (spec bảo mật xác thực §2.2): trang thường chuyển tới màn đổi mật khẩu,
    // yêu cầu AJAX nhận 403 JSON. Bỏ qua người chưa đăng nhập (để [Authorize] xử lý) và action có [BoQuaYeuCauDoiMatKhau].
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class YeuCauDoiMatKhauAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (!ForcedPasswordChange.IsRequired(user))
            {
                return;
            }

            if (context.ActionDescriptor.EndpointMetadata.OfType<BoQuaYeuCauDoiMatKhauAttribute>().Any())
            {
                return;
            }

            var redirectUrl = ForcedPasswordChange.ChangeUrl(user);
            if (IsAjaxRequest(context.HttpContext.Request))
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    requireChangePassword = true,
                    message = "Bạn cần đổi mật khẩu trước khi tiếp tục.",
                    redirectUrl
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            context.Result = new RedirectResult(redirectUrl);
        }

        private static bool IsAjaxRequest(HttpRequest request)
        {
            if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return request.Headers.Accept.Any(v => v != null && v.Contains("application/json", StringComparison.OrdinalIgnoreCase));
        }
    }

    // Action được phép dùng khi đang bị ép đổi mật khẩu: trang/API đổi mật khẩu, đăng nhập, đăng xuất.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class BoQuaYeuCauDoiMatKhauAttribute : Attribute
    {
    }
}
