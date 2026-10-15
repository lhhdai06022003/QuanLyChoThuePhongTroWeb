# Module: Kiểm tra chỉ số theo hợp đồng và duyệt hóa đơn trước khi gửi

- **Nối tiếp:** [hóa đơn tự động](../hoa-don-tu-dong/spec.md) (đã gộp `master` = `c11946c`). Spec này thay một phần luồng đó.
- **Nhánh Git:** `feature/duyet-hoa-don-tu-dong`.
- **Hiện trạng:** spec v4 đã duyệt, plan chờ duyệt (12/10/2026). Chưa code.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [ ] So tiêu thụ với trung vị tối đa 6 tháng trọn trong cùng hợp đồng; bắt tăng, giảm bất thường và nghi đọc sai chữ số.
- [ ] Hợp đồng mới chưa đủ 2 tháng trọn: so với trần cố định tính theo số ngày ở.
- [ ] Nhân viên xác nhận số bất thường thấy cảnh báo và phải ghi chú.
- [ ] Hóa đơn tự tạo ở nháp; nhân viên gửi duyệt, Admin chốt, nhân viên hoặc Admin công bố.
- [ ] Admin sửa được hóa đơn đã chốt trước khi công bố.
- [ ] Điều chỉnh chỉ số và khoản phí trước khi công bố.
- [ ] Đặt hạn phản hồi (mốc thông báo) lúc công bố; khách thanh toán được ngay.
- [ ] Bỏ job tự chốt hóa đơn tạm tính.

## Hồ sơ triển khai

- `spec.md`: đặc tả, người dùng đã duyệt v4 (12/10/2026).
- `plan.md`: kế hoạch triển khai 13 task, chờ người dùng duyệt.
