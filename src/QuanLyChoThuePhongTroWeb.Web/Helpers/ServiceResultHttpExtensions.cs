using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;

namespace QuanLyChoThuePhongTroWeb.Web.Helpers
{
    public static class ServiceResultHttpExtensions
    {
        // Mã HTTP theo loại lỗi: Forbidden 403, NotFound 404, còn lại 400. Dùng chung cho lỗi dạng JSON và trang lỗi HTML.
        public static int ToHttpStatusCode(this ServiceResult result)
        {
            return result.ErrorKind switch
            {
                ServiceErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                ServiceErrorKind.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };
        }

        // Trả lỗi dạng JSON { success, message } với mã theo loại lỗi. Dùng StatusCode(403) thay vì
        // Forbid(), vì cookie auth sẽ biến Forbid() thành redirect về trang đăng nhập.
        public static IActionResult ToErrorResult(this ControllerBase controller, ServiceResult result)
        {
            var body = new { success = false, message = result.Message };
            return controller.StatusCode(result.ToHttpStatusCode(), body);
        }
    }
}
