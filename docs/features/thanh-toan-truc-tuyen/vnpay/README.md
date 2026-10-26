# Thanh toán trực tuyến — VNPay (đợt 2)

- **Thuộc module:** [thanh toán trực tuyến](../README.md).
- **Nối tiếp:** [thanh toán trực tuyến đợt 1](../sepay/README.md) (VietQR + SePay), spec đợt 1 §12.
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen` (người dùng chọn làm tiếp trên nhánh này, 25/10/2026).
- **Hiện trạng:** Phần 1 (backend: cấu hình, bộ ký, IPN, ghi sổ, thông báo, chặn ghi trùng) xong 26/10/2026, reviewer CHOT, toàn solution PASS. Phần 2 (tab trong panel khách, tạo link, trang kết quả, badge màn quản lý, hướng dẫn thiết lập) xong 26/10/2026 (reviewer CHOT; toàn solution PASS: Domain 135, Application 2064, Infrastructure 621, Web 657; `has-pending-model-changes` PASS). Sandbox V01–V07 PASS 26/10/2026 (V03–V06 theo người dùng báo, xem review.md mục 7); giao diện 375 px sáng/tối đã kiểm trên trình duyệt giả lập. Phần 1 commit `74e5c81`, phần 2 commit `0f326ad`. Sau sandbox (commit `3de7181`): IPN thành công cho hóa đơn không có trong DB trả `01` (T2); mã nhập tay mang tiền tố `VNPAY-`/`SEPAY-` bị từ chối (T4); `VnPay:PaymentUrl` không được chứa `?`/`#`; bảng danh sách hóa đơn khách cuộn ngang trong khung để modal không bị bóp ở 375 px. Toàn bộ test PASS trên DB test mới: Domain 135, Application 2092, Infrastructure 621, Web 658. Để sau: tab đang chọn khó phân biệt, DataTables trang hóa đơn khách còn tiếng Anh. Chưa push.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [x] Tab "Thẻ ATM / Visa / MasterCard (VNPay)" trong panel thanh toán của khách thuê; QR chuyển khoản vẫn là mặc định.
- [x] Tạo link VNPay với số tiền cố định bằng số còn lại, kiểm lại hóa đơn lúc bấm.
- [x] IPN `GET /api/webhooks/payment/vnpay-ipn`: HMAC-SHA512, khóa chống trùng `VNPAY-{vnp_TransactionNo}`, ghi sổ dùng chung với SePay.
- [x] Trang kết quả (ReturnUrl) chỉ hiển thị trạng thái, không ghi sổ.
- [x] Thông báo trong app, SignalR, Telegram, email biên lai như SePay; cảnh báo Telegram "cần hoàn tiền" khi hóa đơn không còn nhận tiền.
- [x] Nhãn VNPay trên biên lai, lịch sử thanh toán; chặn ghi trùng ở luồng thủ công. (Phần 1: nhãn biên lai/email, DTO `LaVnPay`, chặn ghi trùng; phần 2: badge "VNPay" + `TXN {mã}` ở chi tiết hóa đơn quản lý.)
- [x] Hướng dẫn thiết lập VNPay sandbox trong [`../huong-dan-thiet-lap.md`](../huong-dan-thiet-lap.md).
- [x] Thử tay V01–V07 trên sandbox (người dùng, 26/10/2026).

## Hồ sơ triển khai

- `spec.md`: đặc tả, đã duyệt 25/10/2026.
- `plan.md`: chia 2 phần `/ship` và các quyết định thiết kế.
- [`review.md`](review.md): review, test, đối chiếu spec/tài liệu và rủi ro còn mở (26/10/2026).
