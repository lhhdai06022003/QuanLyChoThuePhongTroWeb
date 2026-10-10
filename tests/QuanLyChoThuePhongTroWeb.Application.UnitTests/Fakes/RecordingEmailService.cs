using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    public class RecordingEmailService : IEmailService
    {
        public List<(string ToEmail, ProvisionalInvoiceEmailData Data)> ProvisionalCalls { get; } = new();
        public bool ThrowOnProvisional { get; set; }
        public bool FailOnProvisional { get; set; }
        public List<string>? Log { get; set; }

        public Task<(bool IsSuccess, string ErrorMessage)> SendProvisionalInvoiceEmailAsync(string toEmail, ProvisionalInvoiceEmailData data)
        {
            Log?.Add("Email");
            if (ThrowOnProvisional)
            {
                throw new InvalidOperationException("SMTP lỗi");
            }

            ProvisionalCalls.Add((toEmail, data));
            return Task.FromResult(FailOnProvisional ? (false, "SMTP từ chối") : (true, string.Empty));
        }

        public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            => throw new NotSupportedException();

        public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
            => throw new NotSupportedException();
    }
}
