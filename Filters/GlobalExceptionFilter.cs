using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Net;

namespace QuanLyChoThuePhongTroWeb.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception, "Lỗi hệ thống chưa được xử lý tại endpoint: {Path}", context.HttpContext.Request.Path);

            var isAjax = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json") ||
                         context.HttpContext.Request.ContentType?.Contains("application/json") == true;

            if (isAjax)
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống. Vui lòng liên hệ quản trị viên."
                })
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
            else
            {
                var result = new ViewResult
                {
                    ViewName = "~/Views/Shared/Error.cshtml"
                };

                var model = new QuanLyChoThuePhongTroWeb.Models.ModelsOther.ErrorViewModel
                {
                    RequestId = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
                };

                result.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                {
                    Model = model
                };

                context.Result = result;
            }

            context.ExceptionHandled = true;
        }
    }
}
