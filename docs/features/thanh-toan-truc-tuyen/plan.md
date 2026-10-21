# Kế hoạch: Thanh toán trực tuyến hóa đơn — đợt 1 (VietQR + SePay)

- **Nhánh:** `feature/thanh-toan-truc-tuyen`. **Spec:** [spec.md](spec.md) (đã duyệt 16/10/2026).
- **Trạng thái:** kế hoạch đã thực hiện xong cả 5 phần (16–18/10/2026); file này được viết sau khi làm xong để lưu lại cách chia phần và các quyết định.
- **Nguồn của file này:** spec, `git log` của nhánh, bản ghi `.bangiao/` của phần 4 và các quyết định người dùng đã chốt trong phiên làm việc. Kế hoạch chi tiết từng phần do planner sinh ra ở `.bangiao/ke-hoach.md`, thư mục này **không nằm trong Git** và mỗi lượt `/ship` ghi đè lượt trước: hiện chỉ còn bản của phần 4. Phần 1–3 không còn bản kế hoạch gốc; các mục dưới đây chỉ ghi những gì commit message và spec còn chứng minh được.

## 1. Nguyên tắc chung (người dùng đã chốt)

- Phạm vi: cổng quản lý và cổng khách thuê. Khách vãng lai làm bởi đồng đội, gộp thanh toán chung sau (spec §4.4).
- Không đổi schema. Kiểm bằng `has-pending-model-changes`.
- Không tạo script phụ trợ (`.ps1` hay file tương tự); thử luồng webhook bằng test xUnit và hướng dẫn lệnh trong tài liệu.
- Không tự thiết lập môi trường: không sửa `appsettings.json`, không điền secret, không tạo bot, tài khoản hay tunnel. Code xong thì liệt kê việc người dùng phải cung cấp.
- Mỗi phần chạy một lượt `/ship` (planner → coder → tester → reviewer, bàn giao qua `.bangiao/`), người dùng xác nhận các điểm hỏi rồi mới commit; không push.
- Test tích hợp chạy trên DB `_test`, mỗi lượt `dotnet test` chạy một mình.

## 2. Chia 5 phần

| Phần | Nội dung (spec) | Commit |
| --- | --- | --- |
| Spec | Spec đợt 1, README module, dòng trong danh mục | `0a46022` |
| 1 | Webhook SePay `POST /api/webhooks/payment/sepay` (HMAC), phân loại theo mã `TTPTHD{Id}`, ghi sổ qua `InvoicePaymentTransaction` (§4, §5) | `6b690e4`, `33e210e` (gỡ script), `0e6924c` (log) |
| 2 | Thông báo sau ghi sổ: trong app, SignalR, Telegram, email biên lai, cảnh báo giao dịch cần xử lý tay; chặn ghi trùng khi xác nhận minh chứng cũ (§5.3, §5.4, §6) | `2111a43` |
| 3 | Cổng khách thuê: panel VietQR, deeplink, biên lai web in A5, gỡ luồng khách tự tạo lượt và nộp minh chứng (§3, §8) | `6057625` |
| 4 | Cổng quản lý: email hóa đơn có nút thanh toán, QR dùng `TTPTHD{Id}`, nút Biên lai/Gửi lại biên lai, màn Giao dịch SePay và đồng bộ bù (§4.3, §7) | `d093a96` |
| 5 | Tài liệu: hướng dẫn thiết lập, cập nhật README module, danh mục và `AGENTS.md` | chưa commit tại thời điểm viết |

Thứ tự bắt buộc: phần 1 trước (các phần sau dùng cùng ghi sổ); phần 2 dựa vào kết quả ghi sổ của phần 1; phần 3 và 4 dùng thông báo/biên lai của phần 2; phần 5 sau cùng. Phần 5 chỉ là tài liệu nên không chạy `/ship`.

## 3. Quyết định đã chốt theo phần

### Phần 1

- Người dùng xác nhận các điểm hỏi T1–T11 và Q1–Q6; chi tiết từng câu không còn trong tài liệu. Còn lại trong commit: lỗi hệ thống trả 500 để SePay thử lại, tắt (`SePay:Enabled = false`) thì trả 404.
- Chống trùng bằng `LichSuThanhToan.MaGiaoDich` (`SEPAY-{referenceCode}` hoặc `SEPAY-ID-{id}`), kiểm cả dòng đã xóa mềm; unique index sẵn có là lớp chặn thứ hai.
- Việc chặn ghi trùng khi xác nhận minh chứng `DangDoiChieu` chuyển sang phần 2.
- Người dùng thử tay phần này trên SePay Test mode và đạt.

### Phần 2

- Hàng đợi Channel trong process + `PaymentNotificationWorker` luôn đăng ký, không phụ thuộc `BackgroundJobs:Enabled`.
- Telegram: chat chung và chat theo chi nhánh; cache 24 giờ chống cảnh báo lặp.
- §5.4: dòng SePay ghi sau lúc khách nộp minh chứng thì nhân viên phải chuyển Admin, Admin phải ghi chú; mã giao dịch (nhân viên nhập, khách khai trên minh chứng, hoặc thu thủ công) trùng một dòng `SEPAY-` chưa xóa thì báo lỗi.

### Phần 3

- Deeplink 12 ngân hàng: HDBank và VIB thay Sacombank và MSB, vì hai ngân hàng này không có trong danh sách deeplink chính thức của VietQR (người dùng chọn thay).
- Panel hiển thị QR kèm cảnh báo giữ nguyên nội dung chuyển khoản (phương án A); nút "Tôi đã chuyển khoản, kiểm tra lại" luôn hiện.
- Link trong email biên lai trỏ `/KhachThue/HoaDon/BienLai/{id}`; gỡ endpoint khách tạo lượt, hủy lượt, nộp minh chứng.

### Phần 4

- Đồng bộ bù theo PA2: cờ `SePay:SyncRecordEnabled` mặc định tắt, màn Giao dịch SePay chỉ đối chiếu; chỉ bật ghi sổ sau khi kiểm `referenceCode` = `reference_number`.
- Đồng bộ bù không bắn Telegram cho kết quả không ghi sổ.
- E1: đổi toàn bộ QR (email, PDF, màn chi tiết quản lý) sang `TTPTHD{Id}` với số còn lại, và đổi hai chuỗi hiển thị cho khớp.
- Các điểm X3–X7 người dùng đồng ý theo đề xuất của planner.

### Phần 5

- Hướng dẫn thiết lập và kịch bản thử tay ghi bằng lệnh và từng bước, không script.

## 4. Việc phát sinh ngoài kế hoạch gốc

- Sau khi thử tay (20/10/2026): sửa giao diện responsive của modal chi tiết hóa đơn ở ba màn (khách thuê, quản lý, lịch sử thanh toán). Chỉ đổi view, CSS và JS; ghi ở báo cáo ngày 2026-10-20.

## 5. Việc để sau (chưa có spec hoặc chưa làm)

- Chốt chặn "Nghi trùng, kiểm tay" cho đồng bộ bù; làm trước khi bật `SePay:SyncRecordEnabled`.
- Thử tay đồng bộ bù và thử với giao dịch thật (mới thử trên Test mode).
- Đợt 2 VNPay: cần spec riêng (spec §12).
- Docker: spec riêng khi cần deploy (spec §14).
- Dọn các method cũ trong `InvoicePaymentRequestService`, outbox thông báo, lưu giao dịch không khớp (cần đổi schema, spec §14).
- Gộp nhánh vào `master`: chờ người dùng quyết định.
