# Module 1 — Ảnh chỉ số, OCR và hóa đơn

- **Người phụ trách:** A
- **Mốc roadmap:** ngày 11–31, ổn định ngày 32–53
- **Hiện trạng:** đã có schema, tính điện nước/hóa đơn cũ và model trạng thái phát hành; luồng OCR → duyệt → hóa đơn nháp → chốt chưa hoàn chỉnh.

## Tính năng cần hoàn thành

- [ ] Khách/nhân viên tải ảnh đồng hồ điện hoặc nước; lưu ảnh lỗi và lần chụp lại riêng.
- [ ] AI đọc ảnh và ghi một kết quả gợi ý cùng độ tin cậy lên `AnhChiSoDongHo`.
- [ ] Nhân viên đối chiếu, sửa và xác nhận chỉ số chính thức.
- [ ] Tính điện/nước, dịch vụ và tiền phòng bằng `decimal` theo dữ liệu đã duyệt.
- [ ] Tạo hóa đơn nháp, kiểm tra và chốt/gửi/hủy theo quyền.
- [ ] Ghi lịch sử trạng thái phát hành và thanh toán của hóa đơn.
- [ ] Nối Application Service, API/MVC, Razor và test của toàn luồng.

## Ranh giới

A sở hữu luồng chỉ số và hóa đơn. Thanh toán hóa đơn được theo dõi riêng tại module 2. B không sửa service, controller hoặc view thuộc luồng này. Schema đã có; thay đổi database cần migration do A tạo và kiểm thử trên PostgreSQL test.

## Hồ sơ triển khai

- `spec/`: viết đặc tả khi bắt đầu thực hiện.
- `plan/`: viết kế hoạch triển khai sau khi đặc tả được chốt.
- `review/`: lưu kết quả review và kiểm chứng khi có mã triển khai.

Hiện các thư mục trên chỉ có `.gitkeep`; chưa có tài liệu spec, plan hoặc review.
