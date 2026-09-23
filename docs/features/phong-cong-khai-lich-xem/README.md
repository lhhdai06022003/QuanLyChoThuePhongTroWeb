# Module 3 — Phòng công khai và lịch xem

- **Người phụ trách:** B
- **Mốc roadmap:** ngày 11–31
- **Hiện trạng:** đã có entity/schema gallery, khung giờ và yêu cầu xem phòng; trang công khai, Application Service và API chưa hoàn chỉnh.

## Tính năng cần hoàn thành

- [ ] Đăng/ẩn tin phòng và quản lý gallery ảnh, ảnh bìa.
- [ ] Khách không đăng nhập tìm, lọc và xem chi tiết phòng công khai.
- [ ] Nhân viên cấu hình khung giờ xem và sức chứa.
- [ ] Khách gửi yêu cầu xem phòng với họ tên và số điện thoại; email tùy chọn.
- [ ] AI xác nhận khung giờ hợp lệ còn chỗ qua Application Service.
- [ ] Chuyển yêu cầu ngoài khung hoặc xung đột cho nhân viên xử lý.
- [ ] Lưu lịch sử trạng thái, kiểm tra quyền chi nhánh và test cạnh tranh.

## Ranh giới

B sở hữu luồng phòng công khai và lịch xem từ Application đến Web. `PhongTros` hiện phục vụ quản lý phòng nội bộ; nếu cần sửa file cũ hoặc contract dùng chung, hai người thống nhất trước. B không sửa migration, Domain model hoặc file DI/`Program.cs` dùng chung; gửi yêu cầu cho A tích hợp.

## Hồ sơ triển khai

- `spec/`: viết đặc tả khi bắt đầu thực hiện.
- `plan/`: viết kế hoạch triển khai sau khi đặc tả được chốt.
- `review/`: lưu kết quả review và kiểm chứng khi có mã triển khai.

Hiện các thư mục trên chỉ có `.gitkeep`; chưa có tài liệu spec, plan hoặc review.
