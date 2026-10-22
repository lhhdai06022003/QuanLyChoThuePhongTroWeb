# Module: AI Assistant và Thông báo theo chi nhánh

- **Mốc lộ trình vận hành:** bước 2, sau bước 1 "Phân quyền chi nhánh" (commit `818c949`).
- **Nhánh Git:** `feature/ai-thong-bao-chi-nhanh`.
- **Hiện trạng:** đặc tả 1.0 đã duyệt ngày 05/10/2026 ([spec.md](spec.md)). Code xong, test tự động PASS, người dùng thử tay đạt ngày 06/10/2026. Chưa commit.
- **Schema:** không đổi.

## Tính năng cần hoàn thành

- [x] Nhân viên dùng bộ công cụ AI của cổng quản lý, không dùng bộ của khách thuê.
- [x] AI của nhân viên chỉ đọc dữ liệu thuộc chi nhánh được phân công.
- [x] Khách thuê dùng được AI và chỉ thấy dữ liệu của mình.
- [x] AI chỉ chạy được công cụ thuộc vai trò của người hỏi.
- [x] AI gọi được nhiều công cụ trong một câu hỏi, kiểm tra tham số, xử lý câu hỏi ngoài phạm vi và ghi nhật ký.
- [x] Widget có nút gợi ý câu hỏi theo vai trò và lưu lịch sử chat riêng cho từng tài khoản.
- [x] Thông báo sự cố mới chỉ gửi Admin và nhân viên được phân công chi nhánh của phòng.
- [x] Người dùng chỉ đánh dấu đã đọc được thông báo của mình.

## Hồ sơ triển khai

- `spec.md`: đặc tả tính năng, cần được duyệt trước.
- `plan.md`: kế hoạch triển khai, viết sau khi spec được duyệt.
- `review.md`: kết quả review và kiểm chứng khi đã có mã.

Đã có `spec.md`, `plan.md` và [review.md](review.md).
