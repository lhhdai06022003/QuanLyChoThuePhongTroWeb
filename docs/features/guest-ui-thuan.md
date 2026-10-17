# Giao diện khách vãng lai trên thuan — 17/10/2026

Chuyển giao diện công khai từ bản đang phát triển trên `codex/khachvanglai-master-schema` sang nền mã hiện có của `thuan`. Không merge master hay sửa entity, DbContext, migration, schema hoặc dữ liệu database.

## Phạm vi

- Giao diện xanh/trắng đồng bộ admin: danh sách, chi tiết/ảnh, bộ lọc nâng cao, sắp xếp, phòng yêu thích, so sánh và cẩm nang. Yêu thích/so sánh chỉ lưu ID trong trình duyệt.
- Khách chưa đăng nhập mở trang chủ vào `/phong`. Trang hẹn xem/đặt phòng hiển thị lời nhắc; chỉ nút Đăng nhập mở form đăng nhập có sẵn và giữ ReturnUrl.
- Dùng kiểu giá `double`, danh sách phòng được cho phép trong cấu hình và cách đọc ảnh hiện có của thuan. Không thay DTO sang kiểu tiền của master hay thay cơ chế công khai phòng.
- Giữ các màn đăng ký, hẹn xem, giữ chỗ và theo dõi yêu cầu dạng mẫu hiện có của thuan. Các nút gửi nghiệp vụ chưa có xử lý vẫn bị vô hiệu hóa. Phần nghiệp vụ hoàn chỉnh trên nhánh phát triển không nằm trong lần chuyển giao diện này.
- Thêm tùy chọn `Database:InitializeOnStartup=false` để có thể xem thử mà không chạy khởi tạo/migration/seed. Mặc định hiện có không đổi.

## Kiểm chứng

- Build solution thành công, không lỗi; còn cảnh báo nullable/async có sẵn.
- Application: 57/57, Domain: 7/7, kiểm thử chọn phòng bằng Node: 8/8.
- HTTP giao diện: 9/9 với service phòng giả, tắt khởi tạo và background jobs; không mở database. Kiểm tra danh sách, chi tiết, cẩm nang, đã lưu, so sánh, đăng ký mẫu, hai trang nhắc đăng nhập, form đăng nhập gốc và root redirect.
- Không chạy integration suite phụ thuộc DB của nhánh cũ trên database đang dùng bản master.
- Mã chưa commit trước khi chuyển đã được giữ trong stash `backup/thuan-before-guest-ui-2026-10-17`; phần màn quản lý mẫu chưa được chọn vẫn giữ tại máy.
