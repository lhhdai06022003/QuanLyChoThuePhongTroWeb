# Module: AI đọc chỉ số điện nước và cảnh báo theo mức

- **Nối tiếp:** [duyệt hóa đơn tự động](../duyet-hoa-don-tu-dong/README.md).
- **Nhánh Git:** `feature/ai-phan-tich-dien-nuoc`.
- **Hiện trạng (03/11/2026):** Spec v1 đã duyệt (02/11/2026). Code đã xong 5 task qua `/ship` (model ONNX, bộ quy tắc, đổi đồng hồ/quay vòng, giao diện, Docker), đã qua test và review, đã sửa các điểm S1–S4 của review. Đã commit trên nhánh (7 commit, `2805b1e`…`68d52bc`), chưa push, chưa gộp `master`. `MeterAnomalyDetector` và Gemini đọc ảnh đã gỡ.
  - Tối ưu luồng đọc ảnh: đổ tensor và đọc đầu ra model bằng span; OCR dùng luôn ảnh vừa tải lên khi đúng JPG/PNG/WebP, chỉ đọc lại từ Cloudinary khi nội dung lạ hoặc bấm "Thử lại". Đo bằng app tạm (Debug, ảnh tổng hợp): quét 416→296 ms (ảnh 640), 703→585 ms (ảnh 4000×3000); cấp phát ~111 MB → ~10 MB mỗi lần quét.
  - `SixLabors.ImageSharp` giữ 3.1.12 (bản 3.2.0 đòi license khi build Release). Lỗ hổng của 3.1.12 nằm ở bộ giải mã TIFF và vài bộ xử lý không dùng tới; bộ đọc chỉ số chỉ nạp bộ giải mã JPG, PNG, WebP nên TIFF bị từ chối trước khi giải mã. Build còn cảnh báo NU1902/NU1903, đã chấp nhận.
  - Đã kiểm: unit và integration test PASS; câu SQL rà dữ liệu cũ PASS (0 dòng lệch, DB phát triển); thử tay giao diện PASS (người dùng báo); Docker PASS (03/11/2026: model đọc được ảnh trong container Linux, log không lỗi).
  - Chưa đo độ chính xác model (thiếu bộ ảnh), nên cờ `TuXacNhanAnhAi` vẫn tắt.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [x] Đọc ảnh chỉ dùng model ONNX, gỡ Gemini khỏi đọc ảnh.
- [x] AI chỉ gợi ý số; có cờ `TuXacNhanAnhAi`, mặc định tắt.
- [ ] Đo độ chính xác model trên bộ ảnh thật (test đo `OnnxMeterAccuracyEvaluationTests` đã có, chờ bộ ảnh và biến `QLCTPT_METER_EVAL_DIR`).
- [x] Một bộ quy tắc chung, giữ ngưỡng trung vị, thêm mức cảnh báo và gợi ý sửa số.
- [x] Thao tác đổi đồng hồ và quay vòng; nhắc thêm dòng truy thu trên hóa đơn.
- [x] Giao diện cảnh báo và nén ảnh trên trình duyệt (người dùng đã thử tay).
- [x] Docker demo cục bộ ở cổng 5009, secret không nằm trong image (cấu hình lấy từ `.env`, mẫu ở `.env.example`).

## Hồ sơ triển khai

- `spec.md`: đặc tả, người dùng đã duyệt v1 (02/11/2026).
- `plan.md`: viết sau khi spec được duyệt.
