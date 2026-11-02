# Trợ lý tìm phòng và tư vấn cọc

Người dùng đã chọn thực hiện phạm vi đề xuất: AI tìm phòng và tư vấn đặt cọc trước. Khách chưa đăng nhập vẫn chat được; các nút hẹn xem/đặt phòng tiếp tục dùng luồng đăng nhập hiện có.

## Thiết kế

- Tái sử dụng `IAiChatModel`/Gemini hiện có. Service trợ lý công khai riêng, không mở rộng quyền của trợ lý quản lý/khách thuê.
- Chỉ có công cụ tìm phòng công khai theo ngân sách/khu vực/diện tích/số người và xem chi tiết phòng công khai. Tất cả dữ liệu đi qua `IPublicRoomService` và danh sách giới hạn `PublicRooms:RoomIds` hiện có; không gửi số điện thoại chi nhánh hoặc dữ liệu khách thuê cho model.
- Trả lời tiếng Việt, thông tin cọc từ `DepositAmount`; phòng thiếu mức cọc báo chưa mở đặt cọc. Giải thích đúng quy trình: đăng nhập khi hẹn xem/đặt phòng, khách chọn thời gian, nhân viên gọi xác nhận; thanh toán cọc 30 phút, chỉ giữ khi nhận đủ tiền, ký trong 48 giờ sau đó. Hoàn tiền theo xử lý của nhân viên.
- Thẻ kết quả và đường dẫn do server dựng từ dữ liệu đã đọc, không dùng HTML/URL model sinh. Tra lại phòng trước khi trả kết quả để loại phòng vừa bị ẩn. Chat không đặt lịch, tạo giữ chỗ hay chuyển tiền.
- API POST cùng nguồn, anti-forgery, tối đa 32 KB, câu hỏi 1.000 ký tự, lịch sử 8 tin được giới hạn, tối đa 3 vòng công cụ và 4 công cụ/vòng; 6 câu hỏi/phút/IP, 40 giây/yêu cầu. Không lưu lịch sử DB hoặc trình duyệt, chỉ giữ trong bộ nhớ tab.
- Nếu Gemini chưa cấu hình hoặc lỗi: thông báo rõ, giữ nội dung đã nhập và có liên kết tìm phòng/cẩm nang; không giả làm câu trả lời AI.

## Giao diện

Nút “Hỏi AI tìm phòng” cố định góc phải, mở bảng chat 400 px trên máy tính và gần toàn màn hình trên điện thoại. Inter theo trang hiện có; nền trắng #ffffff, chữ #374151, phụ #606b7a, xanh #066fd1, nền nhạt #edf5fd, viền #dce3ec. Tin nhắn căn trái; thẻ phòng làm rõ giá thuê và cọc. Có câu hỏi gợi ý, đang trả lời, thử lại, bắt đầu lại, đóng và trả focus về nút mở. Nội dung model chỉ hiển thị dạng văn bản.

## Ràng buộc

Không sửa schema, migrations, entities, DbContext hay dữ liệu thật để kiểm thử. Giữ toàn bộ thay đổi đặt cọc/giao diện đang có trên `codex/khach-vang-lai`; không tự push hoặc merge. Tính năng tra cứu hồ sơ/trạng thái cá nhân của khách nằm ngoài đợt này.

## Phương án

Mở quyền trợ lý quản lý cho khách có rủi ro lộ dữ liệu; một chatbot luật cố định không xử lý tốt yêu cầu tự nhiên. Chọn service công khai riêng dùng lại Gemini và public-room service để giữ quyền rõ ràng và tận dụng mã hiện có.
