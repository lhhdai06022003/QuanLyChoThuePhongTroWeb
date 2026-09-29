# Cổng khách vãng lai

Yêu cầu nghiệp vụ nằm trong [SPEC.md](SPEC.md); tiến độ, quyết định và bằng chứng kiểm thử nằm trong [PLAN.md](PLAN.md).

## Các màn hình

- `/phong`: danh sách phòng công khai; `/phong/{maCongKhai}`: thông tin và ảnh phòng.
- `/lich-xem/{maCongKhai}`: khách đã đăng nhập chọn khung giờ hoặc đề xuất thời gian; `/lich-xem`: lịch của mình.
- `/QuanLyNhaTro/DangNhap`: dùng chung trang đăng nhập hiện có. Sau đăng nhập, khách quay về lịch xem hoặc giữ chỗ hợp lệ đã chọn.
- `/tai-khoan/ho-so`: bổ sung email và CCCD trước khi giữ chỗ.
- `/giu-cho`: theo dõi yêu cầu; `/thanh-toan-giu-cho/{reservationId}`: hướng dẫn chuyển tiền và gửi minh chứng; `/giu-cho/hoan-tien`: theo dõi hoàn tiền.

Danh sách và chi tiết phòng không yêu cầu đăng nhập. Liên hệ, hẹn xem và bắt đầu đặt cọc yêu cầu đăng nhập; số điện thoại chỉ hiện sau đăng nhập. Yêu cầu giữ chỗ phải được nhân viên duyệt trước khi khách chuyển cọc.

Nhân viên vào **Tin phòng công khai → Ảnh** để chọn tối đa 10 ảnh PNG/JPEG/WebP một lần, tối đa 5 MB mỗi ảnh. Chọn một ảnh bìa nếu cần. Khách bấm mũi tên trước/sau hoặc ảnh thu nhỏ để đổi ảnh; ảnh đã ẩn không hiển thị công khai.

Trang đăng ký cần đối chiếu với giao diện người dùng đang sử dụng; người dùng yêu cầu hoàn tất đăng nhập trước.

## Tổ chức mã nguồn

Application chứa các feature `PhongCongKhais`, `KhachVangLais`, `LichXemPhongs`, `GiuChos`. Domain giữ quy tắc tiền và trạng thái; Infrastructure thực hiện persistence, lưu ảnh và job hết hạn. Web dùng Razor trong `Areas/KhachVangLai`; màn nhân viên nằm trong `Areas/QuanLyNhaTro`.

## Vận hành

Thông tin ngân hàng nhận tiền dùng cấu hình `VietQrSettings` chung. Trong môi trường Development, ảnh phòng công khai được lưu tại `src/QuanLyChoThuePhongTroWeb.Web/wwwroot/uploads/rooms/` để tải ảnh khi không có kết nối Cloudinary; thư mục này bị Git bỏ qua và cần được giữ lại khi khởi động lại. Môi trường khác vẫn dùng Cloudinary. Minh chứng chuyển khoản được lưu riêng ngoài `wwwroot`, chỉ đọc qua controller kiểm tra quyền. Cấu hình, mật khẩu và dữ liệu minh chứng không đưa vào Git.

Chạy kiểm thử tích hợp với `QLCTPT_TEST_CONNECTION_STRING` trỏ tới database riêng có hậu tố `_test` hoặc `_integration_test`. Fixture chạy migration; không dùng database phát triển hoặc production.
