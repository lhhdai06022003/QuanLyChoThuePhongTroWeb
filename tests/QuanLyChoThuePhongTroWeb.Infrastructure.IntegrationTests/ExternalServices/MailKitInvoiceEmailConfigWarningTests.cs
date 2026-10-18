using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Emails;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    // Thiếu OnlinePayment:PublicBaseUrl: email hóa đơn không có nút và chỉ log Warning một lần mỗi process (spec thanh toán trực tuyến §7.1).
    // SMTP trỏ vào cổng đóng ở máy cục bộ nên không gửi thư thật; việc gửi thất bại sau bước dựng nội dung không ảnh hưởng điều cần kiểm.
    // Cờ "đã cảnh báo" là static theo process, nên chỉ một test trong lớp này được phép cạn cờ (xem TheWarningIsLoggedOncePerProcess...).
    public class MailKitInvoiceEmailConfigWarningTests
    {
        private const string WarningText = "OnlinePayment:PublicBaseUrl trống";

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }

        private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailSettings:SmtpServer"] = "127.0.0.1",
            ["EmailSettings:Port"] = "1",
            ["EmailSettings:SenderEmail"] = "tro@example.test",
            ["EmailSettings:Password"] = "khong-phai-mat-khau-that",
            ["VietQRSettings:BankId"] = "MB",
            ["VietQRSettings:AccountNumber"] = "0123456789",
            ["VietQRSettings:AccountName"] = "NHA TRO"
        }).Build();

        private static HoaDonChiTietRes Invoice() => new()
        {
            HoaDonId = 7,
            MaHoaDon = "HD-7",
            TenNguoiThue = "Nguyen Van A",
            TenChiNhanh = "Chi nhanh 1",
            TongTien = 2_000_000m,
            ConLai = 2_000_000m
        };

        [Fact]
        public async Task TheWarningIsLoggedOncePerProcess_WhenThePublicBaseUrlIsBlank()
        {
            var logger = new CapturingLogger<MailKitEmailService>();
            var service = new MailKitEmailService(Config(), logger, new OnlinePaymentOptions { PublicBaseUrl = "  " });

            await service.SendInvoiceEmailAsync("khach@example.test", Invoice(), Array.Empty<byte>());
            await service.SendInvoiceEmailAsync("khach@example.test", Invoice(), Array.Empty<byte>());
            await new MailKitEmailService(Config(), logger, new OnlinePaymentOptions()).SendInvoiceEmailAsync("khach@example.test", Invoice(), Array.Empty<byte>());

            var warnings = logger.Entries.Where(e => e.Message.Contains(WarningText)).ToList();
            Assert.Single(warnings);
            Assert.Equal(LogLevel.Warning, warnings[0].Level);
            Assert.DoesNotContain("khach@example.test", warnings[0].Message);
        }

        [Fact]
        public async Task WithAPublicBaseUrl_NoWarningAboutItIsLogged()
        {
            var logger = new CapturingLogger<MailKitEmailService>();
            var service = new MailKitEmailService(Config(), logger, new OnlinePaymentOptions { PublicBaseUrl = "https://tro.test" });

            await service.SendInvoiceEmailAsync("khach@example.test", Invoice(), Array.Empty<byte>());

            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains(WarningText));
        }
    }
}
