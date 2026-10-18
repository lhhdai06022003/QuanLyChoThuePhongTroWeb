using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Web.Helpers;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Lỗi dịch vụ sang HTTP (spec thanh toán trực tuyến §4.1): mã theo loại lỗi dùng chung cho JSON và trang lỗi HTML,
    // ToErrorResult vẫn trả cùng mã và cùng thân { success:false, message } như trước khi tách ToHttpStatusCode. Không cần DB.
    public class ServiceResultHttpExtensionsTests
    {
        private sealed class AnyController : ControllerBase { }

        [Theory]
        [InlineData(ServiceErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
        [InlineData(ServiceErrorKind.NotFound, StatusCodes.Status404NotFound)]
        public void ToHttpStatusCode_FollowsTheErrorKind(ServiceErrorKind kind, int expected)
        {
            var result = kind == ServiceErrorKind.Forbidden ? ServiceResult.Forbidden("x") : ServiceResult.NotFound("x");

            Assert.Equal(expected, result.ToHttpStatusCode());
        }

        [Fact]
        public void ToHttpStatusCode_IsBadRequest_ForAPlainFailure_AndForTheGenericTypedFailure()
        {
            Assert.Equal(StatusCodes.Status400BadRequest, ServiceResult.Fail("x").ToHttpStatusCode());
            Assert.Equal(StatusCodes.Status400BadRequest, ServiceResult<int>.Fail("x").ToHttpStatusCode());
            Assert.Equal(StatusCodes.Status403Forbidden, ServiceResult<int>.Forbidden("x").ToHttpStatusCode());
            Assert.Equal(StatusCodes.Status404NotFound, ServiceResult<int>.NotFound("x").ToHttpStatusCode());
        }

        [Theory]
        [InlineData(ServiceErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
        [InlineData(ServiceErrorKind.NotFound, StatusCodes.Status404NotFound)]
        [InlineData(ServiceErrorKind.Validation, StatusCodes.Status400BadRequest)]
        public void ToErrorResult_KeepsTheStatusCodeAndTheJsonBody(ServiceErrorKind kind, int expectedStatus)
        {
            var failure = kind switch
            {
                ServiceErrorKind.Forbidden => ServiceResult.Forbidden("Không có quyền"),
                ServiceErrorKind.NotFound => ServiceResult.NotFound("Không thấy"),
                _ => ServiceResult.Fail("Sai dữ liệu")
            };

            var result = Assert.IsAssignableFrom<ObjectResult>(new AnyController().ToErrorResult(failure));

            Assert.Equal(expectedStatus, result.StatusCode);
            using var body = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
            Assert.False(body.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(failure.Message, body.RootElement.GetProperty("message").GetString());
            Assert.Equal(2, body.RootElement.EnumerateObject().Count());
        }
    }
}
