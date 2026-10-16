# Module: Thanh toán trực tuyến hóa đơn

- **Nối tiếp:** [thanh toán hóa đơn](../thanh-toan-hoa-don/spec.md) (lượt + minh chứng + đối chiếu tay).
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen`.
- **Hiện trạng:** Spec đợt 1 đã duyệt (16/10/2026). Đợt 1 chia 5 phần, mỗi phần một lượt `/ship`. **Phần 1 xong** (16/10/2026, commit `6b690e4`, review CHOT): webhook SePay `POST /api/webhooks/payment/sepay` tự ghi sổ hóa đơn, mặc định tắt (`SePay:Enabled=false`). Chưa bật ở môi trường thật trước khi xong phần 2 (thông báo, chặn ghi trùng §5.4). Phần 2–5 chưa làm. Đợt 2 (VNPay) chưa có spec.
- **Schema:** không đổi.

## Tính năng cần hoàn thành — đợt 1

- [ ] Panel thanh toán ở cổng khách thuê: VietQR, tải ảnh QR, deeplink 12 ngân hàng, sao chép 1 chạm.
- [x] Webhook SePay (HMAC-SHA256) tự ghi sổ, chống trùng, xử lý thiếu/dư/sai nội dung (phần 1). Cảnh báo Telegram cho giao dịch sai nội dung thuộc phần 2.
- [ ] Chặn ghi trùng khi nhân viên xác nhận minh chứng cũ `DangDoiChieu` (§5.4, phần 2).
- [ ] Đồng bộ bù qua API SePay (Admin).
- [ ] Panel tự đổi sang biên lai qua SignalR; biên lai web in được.
- [ ] Email biên lai, Telegram (chat chung + theo chi nhánh), thông báo trong app.
- [ ] Email hóa đơn có nút thanh toán dẫn vào cổng khách thuê; QR trong PDF dùng mã `TTPTHD{Id}`.
- [ ] Gỡ tạo lượt và nộp minh chứng phía khách.
- [ ] Hướng dẫn thiết lập SePay Test mode, bot Telegram, tunnel.

## Hồ sơ triển khai

- `spec.md`: đặc tả đợt 1, đã duyệt (16/10/2026).
- Kế hoạch và đánh giá từng phần do `/ship` sinh ở `.bangiao/` (không lưu Git), mỗi lượt chạy ghi đè lượt trước.
