# Module: Thanh toán trực tuyến hóa đơn

- **Nối tiếp:** [thanh toán hóa đơn](../thanh-toan-hoa-don/spec.md) (lượt + minh chứng + đối chiếu tay).
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen`.
- **Hiện trạng:** Spec đợt 1 đã duyệt (16/10/2026). Đợt 1 chia 5 phần, mỗi phần một lượt `/ship`. **Phần 1 xong** (16/10/2026, commit `6b690e4`, review CHOT): webhook SePay `POST /api/webhooks/payment/sepay` tự ghi sổ hóa đơn, mặc định tắt (`SePay:Enabled=false`). **Phần 2 xong** (17–18/10/2026, review CHOT, chưa thử tay Telegram/email thật): sau khi ghi sổ có thông báo trong app (Admin, nhân viên chi nhánh, khách), sự kiện SignalR `HoaDonThanhToanCapNhat` cho khách, tin Telegram (chat chung + chat theo chi nhánh), email biên lai và cảnh báo Telegram cho giao dịch cần xử lý tay, qua hàng đợi trong process và worker luôn đăng ký; xác nhận minh chứng cũ và thu thủ công bị chặn ghi trùng với dòng SePay đã ghi (§5.4). Telegram mặc định tắt (`Telegram:Enabled=false`); email biên lai gửi được khi cấu hình SMTP và `NguoiThue.Email` hợp lệ. **Phần 3 xong** (18/10/2026, review CHOT): cổng khách thuê có panel thanh toán (QR theo hóa đơn và số còn lại, tải ảnh QR, lưới deeplink 12 ngân hàng (Sacombank và MSB không có trong danh sách deeplink VietQR nên thay bằng HDBank, VIB), sao chép 1 chạm, nút "Tôi đã chuyển khoản, kiểm tra lại"), panel tự cập nhật qua SignalR `HoaDonThanhToanCapNhat`, biên lai web in được cho khách (`/KhachThue/HoaDon/BienLai/{id}`) và quản lý (`/QuanLyNhaTro/HoaDon/BienLai/{id}`), đã gỡ khách tạo lượt, hủy lượt và nộp minh chứng. Test tự động đã chạy; **chưa thử** deeplink trên điện thoại thật (TC04) và chưa thử panel đổi sang biên lai bằng SePay Test mode (TC02). **Phần 4 xong** (18/10/2026, test tự động đã chạy, **chưa thử tay**): màn Giao dịch SePay (`/QuanLyNhaTro/GiaoDichSePay`, chỉ Admin) đồng bộ bù qua API v2, mặc định chỉ đối chiếu vì cờ `SePay:SyncRecordEnabled = false` (chỉ bật ghi sổ sau khi kiểm `referenceCode` = `reference_number` trên Test mode) và không cảnh báo Telegram; nút Biên lai và Gửi lại biên lai (quyền `InvoiceSend`) cùng nhãn SePay và mã tham chiếu trong lịch sử thanh toán của chi tiết hóa đơn; email hóa đơn có nút "Thanh toán trực tuyến" và nội dung chuyển khoản `TTPTHD{Id}`; QR trong email, PDF và màn chi tiết hóa đơn quản lý dùng `TTPTHD{Id}` với số còn lại; trang lỗi biên lai thân thiện. Phần 5 chưa làm. Đợt 2 (VNPay) chưa có spec.
- **Schema:** không đổi.

## Tính năng cần hoàn thành — đợt 1

- [x] Panel thanh toán ở cổng khách thuê: VietQR, tải ảnh QR, deeplink 12 ngân hàng, sao chép 1 chạm (phần 3; chưa thử deeplink trên điện thoại thật, TC04).
- [x] Webhook SePay (HMAC-SHA256) tự ghi sổ, chống trùng, xử lý thiếu/dư/sai nội dung (phần 1). Cảnh báo Telegram cho giao dịch sai nội dung làm ở phần 2.
- [x] Chặn ghi trùng khi nhân viên xác nhận minh chứng cũ `DangDoiChieu` (§5.4, phần 2): dòng SePay do hệ thống ghi sau khi khách nộp minh chứng thì nhân viên phải chuyển Admin, Admin phải ghi chú; mã giao dịch (nhân viên nhập, khách khai trên minh chứng, hoặc thu thủ công) trùng một dòng `SEPAY-{mã}` chưa xóa thì báo lỗi.
- [x] Đồng bộ bù qua API SePay (Admin) (phần 4; chưa thử tay với token Test mode, bước T3: kiểm mã tham chiếu và múi giờ trước khi bật `SePay:SyncRecordEnabled`).
- [x] Panel tự đổi sang biên lai qua SignalR; biên lai web in được (phần 3; chưa thử với SePay Test mode, TC02).
- [x] Email biên lai, Telegram (chat chung + theo chi nhánh), thông báo trong app (phần 2). Nút "Xem biên lai" trong email trỏ `/KhachThue/HoaDon/BienLai/{id}` (đổi ở phần 3); nút gửi lại biên lai làm ở phần 4.
- [x] Email hóa đơn có nút thanh toán dẫn vào cổng khách thuê; QR trong PDF dùng mã `TTPTHD{Id}` (phần 4; chưa thử tay, T1).
- [x] Gỡ tạo lượt và nộp minh chứng phía khách (phần 3). Service `InvoicePaymentRequestService` vẫn giữ các method cũ, dọn ở đợt sau.
- [ ] Hướng dẫn thiết lập SePay Test mode, bot Telegram, tunnel.

## Hồ sơ triển khai

- `spec.md`: đặc tả đợt 1, đã duyệt (16/10/2026).
- Kế hoạch và đánh giá từng phần do `/ship` sinh ở `.bangiao/` (không lưu Git), mỗi lượt chạy ghi đè lượt trước.
