using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes
{
    // Chỉ quan tâm email báo tiền giữ chỗ; hai hàm bắt buộc còn lại không được gọi.
    public class RecordingReservationNoticeEmailService : IEmailService
    {
        public List<(string ToEmail, ReservationPaymentNoticeEmail Data)> NoticeCalls { get; } = new();
        public (bool IsSuccess, string ErrorMessage) Result { get; set; } = (true, string.Empty);
        public bool Throw { get; set; }

        public Task<(bool IsSuccess, string ErrorMessage)> SendReservationPaymentNoticeAsync(string toEmail, ReservationPaymentNoticeEmail data)
        {
            NoticeCalls.Add((toEmail, data));

            if (Throw)
            {
                throw new InvalidOperationException("SMTP down");
            }

            return Task.FromResult(Result);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes)
            => throw new NotSupportedException();

        public Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData)
            => throw new NotSupportedException();
    }
}
