# Module: AI đọc chỉ số điện nước và cảnh báo theo mức

- **Nối tiếp:** [duyệt hóa đơn tự động](../duyet-hoa-don-tu-dong/README.md).
- **Nhánh Git:** `feature/ai-phan-tich-dien-nuoc`.
- **Hiện trạng:** Spec v1 đã duyệt (02/11/2026). Code đã xong 5 task qua `/ship` (model ONNX, bộ quy tắc, đổi đồng hồ/quay vòng, giao diện, Docker), đã qua test và review, đã sửa các điểm S1–S4 của review, chưa commit; chưa thử tay trên giao diện, chưa đo độ chính xác model (thiếu bộ ảnh), chưa chạy Docker (thiếu `.env`). `MeterAnomalyDetector` và Gemini đọc ảnh đã gỡ.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [x] Đọc ảnh chỉ dùng model ONNX, gỡ Gemini khỏi đọc ảnh.
- [x] AI chỉ gợi ý số; có cờ `TuXacNhanAnhAi`, mặc định tắt.
- [ ] Đo độ chính xác model trên bộ ảnh thật (test đo `OnnxMeterAccuracyEvaluationTests` đã có, chờ bộ ảnh và biến `QLCTPT_METER_EVAL_DIR`).
- [x] Một bộ quy tắc chung, giữ ngưỡng trung vị, thêm mức cảnh báo và gợi ý sửa số.
- [x] Thao tác đổi đồng hồ và quay vòng; nhắc thêm dòng truy thu trên hóa đơn.
- [x] Giao diện cảnh báo và nén ảnh trên trình duyệt (chưa thử tay).
- [ ] Docker demo cục bộ ở cổng 5009, secret không nằm trong image (đã sửa tệp cấu hình, chưa chạy `docker compose` vì thiếu `.env`).

## Hồ sơ triển khai

- `spec.md`: đặc tả, người dùng đã duyệt v1 (02/11/2026).
- `plan.md`: viết sau khi spec được duyệt.
