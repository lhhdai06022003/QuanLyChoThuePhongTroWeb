using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.Emails
{
    // Gửi email tạm tính ở nền sau khi request trả về. Dùng DI scope riêng, không đụng DbContext của request.
    public class BackgroundProvisionalInvoiceEmailDispatcher : IProvisionalInvoiceEmailDispatcher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundProvisionalInvoiceEmailDispatcher> _logger;

        public BackgroundProvisionalInvoiceEmailDispatcher(
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundProvisionalInvoiceEmailDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Enqueue(IReadOnlyList<PendingProvisionalEmail> emails)
        {
            if (emails == null || emails.Count == 0)
            {
                return;
            }

            var copy = emails.ToArray();
            _ = Task.Run(() => SendAllAsync(copy));
        }

        private async Task SendAllAsync(PendingProvisionalEmail[] emails)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var sent = 0;
                foreach (var e in emails)
                {
                    try
                    {
                        var result = await emailService.SendProvisionalInvoiceEmailAsync(e.ToEmail, e.Data);
                        if (result.IsSuccess)
                        {
                            sent++;
                        }
                        else
                        {
                            _logger.LogWarning("Không gửi được email tạm tính hóa đơn {HoaDonId}: {Error}", e.HoaDonId, result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi khi gửi email tạm tính hóa đơn {HoaDonId}.", e.HoaDonId);
                    }
                }

                _logger.LogInformation("Đã gửi nền {Sent}/{Total} email hóa đơn tạm tính.", sent, emails.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi nền email hóa đơn tạm tính.");
            }
        }
    }
}
