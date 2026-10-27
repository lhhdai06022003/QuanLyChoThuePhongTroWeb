# Module: Thanh toán trực tuyến (Online Payments)

- **Phạm vi:** Thanh toán Hóa đơn và Đặt cọc giữ chỗ qua 2 cổng thanh toán trực tuyến.
- **Nối tiếp:** [thanh toán hóa đơn](../thanh-toan-hoa-don/spec.md) và [khách vãng lai, giữ chỗ, cọc và hoàn tiền](../khach-vang-lai-giu-cho/README.md).
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen`.
- **Kiến trúc dùng chung:**
  - Ghi sổ tự động, chống trùng tuyệt đối theo tiền tố `MaGiaoDich` (`SEPAY-`, `VNPAY-`).
  - Tự động ghi nhận khoản tiền giữ chỗ về trễ (yêu cầu đã hủy/hết hạn) với ghi chú `LATE_REFUND_REQUIRED` để nhân viên hoàn trả.
  - Tự động phát sự kiện SignalR thời gian thực (`HoaDonThanhToanCapNhat`, `GiuChoThanhToanCapNhat`) giúp trình duyệt của khách tự động cập nhật ngay khi tiền về.
  - Thông báo đa kênh: thông báo trong app, Telegram (nhóm chung và nhóm theo chi nhánh), email biên nhận/xác nhận.

## Hai cổng thanh toán

| Cổng thanh toán | Phương thức & Kênh | Phạm vi hỗ trợ | Hiện trạng |
| :--- | :--- | :--- | :--- |
| [SePay](sepay/README.md) | Chuyển khoản VietQR động theo số còn lại; Webhook `POST /api/webhooks/payment/sepay` (HMAC-SHA256); Đồng bộ bù API v2 | Hóa đơn (`TTPTHD{Id}`) & Giữ chỗ (`TTPTGC{Id}`) | Hoàn thành; SePay Test mode và kịch bản thử tay TC01–TC05, G01–G03 đã sẵn sàng |
| [VNPay](vnpay/README.md) | Thẻ ATM nội địa, thẻ quốc tế Visa/MasterCard, VNPAY-QR; IPN `GET /api/webhooks/payment/vnpay-ipn` (HMAC-SHA512); Trang kết quả chỉ đọc | Hóa đơn & Giữ chỗ | Hoàn thành; Sandbox V01–V07, G04–G07 đã xác nhận PASS |

## Cấu trúc tài liệu

- [`sepay/`](sepay/README.md): Đặc tả ([spec.md](sepay/spec.md)), kế hoạch ([plan.md](sepay/plan.md)), đánh giá kiểm thử ([review.md](sepay/review.md)), và tổng quan ([README.md](sepay/README.md)) cho cổng SePay.
- [`vnpay/`](vnpay/README.md): Đặc tả ([spec.md](vnpay/spec.md)), kế hoạch ([plan.md](vnpay/plan.md)), đánh giá kiểm thử ([review.md](vnpay/review.md)), và tổng quan ([README.md](vnpay/README.md)) cho cổng VNPay.
- [`huong-dan-thiet-lap.md`](huong-dan-thiet-lap.md): Hướng dẫn thiết lập môi trường thử nghiệm và các kịch bản thử tay cho cả SePay và VNPay.
