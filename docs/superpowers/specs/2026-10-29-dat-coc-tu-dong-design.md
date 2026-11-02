# Đặt cọc theo giá công khai, không chờ nhân viên duyệt

## Các yêu cầu đã chốt

- Làm trên `codex/khach-vang-lai`, không merge về `thuan`.
- Quản lý nhập tiền cọc riêng cho từng phòng khi đăng/sửa tin.
- Khách nhìn thấy tiền cọc trước khi đặt phòng; vẫn dùng đăng nhập hiện có để xác định người đặt.
- Bỏ bước duyệt từng yêu cầu trước khi thanh toán.
- Chỉ tự ẩn phòng khi SePay/VNPay xác thực đã nhận đủ tiền cọc.
- Gửi ảnh chuyển khoản không được xem là đã nhận tiền.
- Không thêm cột, bảng, migration hoặc thay đổi schema. Người dùng chọn lưu tiền cọc công khai trong cấu hình riêng trên máy chủ.

## Cách triển khai được chọn

1. Lưu giá cọc theo ID phòng trong file JSON riêng dưới `App_Data`, ngoài `wwwroot`, không đưa vào Git. Dùng khóa khi sửa và ghi file tạm rồi thay thế nguyên tử để tránh hỏng file. Không dùng cột mô tả để nhét cấu hình.
2. Bổ sung trường tiền cọc trong màn Tin phòng. Hiện giá trên thẻ phòng, trang chi tiết, xác nhận đặt và trang thanh toán. Phòng chưa có giá cọc vẫn xem được nhưng chưa mở đặt cọc tự động.
3. Khi khách đặt, server kiểm tra lại phòng và giá cọc; tạo giữ chỗ và yêu cầu thanh toán ngay trong một giao dịch, dùng các bảng hiện có. Lưu số tiền đã chốt vào yêu cầu để lần sửa tin sau không đổi tiền khách đang thanh toán. Không ghi giả nhân viên đã duyệt.
4. Chuyển khách thẳng tới trang QR/VNPay hiện có. Sửa script VNPay để hoạt động trên layout công khai không có jQuery.
5. Bỏ điều kiện ẩn phòng chỉ vì đang chờ chuyển khoản. Khi nhận đủ tiền và giữ chỗ còn hợp lệ, chuyển sang đang giữ chỗ và loại phòng khỏi danh sách công khai, chi tiết công khai và đặt cọc mới.
6. Giữ xác thực webhook/IPN, chống ghi trùng mã giao dịch và xử lý tiền đến muộn. Tiền tới sau khi yêu cầu đã hết hạn không tự giành lại phòng.

## Cạnh tranh giữa nhiều khách và các hạn — đề xuất cần chốt

Schema hiện có unique index chỉ cho một yêu cầu đang chờ thanh toán/chờ đối soát/đang giữ chỗ trên mỗi phòng. Vì vậy không tạo nhiều mã thanh toán cùng lúc cho một phòng.

- Đề xuất mỗi lượt thanh toán có hạn **30 phút**. Phòng vẫn xuất hiện công khai trong thời gian này; khách khác được thông báo đang có lượt thanh toán và không nhận thêm mã chuyển khoản cho cùng phòng.
- Đề xuất hạn ký hợp đồng **48 giờ sau thời điểm nhận đủ cọc**. Quá hạn ký, giữ cách xử lý hiện có: nhân viên quyết định gia hạn/hủy và xử lý hoàn, không tự mất tiền của khách.
- Hai khoảng thời gian là cấu hình trên máy chủ, không thêm trường DB.
- Khách quay lại trong hạn tiếp tục đúng yêu cầu và mã thanh toán cũ. Hủy hoặc hết hạn chưa nhận tiền sẽ giải phóng lượt thanh toán.

## Các phương án đã xem xét

- Thêm cột tiền cọc vào bảng phòng: không chọn vì người dùng yêu cầu giữ schema.
- Lưu giá cọc trong cấu hình riêng, chốt giá vào yêu cầu thanh toán hiện có: người dùng đã chọn.
- Ẩn phòng ngay khi có ảnh chuyển khoản: không chọn; người dùng đã chốt phải nhận đủ tiền qua hệ thống.

## Giới hạn vận hành

- File giá cọc cần được giữ cùng dữ liệu ứng dụng khi chuyển máy hoặc triển khai. Thiết kế dùng cho một máy chủ ứng dụng hiện tại; không tuyên bố hỗ trợ nhiều máy chủ dùng các bản file khác nhau.
- Nếu chưa cấu hình thanh toán hoặc webhook/IPN chưa nhận được xác nhận hợp lệ thì không báo đã thanh toán, không tự ẩn phòng theo ảnh minh chứng.
- Yêu cầu cũ không tự đổi tiền hoặc ghi giao dịch. Tương thích mã thanh toán và lịch sử hiện có.

## Kiểm chứng dự kiến

- Giá cọc được lưu, đọc lại sau khởi động, phân quyền đúng chi nhánh; chặn số tiền không hợp lệ và dữ liệu giá bị sửa ở trình duyệt.
- Đặt phòng sinh số tiền/mã thanh toán ngay; không cần gọi duyệt; chống tạo trùng/cạnh tranh hai khách.
- Chưa trả hoặc trả thiếu: phòng chưa tự ẩn. Trả đủ đúng hạn: phòng tự ẩn. Callback lặp không ghi tiền hai lần; tiền đến muộn không giành lại phòng.
- Kiểm thử luồng QR/VNPay, trang công khai và khách chưa đăng nhập; chỉ dùng DB test và dịch vụ thanh toán giả.
- Build và rà diff đảm bảo không thay đổi schema/migration.
