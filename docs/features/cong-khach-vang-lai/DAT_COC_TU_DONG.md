# Sử dụng đặt cọc tự động

## Quản lý đăng tin

1. Mở `/QuanLyNhaTro/TinPhong` và chọn **Chỉnh sửa tin**.
2. Nhập **Tiền cọc phòng** cùng tiêu đề, mô tả và tùy chọn đăng công khai.
3. Bấm **Lưu tin và ảnh**. Nhập số nguyên VND, không có dấu phân cách. Ẩn tin thì được để 0; đăng công khai thì phải lớn hơn 0.
4. Mở trang phòng công khai để kiểm tra giá cọc. Phòng Trống đang bật công khai nhưng cọc bằng 0 không hiện ở `/phong`; màn Tin phòng ghi "Ẩn — chưa có tiền cọc". Phòng Đã thuê/Bảo trì ghi "Chưa hiển thị" dù cọc bằng 0.

Chọn **Đăng công khai** thì ô tiền cọc bắt buộc và tối thiểu 1; thông báo lỗi ô nhập do trình duyệt sinh nên có thể là tiếng Anh. Server kiểm lại và trả "Nhập tiền cọc phòng lớn hơn 0 trước khi đăng công khai." nếu request bỏ qua kiểm tra của trình duyệt.

## Khách đặt cọc

1. Xem giá cọc trên tin phòng rồi đăng nhập bằng tài khoản khách vãng lai hoặc khách thuê có sẵn.
2. Bấm **Đặt phòng và đặt cọc**. Server tạo mã thanh toán và chuyển ngay tới trang QR/VNPay; không cần nhân viên duyệt trước.
3. Chuyển đúng tài khoản và nội dung trong 30 phút. Cùng tài khoản quay lại vẫn sử dụng mã cũ, không tạo thêm mã đang hoạt động.
4. Sau khi hệ thống xác thực đã nhận đủ tiền, phòng tự ẩn khỏi danh sách công khai và trạng thái tài khoản chuyển sang đang giữ chỗ. Hạn ký hợp đồng là 48 giờ tính từ thời điểm nhận đủ tiền. Trước đó trang khách ghi "trong 48 giờ sau khi nhận đủ cọc"; lượt hủy hoặc hết hạn khi chưa nhận đủ cọc không hiện hạn ký (`Web/Helpers/ReservationContractDeadline.cs`).

Trong thời gian chờ thanh toán, tin vẫn hiển thị; khách khác chưa được tạo lượt chuyển khoản cho cùng phòng. Trả thiếu hoặc gửi ảnh chưa được xem là hoàn thành. Hết hạn chưa đủ cọc thì giải phóng lượt, tiền đã nhận được xử lý theo quy tắc hoàn hiện có.

## Điều kiện tự nhận tiền

- `OnlinePayment:ReservationEnabled` phải bật để ghi nhận tiền giữ chỗ tự động.
- Với chuyển khoản QR trực tiếp: cần cấu hình và bật SePay, xác thực webhook đúng, cùng tài khoản nhận tiền và endpoint nhận callback truy cập được từ dịch vụ.
- Với VNPay: cần cấu hình merchant, khóa ký, URL IPN/return phù hợp và bật VNPay. Kết quả trên trình duyệt không tự thay thế IPN đã xác thực.
- Nếu dịch vụ chưa được cấu hình hoặc callback chưa tới, không được báo đã nhận tiền chỉ dựa vào ảnh hoặc nút bấm của khách.

Không ghi mật khẩu, khóa webhook, khóa VNPay hoặc thông tin ngân hàng thật vào commit.

## Lưu trữ

Tiền cọc phòng nằm ở cột `phong_tro."TienCocPhong"` (migration `AddRoomDepositAmount`), lưu cùng transaction với tiêu đề, mô tả và ảnh của tin. Bản đầu của nhánh lưu ở `App_Data/room-deposits.json`; số đã nhập ở file đó không tự chuyển sang DB, cần nhập lại trên màn Tin phòng.

Giữ chỗ, mã thanh toán, số tiền đã chốt, lịch sử và giao dịch sử dụng các bảng hiện có. Sửa giá tin sau khi khách bắt đầu thanh toán không sửa giá trên lượt thanh toán đó (số tiền đã chép vào `YeuCauGiuCho.SoTienGiuCho` và `YeuCauThanhToanGiuCho.SoTien`).

Hạn thanh toán 30 phút và hạn ký hợp đồng 48 giờ là hằng số trong `SelfServiceReservationRules`.

## Kiểm chứng

- Test tự động (DB `_test`): `RoomListingDepositTests`, `ReservationServiceTests`, `RoomDepositSchemaTests`, `SelfServiceReservationTests`, `TinPhongHoldStatusTests`, `GuestDepositLifecycleTests` và các test Web của cổng khách. Toàn bộ solution PASS ngày 04/11/2026.
- Thử tay 04/11/2026 trên DB phát triển: chặn đăng công khai khi cọc 0 (trình duyệt và server), đăng phòng có cọc, trang công khai và trang đặt phòng hiện đúng số cọc, khách bấm đặt cọc nhận ngay mã `TTPTGC{id}` với hạn 30 phút, hủy lượt từ `/tai-khoan/yeu-cau-phong`. Chưa thử nhận tiền thật qua SePay/VNPay.

## Hạn chế đã biết

- Lượt đã nộp minh chứng nhưng nhân viên chưa đối soát vẫn hết hạn sau 30 phút.
- Phòng đang chờ thanh toán vẫn hiện công khai; khách khác không tạo được lượt mới cho tới khi lượt hiện tại hủy hoặc hết hạn.
