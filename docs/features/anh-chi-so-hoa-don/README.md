# Module 1 — Ảnh chỉ số, OCR và hóa đơn

- **Người phụ trách:** A
- **Mốc roadmap:** ngày 11–31, ổn định ngày 32–53
- **Hiện trạng:** đã có schema, tính điện nước/hóa đơn cũ và model trạng thái phát hành; luồng OCR → duyệt → hóa đơn nháp → chốt chưa hoàn chỉnh.
- **Phân quyền giai đoạn này:** mọi `NhanVien` có phân công chi nhánh còn hiệu lực cùng quyền thao tác nhân viên trong module; chỉ Admin chốt/trả lại. Quyền riêng từng nhân viên được ghi ở `spec.md` như hướng phát triển sau.

## Tính năng cần hoàn thành

- [ ] Khách/nhân viên tải ảnh đồng hồ điện hoặc nước; lưu ảnh lỗi và lần chụp lại riêng.
- [ ] AI đọc ảnh và ghi một kết quả gợi ý cùng độ tin cậy lên `AnhChiSoDongHo`.
- [ ] Nhân viên đối chiếu, sửa và xác nhận chỉ số chính thức.
- [ ] Tính điện/nước, dịch vụ và tiền phòng bằng `decimal` theo dữ liệu đã duyệt.
- [ ] Tạo hóa đơn nháp, kiểm tra và chốt/gửi/hủy theo quyền.
- [ ] Ghi lịch sử trạng thái phát hành và thanh toán của hóa đơn.
- [ ] Nối Application Service, API/MVC, Razor và test của toàn luồng.

## Ranh giới

A sở hữu luồng chỉ số và hóa đơn. Thanh toán hóa đơn được theo dõi riêng tại module 2. B không sửa service, controller hoặc view thuộc luồng này. Giai đoạn này dùng schema hiện có, không tạo migration hay thêm cột/bảng cho chỉ số và email. Khi thật sự cần mở rộng schema ở giai đoạn sau, A lập thiết kế riêng và kiểm thử migration trên PostgreSQL test.

## Hồ sơ triển khai

- `spec.md`: đặc tả nghiệp vụ và thiết kế đã chốt.
- `plan.md`: kế hoạch triển khai theo từng phần.
- `review/`: lưu kết quả review và kiểm chứng khi có mã triển khai.
