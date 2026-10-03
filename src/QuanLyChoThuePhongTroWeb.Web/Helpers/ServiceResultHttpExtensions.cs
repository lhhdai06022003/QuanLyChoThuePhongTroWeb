using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    public static class ServiceResultHttpExtensions
    {
        // Trả lỗi dạng JSON { success, message } với mã theo loại lỗi. Dùng StatusCode(403) thay vì
        // Forbid(), vì cookie auth sẽ biến Forbid() thành redirect về trang đăng nhập.
        public static IActionResult ToErrorResult(this ControllerBase controller, ServiceResult result)
        {
            var body = new { success = false, message = result.Message };
            return result.ErrorKind switch
            {
                ServiceErrorKind.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, body),
                ServiceErrorKind.NotFound => controller.NotFound(body),
                _ => controller.BadRequest(body)
            };
        }
    }
}
