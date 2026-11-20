# Lịch job nền không cần sổ "đã gửi"

Người dùng duyệt trong phiên 20/11/2026, trước lần deploy Azure đầu tiên. Nhánh `feature/bo-file-trang-thai-job`. Không đổi schema.

## Vì sao

`InvoiceReminderJob` và `ContractExpiryAlertJob` từng ghi "đã gửi" vào `sent_invoice_reminders.json` và `sent_contract_alerts.json`. Trên Azure App Service, hai file phải nằm ở `/home` (biến `BackgroundJobs__StatePath`); mất file thì gửi lại thông báo cũ. Mục tiêu: bỏ hẳn hai file mà không gửi trùng.

## Quyết định

| Việc | Trước | Sau |
| --- | --- | --- |
| Lịch chạy | Nhắc hóa đơn: lúc khởi động rồi mỗi giờ. Cảnh báo hợp đồng: lúc khởi động rồi 8:00 mỗi ngày | Cả hai: **chỉ 8:00 giờ VN mỗi ngày**, không chạy ngay khi khởi động |
| Nhắc hóa đơn quá hạn | Một email cho mỗi hóa đơn (sổ JSON) | Email kèm PDF vào ngày quá hạn `OverdueDays` (mặc định 5), rồi mỗi `RepeatEveryDays` ngày (mặc định 5) tới khi trả: 5, 10, 15... `RepeatEveryDays = 0` thì chỉ nhắc ở mốc đầu. Số ngày tính theo ngày giờ VN |
| Email hợp đồng còn `AlertDays` ngày | Đúng ngày, chống lặp bằng sổ | Đúng ngày, không cần sổ |
| Thông báo app "hợp đồng đã hết hạn" | Hết hạn ≤ hôm nay, một lần (sổ) | **Chỉ đúng ngày hết hạn** (giờ VN). Hết hạn từ trước hoặc ngày job không chạy thì không báo; KH4 "Hợp đồng đã quá hạn chưa xử lý" của Việc cần làm giữ lời nhắc |
| Lượt bị lỗi | Hóa đơn: một giờ sau quét lại | Lỗi cả lượt (DB, mạng) thử lại mỗi 10 phút tới 10:00 giờ VN, quá thì bỏ lượt. Use case đọc hết dữ liệu trước khi gửi, nên lỗi đọc DB không gây gửi trùng khi thử lại. Email gửi lỗi cho từng khách không thử lại trong ngày |

## Đánh đổi đã chấp nhận

- Web không chạy đúng lúc 8:00 (đang deploy, sập) thì bỏ lượt hôm đó: hóa đơn còn mốc sau, email mốc hợp đồng và thông báo hết hạn của hôm đó mất hẳn; Việc cần làm vẫn hiện.
- Chỉ chạy một instance; cần Always On.
- Thử tay trên máy phải chờ tới 8:00 (không có nút chạy ngay).

## Phạm vi code

- Infrastructure: `BackgroundJobs/DailyJobSchedule.cs` (tính giờ chạy, thử lại), `DailyBackgroundJob.cs` (khung job), hai job kế thừa. Bỏ `BackgroundJobs/State/*`.
- Application: bỏ `IInvoiceReminderStateStore`, `IContractExpiryAlertStateStore`; `InvoiceReminderUseCase` nhận `TimeProvider`; `AutoReminderSettings.RepeatEveryDays`.
- Cấu hình: bỏ `BackgroundJobs:StatePath` (`appsettings.example.json`, `.env.example`, tài liệu Azure).
- Test: `BackgroundJobUseCaseTests` (mốc, ngày VN), `DailyJobScheduleTests`, hai test khởi động job dùng đồng hồ đứng ở 07:59:59.8 giờ VN.
