# Cổng thanh toán trực tuyến: SePay (VietQR + Webhook)

- **Thuộc module:** [thanh toán trực tuyến](../README.md).
- **Phạm vi áp dụng:** Thanh toán Hóa đơn (`TTPTHD{Id}`) và Đặt cọc giữ chỗ (`TTPTGC{Id}`).
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen`.
- **Cơ chế hoạt động:** Chuyển khoản ngân hàng qua mã VietQR động, SePay webhook `POST /api/webhooks/payment/sepay` (xác thực HMAC-SHA256) tự động ghi sổ tức thì, chống trùng khóa `SEPAY-{ref}` / `SEPAY-ID-{id}`.

## 1. Hiện trạng triển khai

1. **Hóa đơn:**
   - Panel thanh toán cổng khách thuê: mã VietQR theo số tiền còn nợ, tải ảnh QR, sao chép 1 chạm, deeplink 12 app ngân hàng.
   - Webhook tự động ghi sổ hóa đơn, chuyển trạng thái `DaThanhToan` / `ThanhToanMotPhan`, cập nhật realtime qua SignalR `HoaDonThanhToanCapNhat`, xuất biên lai web in A5.
   - Gửi thông báo trong ứng dụng, Telegram (chat chung & theo chi nhánh), email biên lai cho khách.
   - Cổng quản lý: màn Giao dịch SePay (`/QuanLyNhaTro/GiaoDichSePay`) đồng bộ bù qua API v2 (mặc định chỉ đối chiếu `SePay:SyncRecordEnabled = false`), nút Biên lai và Gửi lại biên lai.
2. **Đặt cọc giữ chỗ:**
   - Trang đặt cọc (`/tai-khoan/yeu-cau-phong/giu-cho/{id}/dat-coc`): Tab "Chuyển khoản QR" sinh mã VietQR động với cú pháp `TTPTGC{Id}`, nút tải ảnh QR, copy nhanh thông tin tài khoản và lưới deeplink 12 app ngân hàng.
   - Webhook tự động ghi nhận vào `giao_dich_giu_cho` khi cờ `OnlinePayment:ReservationEnabled = true`.
   - Cập nhật số tiền đã nhận và trạng thái (`DangGiuCho` khi đủ tiền, `ChoXacNhanTien` khi nhận một phần).
   - Khoản tiền về sau khi yêu cầu đã hủy/hết hạn: tự động ghi nhận với ghi chú `LATE_REFUND_REQUIRED` (cần hoàn lại) để nhân viên xử lý hoàn tiền.
   - Bắn sự kiện SignalR `GiuChoThanhToanCapNhat` giúp trang đặt cọc tự động tải lại mà không cần bấm F5.
   - Cảnh báo Telegram tiền giữ chỗ đã nhận / cần hoàn và gửi email xác nhận cho khách.
   - Màn Giao dịch SePay tự động đối chiếu dòng giữ chỗ, hiển thị `Đã có` và link tới `/QuanLyNhaTro/GiuCho/ChiTiet/{id}`.

## 2. Tính năng đã hoàn thành

- [x] Panel thanh toán Hóa đơn ở cổng khách thuê: VietQR, tải ảnh QR, deeplink 12 ngân hàng, sao chép 1 chạm.
- [x] Tab Chuyển khoản QR trên trang Đặt cọc giữ chỗ: VietQR động, tải ảnh QR, copy 1 chạm, deeplink 12 ngân hàng.
- [x] Webhook SePay (HMAC-SHA256) tự ghi sổ cho cả `TTPTHD` và `TTPTGC`, chống trùng qua tiền tố `SEPAY-`.
- [x] Tự động xử lý khoản giữ chỗ về trễ thành `LATE_REFUND_REQUIRED`.
- [x] Chặn ghi trùng khi nhân viên xác nhận minh chứng cũ hoặc thu thủ công với mã đã có trong hệ thống.
- [x] Đồng bộ bù qua API SePay (Admin) trên màn Giao dịch SePay; đối chiếu cả dòng hóa đơn và giữ chỗ.
- [x] Tự động cập nhật thời gian thực qua SignalR: `HoaDonThanhToanCapNhat` và `GiuChoThanhToanCapNhat`.
- [x] Thông báo đa kênh: Trong app, Telegram (chung & theo chi nhánh), email biên lai/xác nhận.
- [x] Hướng dẫn thiết lập SePay Test mode, Telegram, Gmail, ngrok, kịch bản thử tay trong [`../huong-dan-thiet-lap.md`](../huong-dan-thiet-lap.md).

## 3. Hồ sơ và tài liệu

- [`spec.md`](spec.md): Đặc tả kỹ thuật cổng SePay (Hóa đơn và Giữ chỗ).
- [`plan.md`](plan.md): Kế hoạch thực thi và các quyết định thiết kế.
- [`review.md`](review.md): Kết quả kiểm thử, review code và bằng chứng xác nhận.
- [`../huong-dan-thiet-lap.md`](../huong-dan-thiet-lap.md): Hướng dẫn thiết lập và các kịch bản thử nghiệm SePay (TC01–TC05 cho hóa đơn, G01–G03 cho giữ chỗ).
