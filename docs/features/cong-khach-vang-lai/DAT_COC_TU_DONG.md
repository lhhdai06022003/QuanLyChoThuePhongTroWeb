# Sử dụng đặt cọc tự động

## Quản lý đăng tin

1. Mở `/QuanLyNhaTro/TinPhong` và chọn **Chỉnh sửa tin**.
2. Nhập **Tiền cọc giữ chỗ** cùng tiêu đề, mô tả và tùy chọn đăng công khai.
3. Bấm **Lưu tin và ảnh**. Nhập số nguyên VND, không có dấu phân cách; số tiền phải lớn hơn 0.
4. Mở trang phòng công khai để kiểm tra giá cọc. Tin chưa có giá cọc vẫn xem được nhưng chưa nhận đặt cọc tự động.

## Khách đặt cọc

1. Xem giá cọc trên tin phòng rồi đăng nhập bằng tài khoản khách vãng lai hoặc khách thuê có sẵn.
2. Bấm **Đặt phòng và đặt cọc**. Server tạo mã thanh toán và chuyển ngay tới trang QR/VNPay; không cần nhân viên duyệt trước.
3. Chuyển đúng tài khoản và nội dung trong 30 phút. Cùng tài khoản quay lại vẫn sử dụng mã cũ, không tạo thêm mã đang hoạt động.
4. Sau khi hệ thống xác thực đã nhận đủ tiền, phòng tự ẩn khỏi danh sách công khai và trạng thái tài khoản chuyển sang đang giữ chỗ. Hạn ký hợp đồng là 48 giờ tính từ thời điểm nhận đủ tiền.

Trong thời gian chờ thanh toán, tin vẫn hiển thị; khách khác chưa được tạo lượt chuyển khoản cho cùng phòng. Trả thiếu hoặc gửi ảnh chưa được xem là hoàn thành. Hết hạn chưa đủ cọc thì giải phóng lượt, tiền đã nhận được xử lý theo quy tắc hoàn hiện có.

## Điều kiện tự nhận tiền

- `OnlinePayment:ReservationEnabled` phải bật để ghi nhận tiền giữ chỗ tự động.
- Với chuyển khoản QR trực tiếp: cần cấu hình và bật SePay, xác thực webhook đúng, cùng tài khoản nhận tiền và endpoint nhận callback truy cập được từ dịch vụ.
- Với VNPay: cần cấu hình merchant, khóa ký, URL IPN/return phù hợp và bật VNPay. Kết quả trên trình duyệt không tự thay thế IPN đã xác thực.
- Nếu dịch vụ chưa được cấu hình hoặc callback chưa tới, không được báo đã nhận tiền chỉ dựa vào ảnh hoặc nút bấm của khách.

Không ghi mật khẩu, khóa webhook, khóa VNPay hoặc thông tin ngân hàng thật vào commit.

## Lưu trữ

Giá theo phòng nằm trong `src/QuanLyChoThuePhongTroWeb.Web/App_Data/room-deposits.json` theo mặc định, có file khóa kèm theo. Giữ file giá cọc khi triển khai lại/chuyển máy; file này không được đưa vào Git. `RoomDeposits:StoragePath` có thể chỉ định nơi lưu riêng ngoài wwwroot.

Giữ chỗ, mã thanh toán, số tiền đã chốt, lịch sử và giao dịch sử dụng các bảng hiện có. Sửa giá tin sau khi khách bắt đầu thanh toán không sửa giá trên lượt thanh toán đó.

Thiết kế hiện tại dành cho một máy chủ ứng dụng với ổ đĩa bền vững. File cấu hình và database không thể cùng hoàn tất như một transaction duy nhất khi tiến trình bị tắt đột ngột; cần giữ dữ liệu ứng dụng và kiểm tra lại thao tác lưu tin nếu gặp sự cố đó.
