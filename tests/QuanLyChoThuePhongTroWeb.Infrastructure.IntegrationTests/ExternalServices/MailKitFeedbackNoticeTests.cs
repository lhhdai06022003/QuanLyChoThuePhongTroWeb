using System;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Emails;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Hàm thuần, không cần DB: dòng hạn phản hồi trong email hóa đơn.
    public class MailKitFeedbackNoticeTests
    {
        private static readonly DateTime Now = new(2026, 10, 14, 1, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Han = new(2026, 10, 15, 3, 30, 0, DateTimeKind.Utc);

        [Fact]
        public void WithBranchPhone_AsksToContactBeforeDeadline()
        {
            var html = MailKitEmailService.BuildFeedbackNoticeHtml(" 0900000000 ", Han, Now);

            Assert.Contains("Nếu thấy sai sót, vui lòng liên hệ 0900000000 trước 15/10/2026 10:30.", html);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithoutBranchPhone_AsksToRespondBeforeDeadline(string? phone)
        {
            var html = MailKitEmailService.BuildFeedbackNoticeHtml(phone, Han, Now);

            Assert.Contains("Nếu thấy sai sót, vui lòng phản hồi trước 15/10/2026 10:30.", html);
            Assert.DoesNotContain("vui lòng trước", html);
        }

        [Fact]
        public void NoDeadlineOrPastDeadline_IsEmpty()
        {
            Assert.Equal(string.Empty, MailKitEmailService.BuildFeedbackNoticeHtml("0900000000", null, Now));
            Assert.Equal(string.Empty, MailKitEmailService.BuildFeedbackNoticeHtml("0900000000", Now.AddMinutes(-1), Now));
        }

        [Fact]
        public void BranchPhone_IsHtmlEncoded()
        {
            var html = MailKitEmailService.BuildFeedbackNoticeHtml("<b>09</b>", Han, Now);

            Assert.Contains("&lt;b&gt;09&lt;/b&gt;", html);
            Assert.DoesNotContain("<b>", html);
        }
    }
}
