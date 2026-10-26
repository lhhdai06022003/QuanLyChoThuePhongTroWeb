# Module: Thanh toán hóa đơn qua VNPay (đợt 2 thanh toán trực tuyến)

- **Nối tiếp:** [thanh toán trực tuyến đợt 1](../thanh-toan-truc-tuyen/README.md) (VietQR + SePay), spec đợt 1 §12.
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen` (người dùng chọn làm tiếp trên nhánh này, 25/10/2026).
- **Hiện trạng:** Phần 1 (backend: cấu hình, bộ ký, IPN, ghi sổ, thông báo, chặn ghi trùng) xong 26/10/2026, reviewer CHOT, toàn solution PASS. Phần 2 (cổng khách thuê, badge màn quản lý, tài liệu) chưa bắt đầu. Chưa thử trên sandbox VNPay.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [ ] Tab "Thẻ ATM / Visa / MasterCard (VNPay)" trong panel thanh toán của khách thuê; QR chuyển khoản vẫn là mặc định.
- [ ] Tạo link VNPay với số tiền cố định bằng số còn lại, kiểm lại hóa đơn lúc bấm.
- [x] IPN `GET /api/webhooks/payment/vnpay-ipn`: HMAC-SHA512, khóa chống trùng `VNPAY-{vnp_TransactionNo}`, ghi sổ dùng chung với SePay.
- [ ] Trang kết quả (ReturnUrl) chỉ hiển thị trạng thái, không ghi sổ.
- [x] Thông báo trong app, SignalR, Telegram, email biên lai như SePay; cảnh báo Telegram "cần hoàn tiền" khi hóa đơn không còn nhận tiền.
- [ ] Nhãn VNPay trên biên lai, lịch sử thanh toán; chặn ghi trùng ở luồng thủ công. (Phần 1 xong: nhãn biên lai/email, DTO `LaVnPay`, chặn ghi trùng; còn badge VNPay ở màn chi tiết hóa đơn quản lý — phần 2.)
- [ ] Hướng dẫn thiết lập VNPay sandbox trong `../thanh-toan-truc-tuyen/huong-dan-thiet-lap.md`.
- [ ] Thử tay V01–V07 trên sandbox (người dùng).

## Hồ sơ triển khai

- `spec.md`: đặc tả, đã duyệt 25/10/2026.
- `plan.md`: chia 2 phần `/ship` và các quyết định thiết kế.
- `review.md`: viết khi có code để review.
