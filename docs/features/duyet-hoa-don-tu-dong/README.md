# Module: Kiểm tra chỉ số theo hợp đồng và duyệt hóa đơn trước khi gửi

- **Nối tiếp:** [hóa đơn tự động](../hoa-don-tu-dong/spec.md) (đã gộp `master` = `c11946c`). Spec này thay một phần luồng đó.
- **Nhánh Git:** `feature/duyet-hoa-don-tu-dong`.
- **Hiện trạng:** Đã code đủ 13 task trên nhánh `feature/duyet-hoa-don-tu-dong` (13/10/2026); review cuối và các đợt sửa xong (14/10/2026); chờ người dùng kiểm thử tay và duyệt gộp.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [x] So tiêu thụ với trung vị tối đa 6 tháng trọn trong cùng hợp đồng; bắt tăng, giảm bất thường và nghi đọc sai chữ số.
- [x] Hợp đồng mới chưa đủ 2 tháng trọn: so với trần cố định tính theo số ngày ở.
- [x] Nhân viên xác nhận số bất thường thấy cảnh báo và phải ghi chú.
- [x] Hóa đơn tự tạo ở nháp; nhân viên gửi duyệt, Admin chốt, nhân viên hoặc Admin công bố.
- [x] Admin sửa được hóa đơn đã chốt trước khi công bố.
- [x] Điều chỉnh chỉ số và khoản phí trước khi công bố.
- [x] Đặt hạn phản hồi (mốc thông báo) lúc công bố; khách thanh toán được ngay.
- [x] Bỏ job tự chốt hóa đơn tạm tính.

## Hồ sơ triển khai

- `spec.md`: đặc tả, người dùng đã duyệt v4 (12/10/2026).
- `plan.md`: kế hoạch 13 task, đã thực hiện.
