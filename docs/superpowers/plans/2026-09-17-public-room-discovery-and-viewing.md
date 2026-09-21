# Kế hoạch tìm phòng công khai và lịch xem ngày 17/09 (đã thay thế)

Tài liệu này đã được thay thế ngày 21/09/2026 sau khi phạm vi database và cách chia việc được chốt lại. Không thực thi checklist cũ trong lịch sử Git vì các điểm sau đã thay đổi:

- Ảnh phòng dùng bảng `AnhPhongTro`, không dùng một cột `AnhDaiDienUrl`.
- Yêu cầu xem phòng bắt buộc họ tên và số điện thoại; email tùy chọn và không dùng token email.
- AI được tự xác nhận khung giờ đã cấu hình và còn chỗ; trường hợp ngoài khung chuyển cho nhân viên.
- Giữ chỗ yêu cầu tài khoản `KhachVangLai`.
- Người A tạo toàn bộ entity, EF configuration và hai migration nền trước khi hai người phát triển song song.

Nguồn thiết kế hiện hành:

- [Thiết kế nền tảng hai migration](../specs/2026-09-21-database-foundation-two-migrations-design.md)
- [Kế hoạch Migration 1](2026-09-21-migration-1-billing-foundation.md)
- [Kế hoạch Migration 2](2026-09-21-migration-2-public-reservation-foundation.md)

Tài liệu ngày 17/09 chỉ còn giá trị lịch sử qua Git.
