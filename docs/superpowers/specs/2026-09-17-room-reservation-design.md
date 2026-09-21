# Thiết kế giữ chỗ ngày 17/09 (đã được hợp nhất)

Thiết kế này đã được rà soát và hợp nhất vào tài liệu ngày 21/09/2026. Không dùng nội dung cũ trong lịch sử Git để tạo entity hoặc migration.

Nguồn thiết kế hiện hành:

- [Thiết kế nền tảng cơ sở dữ liệu cho hai migration](2026-09-21-database-foundation-two-migrations-design.md)
- [Kế hoạch Migration 1](../plans/2026-09-21-migration-1-billing-foundation.md)
- [Kế hoạch Migration 2](../plans/2026-09-21-migration-2-public-reservation-foundation.md)

Các quyết định thay thế chính:

- Thanh toán hóa đơn và thanh toán giữ chỗ tách theo nghiệp vụ.
- Khách xem phòng không cần đăng nhập; họ tên và số điện thoại bắt buộc, email tùy chọn.
- Khách phải có tài khoản `KhachVangLai` trước khi gửi yêu cầu giữ chỗ; chỉ tạo `NguoiThue` khi lập hợp đồng.
- Không dùng phiên truy cập khách vãng lai hoặc token email cho yêu cầu xem phòng.
- AI chỉ tự xác nhận khung giờ xem phòng đã được cấu hình và còn chỗ.
- Ảnh phòng lưu trong bảng riêng.
- Tiền giữ chỗ được áp dụng vào tiền cọc đúng một lần; quyết định hoàn và các giao dịch hoàn thực tế được lưu riêng.
- Tên thuộc tính thời gian không có hậu tố `Utc`, nhưng giá trị vẫn được lưu theo UTC.
