using System.IO;
using System.Text;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class CommonTypesTests
    {
        [Fact]
        public void SelectOptionDto_SetsPropertiesCorrectly()
        {
            var dto = new SelectOptionDto("1", "Chi nhanh 1", true);

            Assert.Equal("1", dto.Value);
            Assert.Equal("Chi nhanh 1", dto.Text);
            Assert.True(dto.Selected);
        }

        [Fact]
        public void UploadFile_PropertiesAndStreamAccessWork()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("fake file content");
            using var stream = new MemoryStream(bytes);
            var file = new UploadFile(stream, "test.png", "image/png", bytes.Length);

            Assert.Equal("test.png", file.FileName);
            Assert.Equal("image/png", file.ContentType);
            Assert.Equal(bytes.Length, file.Length);
            Assert.NotNull(file.Content);
        }

        [Fact]
        public void ServiceResult_Fail_DefaultsToValidationKind()
        {
            Assert.Equal(ServiceErrorKind.Validation, ServiceResult.Fail("x").ErrorKind);
            Assert.Equal(ServiceErrorKind.Validation, ServiceResult<int>.Fail("x").ErrorKind);
        }

        [Fact]
        public void ServiceResult_ForbiddenAndNotFound_CarryKind()
        {
            var forbidden = ServiceResult<int>.Forbidden("cấm");
            var notFound = ServiceResult.NotFound("không thấy");

            Assert.False(forbidden.Success);
            Assert.Equal(ServiceErrorKind.Forbidden, forbidden.ErrorKind);
            Assert.Equal("cấm", forbidden.Message);
            Assert.False(notFound.Success);
            Assert.Equal(ServiceErrorKind.NotFound, notFound.ErrorKind);
        }

        [Fact]
        public void ServiceResult_FailFrom_KeepsMessageAndKind()
        {
            var converted = ServiceResult<string>.FailFrom(ServiceResult.Forbidden("cấm"));

            Assert.False(converted.Success);
            Assert.Equal("cấm", converted.Message);
            Assert.Equal(ServiceErrorKind.Forbidden, converted.ErrorKind);
            Assert.Null(converted.Data);
        }
    }
}
