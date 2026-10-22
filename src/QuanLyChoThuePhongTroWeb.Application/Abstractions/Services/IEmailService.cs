using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.Emails.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Services
{
    public interface IEmailService
    {
        Task<(bool IsSuccess, string ErrorMessage)> SendViewingStatusNoticeAsync(
            QuanLyChoThuePhongTroWeb.Application.Features.Viewings.ViewingAdminRequestDto notice) =>
            Task.FromResult((IsSuccess: false, ErrorMessage: "Dịch vụ email chưa hỗ trợ thông báo lịch xem."));
        Task<(bool IsSuccess, string ErrorMessage)> SendInvoiceEmailAsync(string toEmail, HoaDonChiTietRes hoaDon, byte[] pdfBytes);
        Task<(bool IsSuccess, string ErrorMessage)> SendContractExpiryAlertAsync(string toEmail, string tenNguoiNhan, ContractExpiryAlertData alertData);
        Task<(bool IsSuccess, string ErrorMessage)> SendRoomDepositedNoticeAsync(
            string toEmail, string recipientName, string roomNumber, string branchName) =>
            Task.FromResult((IsSuccess: false,
                ErrorMessage: "Dịch vụ email chưa hỗ trợ thông báo đặt cọc phòng."));
        Task<(bool IsSuccess, string ErrorMessage)> SendPaymentReceiptAsync(string toEmail, PaymentReceiptEmail data) =>
            Task.FromResult((IsSuccess: false,
                ErrorMessage: "Dịch vụ email chưa hỗ trợ gửi biên lai thanh toán."));
    }
}
