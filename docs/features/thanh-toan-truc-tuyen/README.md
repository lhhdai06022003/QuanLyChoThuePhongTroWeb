# Module: Thanh toán trực tuyến hóa đơn

- **Nối tiếp:** [thanh toán hóa đơn](../thanh-toan-hoa-don/spec.md) (lượt + minh chứng + đối chiếu tay).
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen`.
- **Hiện trạng:** Đợt 1 (VietQR + SePay) có spec v1 chờ người dùng duyệt (16/10/2026); chưa có plan, chưa code. Đợt 2 (VNPay) chưa có spec.
- **Schema:** không đổi.

## Tính năng cần hoàn thành — đợt 1

- [ ] Panel thanh toán ở cổng khách thuê: VietQR, tải ảnh QR, deeplink 12 ngân hàng, sao chép 1 chạm.
- [ ] Webhook SePay (HMAC-SHA256) tự ghi sổ, chống trùng, xử lý thiếu/dư/sai nội dung.
- [ ] Đồng bộ bù qua API SePay (Admin).
- [ ] Panel tự đổi sang biên lai qua SignalR; biên lai web in được.
- [ ] Email biên lai, Telegram (chat chung + theo chi nhánh), thông báo trong app.
- [ ] Email hóa đơn có nút thanh toán dẫn vào cổng khách thuê; QR trong PDF dùng mã `TTPTHD{Id}`.
- [ ] Gỡ tạo lượt và nộp minh chứng phía khách.
- [ ] Hướng dẫn thiết lập SePay Test mode, bot Telegram, tunnel.

## Hồ sơ triển khai

- `spec.md`: đặc tả đợt 1, v1 chờ duyệt.
- `plan.md`: viết sau khi spec được duyệt.
- `review.md`: khi có mã triển khai.
