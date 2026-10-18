namespace QuanLyChoThuePhongTroWeb.Application.Features.OnlinePayments;

// Một việc trong hàng đợi hậu xử lý: gửi Telegram và email biên lai cho một kết quả xử lý giao dịch.
public sealed record PaymentNotificationWorkItem(TransferOutcome Outcome);
