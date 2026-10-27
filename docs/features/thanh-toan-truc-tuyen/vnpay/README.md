# Cổng thanh toán trực tuyến: VNPay (ATM / Visa / MasterCard / QR)

- **Thuộc module:** [thanh toán trực tuyến](../README.md).
- **Phạm vi áp dụng:** Thanh toán Hóa đơn và Đặt cọc giữ chỗ.
- **Nhánh Git:** `feature/thanh-toan-truc-tuyen`.
- **Cơ chế hoạt động:** Thanh toán thẻ nội địa, thẻ quốc tế hoặc VNPAY-QR qua cổng thanh toán VNPay. IPN `GET /api/webhooks/payment/vnpay-ipn` (HMAC-SHA512) tự động ghi sổ, chống trùng khóa `VNPAY-{vnp_TransactionNo}`. Trang kết quả (ReturnUrl) chỉ đọc trạng thái, không trực tiếp ghi sổ.

## 1. Hiện trạng triển khai

1. **Hóa đơn:**
   - Panel thanh toán khách: Tab "Thẻ ATM / Visa / MasterCard (VNPay)" khi `VnPay:Enabled = true`.
   - `POST /KhachThue/HoaDon/ThanhToan/VnPay/{hoaDonId}`: Tạo link thanh toán VNPay với số tiền cố định bằng số còn lại, kiểm tra hóa đơn trước khi tạo link.
   - `GET /KhachThue/HoaDon/ThanhToan/VnPay/KetQua`: Trang kết quả chỉ đọc hiển thị trạng thái (Đã ghi nhận / Đang ghi nhận / Không thành công / Đã hủy / Không xác thực).
   - `GET /api/webhooks/payment/vnpay-ipn`: Xác thực chữ ký HMAC-SHA512, ghi sổ vào `LichSuThanhToan`, phát SignalR `HoaDonThanhToanCapNhat`, thông báo Telegram/app và email biên lai.
   - Màn quản lý: Badge "VNPay" kèm mã giao dịch `TXN {mã}` trong chi tiết hóa đơn.
2. **Đặt cọc giữ chỗ:**
   - Trang đặt cọc (`/tai-khoan/yeu-cau-phong/giu-cho/{id}/dat-coc`): Tab "Thẻ ATM / Visa (VNPay)" hiển thị khi `VnPay:Enabled = true` và mã thanh toán dạng `TTPTGC`.
   - `POST /tai-khoan/yeu-cau-phong/giu-cho/{reservationId}/dat-coc/vnpay`: Tạo link thanh toán VNPay cho giữ chỗ với mã tham chiếu `vnp_TxnRef = GC{id}_{ticks}` và nội dung không dấu `Thanh toan giu cho phong {SoPhong}`.
   - `GET /tai-khoan/yeu-cau-phong/giu-cho/dat-coc/vnpay/ket-qua`: Trang kết quả chỉ đọc cho giữ chỗ. Khi trạng thái "Đang ghi nhận", script tự lắng nghe SignalR `GiuChoThanhToanCapNhat` để tự động tải lại trang.
   - IPN tự động nhận diện giao dịch giữ chỗ qua tiền tố `GC`, ghi sổ vào `giao_dich_giu_cho` với khóa `VNPAY-{vnp_TransactionNo}`, hỗ trợ đánh dấu `LATE_REFUND_REQUIRED` nếu tiền về sau khi yêu cầu đã hủy/hết hạn.
   - Màn quản lý: Chi tiết giữ chỗ có bảng lịch sử giao dịch với badge `VNPay` và người xác nhận "Hệ thống".

## 2. Tính năng đã hoàn thành

- [x] Tích hợp tab thanh toán VNPay trên cả Panel hóa đơn và Trang đặt cọc giữ chỗ.
- [x] Tạo URL chuyển sang cổng VNPay an toàn (chuẩn hóa tham số, không dấu, chống số tiền lẻ, mã hóa URL).
- [x] IPN xử lý tập trung cho cả hóa đơn và giữ chỗ: HMAC-SHA512, khóa chống trùng `VNPAY-`, fail-closed khi lỗi.
- [x] Trang kết quả chỉ đọc, bảo mật, không dựa vào tham số query để ghi sổ.
- [x] Script frontend (`khachthue-vnpay.js` và `guest-dat-coc.js`) tuân thủ nghiêm ngặt kiểm tra tĩnh: chống bấm đúp, kiểm tra `https://`, không nối chuỗi HTML, nghe SignalR cập nhật tự động.
- [x] Đầy đủ thông báo app, Telegram (cảnh báo tiền về/tiền cần hoàn), email xác nhận.
- [x] Thử nghiệm sandbox V01–V07 PASS (26/10/2026), toàn bộ 87 Web integration tests PASS.

## 3. Hồ sơ và tài liệu

- [`spec.md`](spec.md): Đặc tả kỹ thuật cổng VNPay (Hóa đơn và Giữ chỗ).
- [`plan.md`](plan.md): Kế hoạch triển khai chia theo 2 phần và các quyết định thiết kế.
- [`review.md`](review.md): Báo cáo review, kiểm thử và đối chiếu sandbox.
- [`../huong-dan-thiet-lap.md`](../huong-dan-thiet-lap.md): Hướng dẫn thiết lập sandbox VNPay, kịch bản thử tay V01–V07 cho hóa đơn và G04–G07 cho giữ chỗ.
