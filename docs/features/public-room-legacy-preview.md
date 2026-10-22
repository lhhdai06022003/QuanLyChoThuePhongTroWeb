# Xem trước cổng phòng với database cũ

Nhánh này dùng schema `master` cho môi trường thông thường. Để chỉ xem danh sách và chi tiết phòng trên database cũ `QuanLyPhongTroDb` mà không áp migration, đặt các biến môi trường sau trước khi chạy Web:

```text
Database__InitializeOnStartup=false
BackgroundJobs__Enabled=false
PublicRooms__LegacySchemaPreview=true
PublicRooms__RoomIds__0=10
PublicRooms__RoomIds__1=28
PublicRooms__RoomIds__2=32
PublicRooms__RoomIds__3=36
```

Kết nối `ConnectionStrings__DefaultConnection` phải trỏ tới `QuanLyPhongTroDb`; không đưa mật khẩu vào Git. Bản xem trước hiện tại còn thêm tùy chọn PostgreSQL `default_transaction_read_only=on` vào chuỗi kết nối để ngăn ghi dữ liệu.

Chế độ này chỉ đọc `phong_tro` và `chi_nhanh` theo các cột cũ, giới hạn ở ID cấu hình, phòng trống và chi nhánh chưa xóa. Ảnh được đọc từ `wwwroot/uploads/rooms/{id}`. Các trang quản trị và chức năng ghi dữ liệu của `master` vẫn cần schema `master`; không dùng chúng trên database cũ.
